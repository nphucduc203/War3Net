using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using War3Engine.Assets;
using War3Engine.Core;
using War3Engine.Core.Map;
using War3Engine.Core.Models;
using War3Engine.Scripting;
using War3Engine.Scripting.Lua;
using War3Engine.Rendering;

namespace War3Engine.Client;

public sealed class ViewerGame : Game
{
    private const string DefaultModel = @"units\human\footman\footman.mdx";

    private readonly ViewerOptions _options;
    private readonly GraphicsDeviceManager _graphics;
    private readonly OrbitCamera _camera = new();

    private AssetStore? _store;
    private RenderModel? _renderModel;
    private ModelInstance? _instance;
    private ModelRenderer? _renderer;
    private RenderStats _stats = new();
    private MapSceneData? _scene;
    private TerrainRenderer? _terrain;
    private readonly List<(RenderModel Model, ModelInstance Instance)> _mapInstances = new();
    private readonly List<RenderModel> _ownedModels = new();
    private readonly List<Texture2D> _standaloneTextures = new();
    private UiOverlay? _overlay;
    private KeyboardState _previousKeyboard;
    private readonly List<string> _mapUiLines = new();
    private readonly List<(RenderModel Model, ModelInstance Instance, UnitState Unit)> _runtimeUnits = new();
    private JassMapRunResult? _jass;
    private JassGameRuntime? _jassRuntime;
    private MapArtCache? _artCache;
    private MouseState _previousMouse;
    private bool _paused;
    private bool _finished;
    private double _statusTimer;

    public ViewerGame(ViewerOptions options)
    {
        _options = options;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = options.Width,
            PreferredBackBufferHeight = options.Height,
            SynchronizeWithVerticalRetrace = options.Screenshot is null,
            GraphicsProfile = GraphicsProfile.HiDef,
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        IsFixedTimeStep = options.Screenshot is not null;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
    }

    protected override void LoadContent()
    {
        _overlay = new UiOverlay(GraphicsDevice, Content.Load<SpriteFont>("UI"));
        _previousKeyboard = Keyboard.GetState();
        var settings = EngineSettings.Load();
        _store = new AssetStore(settings.WarcraftPath);
        if (!string.IsNullOrEmpty(_options.Map))
        {
            _store.MountMap(_options.Map);
        }

        if (!string.IsNullOrEmpty(_options.Map))
        {
            LoadMap(_store, _options.Map);
            base.LoadContent();
            return;
        }

        var modelPath = _options.Model ?? DefaultModel;
        var data = MdxModelLoader.Load(_store, modelPath);
        Console.WriteLine($"Model: {data.Name}");
        Console.WriteLine($"  source={modelPath} format=v{data.FormatVersion} geosets={data.Geosets.Length} nodes={data.Nodes.Length} materials={data.Materials.Length} textures={data.Textures.Length}");

        var effect = Content.Load<Effect>("Shaders/Model");
        _renderer = new ModelRenderer(GraphicsDevice, effect);
        _renderModel = new RenderModel(GraphicsDevice, data, LoadTexture);
        Console.WriteLine($"  triangles={_renderModel.Geosets.Sum(g => g.PrimitiveCount)} texturesResolved={data.Textures.Length - _renderModel.UnresolvedTextures.Count}/{data.Textures.Length}");
        foreach (var unresolved in _renderModel.UnresolvedTextures)
        {
            Console.WriteLine($"  UNRESOLVED TEXTURE: {unresolved}");
        }

        Console.WriteLine($"  sequences ({data.Sequences.Length}): {string.Join(", ", data.Sequences.Select(s => $"{s.Name}[{s.StartFrame}-{s.EndFrame}]"))}");

        _instance = new ModelInstance(data) { Speed = _options.Speed };
        var sequence = data.FindSequence(_options.Animation);
        if (_options.Animation is not null && sequence is null)
        {
            Console.WriteLine($"  animation '{_options.Animation}' not found; using default");
        }

        if (sequence is not null)
        {
            _instance.SequenceIndex = Array.IndexOf(data.Sequences, sequence);
        }

        SequenceData? current = _instance.Sequence;
        if (current is not null)
        {
            Console.WriteLine($"  playing: {current.Name} [{current.StartFrame}-{current.EndFrame}] time={_options.TimeMs}ms");
        }

        _instance.SequenceTimeMs = _options.TimeMs;

        // Frame the rest-pose mesh rather than sequence extents (death poses inflate the model bounds).
        var min = new System.Numerics.Vector3(float.MaxValue);
        var max = new System.Numerics.Vector3(float.MinValue);
        foreach (var geoset in data.Geosets)
        {
            foreach (var position in geoset.Positions)
            {
                min = System.Numerics.Vector3.Min(min, position);
                max = System.Numerics.Vector3.Max(max, position);
            }
        }

        if (min.X > max.X)
        {
            min = data.Bounds.Minimum;
            max = data.Bounds.Maximum;
        }

        var center = (min + max) * 0.5f;
        var radius = MathF.Max((max - min).Length() * 0.5f, 8f);
        _camera.Frame(center, radius);
        _camera.Yaw = _options.Yaw;
        _camera.Pitch = _options.Pitch;
        if (_options.Distance is { } distance)
        {
            _camera.Distance = distance;
        }

        _camera.AspectRatio = (float)GraphicsDevice.PresentationParameters.BackBufferWidth / GraphicsDevice.PresentationParameters.BackBufferHeight;
        base.LoadContent();
    }

    private void LoadMap(AssetStore store, string mapPath)
    {
        _scene = MapSceneLoader.Load(store, mapPath, _options.MaxInstances, includeUnits: !_options.RunJass);
        Console.WriteLine($"Map: {_scene.MapName}");
        Console.WriteLine($"  tileset={_scene.Tileset} terrainTiles={_scene.Terrain.TileCount} terrainTris={_scene.Terrain.Indices.Length / 3} textureGroups={_scene.Terrain.TextureGroups.Length}");
        Console.WriteLine($"  doodads={_scene.TotalDoodads} units={_scene.TotalUnits} placements={_scene.Placements.Length} unresolvedModels={_scene.UnresolvedModels}");
        foreach (var group in _scene.Terrain.TextureGroups)
        {
            Console.WriteLine($"  terrain texture: {group.TexturePath} ({group.IndexCount / 6} tiles)");
        }

        var effect = Content.Load<Effect>("Shaders/Model");
        _renderer = new ModelRenderer(GraphicsDevice, effect);
        _terrain = new TerrainRenderer(GraphicsDevice, effect, _scene.Terrain, _scene.Water, LoadTextureByPath);
        Console.WriteLine($"  terrain missing textures: {_terrain.MissingTextures} waterTris={_terrain.WaterTriangleCount}");

        var cache = new Dictionary<string, RenderModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var placement in _scene.Placements)
        {
            var cacheKey = placement.ModelPath + "|" + placement.ReplaceableTexture;
            if (!cache.TryGetValue(cacheKey, out var renderModel))
            {
                try
                {
                    var data = MdxModelLoader.Load(store, placement.ModelPath);
                    renderModel = new RenderModel(GraphicsDevice, data, texture =>
                        texture.ReplaceableId == placement.ReplaceableId && placement.ReplaceableTexture is not null
                            ? LoadTextureByPath(placement.ReplaceableTexture)
                            : LoadTexture(texture));
                    cache[cacheKey] = renderModel;
                    _ownedModels.Add(renderModel);
                    Console.WriteLine($"  model {placement.ModelPath}: geosets={data.Geosets.Length} nodes={data.Nodes.Length} unresolvedTextures={renderModel.UnresolvedTextures.Count}");
                }
                catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
                {
                    Console.WriteLine($"  model {placement.ModelPath} SKIPPED: {ex.Message}");
                    continue;
                }
            }

            var instance = new ModelInstance(renderModel.Data)
            {
                World = placement.World,
                SequenceTimeMs = _options.TimeMs,
            };
            var sequence = renderModel.Data.Sequences.FirstOrDefault(x => x.Name.StartsWith("Stand", StringComparison.OrdinalIgnoreCase))
                           ?? renderModel.Data.Sequences.FirstOrDefault();
            if (sequence is not null)
            {
                instance.SequenceIndex = Array.IndexOf(renderModel.Data.Sequences, sequence);
            }

            _mapInstances.Add((renderModel, instance));
        }

        Console.WriteLine($"  rendered instances: {_mapInstances.Count} (unique models {cache.Count})");
        if (_options.RunJass)
        {
            RunJassScene(store, mapPath, cache);
        }
        _mapUiLines.Add($"tileset {_scene.Tileset}  tiles {_scene.Terrain.TileCount}  tris {_scene.Terrain.Indices.Length / 3}");
        _mapUiLines.Add($"doodads {_scene.TotalDoodads}  units {_scene.TotalUnits}  visible {_mapInstances.Count}/{_scene.Placements.Length}");
        _mapUiLines.Add($"unique models {cache.Count}  unresolved {_scene.UnresolvedModels}  missing textures {_terrain.MissingTextures}");
        _mapUiLines.Add($"terrain textures {_scene.Terrain.TextureGroups.Length}  water tris {_terrain.WaterTriangleCount}");

        var bounds = _scene.Bounds;
        var center = _options.Target is { } target
            ? War3Engine.Core.Wc3Space.Position(target)
            : _runtimeUnits.Count > 0
                ? _camera.Target
                : bounds.Center;
        var radius = MathF.Max((bounds.Maximum - bounds.Minimum).Length() * 0.5f, 500f);
        _camera.Target = center;
        _camera.Distance = radius * 2.2f;
        _camera.Yaw = _options.Yaw;
        _camera.Pitch = _options.Pitch > 0.12f ? _options.Pitch : 0.55f;
        if (_options.Distance is { } distance)
        {
            _camera.Distance = distance;
        }
    }

    private void RunJassScene(AssetStore store, string mapPath, Dictionary<string, RenderModel> doodadCache)
    {
        var common = ReadText(store, @"Scripts\common.j");
        var blizzard = ReadText(store, @"Scripts\Blizzard.j");
        var map = ReadText(store, "war3map.j");
        var slk = JassSlkQueryProvider.Load(store, mapPath);
        var lua = new NLuaBridge(slk.Query);
        var result = JassMapHost.Run(common, blizzard, map, 5_000_000, _options.AdvanceMs, lua: lua, chatMessage: _options.Chat);
        _jass = result;
        _jassRuntime = result.Runtime;
        Console.WriteLine($"  jass: globals={result.GlobalsInitialized} main={result.MainCompleted} units={result.UnitsAlive} triggers={result.TriggerCount} timers={result.TimerCount} frames={result.FrameCount} callbacks={result.TimerCallbacks} chatFired={result.ChatFired}");
        Console.WriteLine($"  jass lua: calls={lua.History.Count} errors={lua.History.Count(h => h.ErrorKind != LuaErrorKind.None)}");
        foreach (var message in result.Runtime.DisplayMessages.Take(3))
        {
            Console.WriteLine($"  jass message: {message}");
        }

        if (result.Failure is UnsupportedNativeException unsupported)
        {
            Console.WriteLine($"  jass BLOCKED by native {unsupported.NativeName} ({unsupported.Signature})");
            return;
        }

        if (result.Failure is not null)
        {
            Console.WriteLine($"  jass FAILURE: {result.Failure.Message}");
            return;
        }

        if (_options.ClickDialog is { } clickIndex)
        {
            var dialog = result.Runtime.Dialogs.LastOrDefault(d =>
                d.Visible && !d.Destroyed && d.PlayerId == 0 && d.Buttons.Count > clickIndex);
            if (dialog is null)
            {
                Console.WriteLine("  jass dialog click: no visible dialog");
            }
            else
            {
                var fired = ViewerInput.DispatchDialogClick(result.Runtime, dialog, dialog.Buttons[clickIndex]);
                Console.WriteLine($"  jass dialog click: button='{dialog.Buttons[clickIndex].Label}' fired={fired} messages={result.Runtime.DisplayMessages.Count}");
            }
        }

        _artCache = MapArtCache.Load(store, mapPath);
        var unresolved = 0;
        var focusApplied = false;
        var models = new Dictionary<string, RenderModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var unit in result.Runtime.Units.Where(u => !u.Removed))
        {
            var art = _artCache.ResolveUnit(unit.TypeId);
            if (art is null)
            {
                unresolved++;
                continue;
            }

            if (!models.TryGetValue(art.ModelPath, out var renderModel))
            {
                try
                {
                    var data = MdxModelLoader.Load(store, art.ModelPath);
                    renderModel = new RenderModel(GraphicsDevice, data, LoadTexture);
                    models[art.ModelPath] = renderModel;
                    _ownedModels.Add(renderModel);
                }
                catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
                {
                    Console.WriteLine($"  unit model {art.ModelPath} SKIPPED: {ex.Message}");
                    unresolved++;
                    continue;
                }
            }

            var z = _artCache.SampleHeight(unit.X, unit.Y) + unit.Z;
            var world = System.Numerics.Matrix4x4.CreateScale(art.Scale)
                        * System.Numerics.Matrix4x4.CreateRotationZ(unit.Facing)
                        * System.Numerics.Matrix4x4.CreateTranslation(new System.Numerics.Vector3(unit.X, unit.Y, z));
            var instance = new ModelInstance(renderModel.Data)
            {
                World = War3Engine.Core.Wc3Space.Transform(world),
                SequenceTimeMs = _options.TimeMs,
            };
            var sequence = renderModel.Data.Sequences.FirstOrDefault(x => x.Name.StartsWith("stand", StringComparison.OrdinalIgnoreCase))
                           ?? renderModel.Data.Sequences.FirstOrDefault();
            if (sequence is not null)
            {
                instance.SequenceIndex = Array.IndexOf(renderModel.Data.Sequences, sequence);
            }

            _runtimeUnits.Add((renderModel, instance, unit));
            var focus = _options.FocusType is null
                ? _runtimeUnits.Count == 1
                : string.Equals(Rawcode(unit.TypeId), _options.FocusType, StringComparison.OrdinalIgnoreCase);
            if (_options.Target is null && focus && !focusApplied)
            {
                var center = System.Numerics.Vector3.Transform(renderModel.Data.Bounds.Center, instance.World);
                var radius = MathF.Max(renderModel.Data.Bounds.Radius * art.Scale, 16f);
                _camera.Target = center;
                _camera.Distance = _options.Distance ?? MathF.Max(radius * 2.5f, 250f);
                focusApplied = true;
            }
        }

        Console.WriteLine($"  jass runtime units rendered={_runtimeUnits.Count} unresolved={unresolved} models={models.Count}");
        if (_options.SelectUnit is { } selectedIndex && selectedIndex >= 0 && selectedIndex < _runtimeUnits.Count)
        {
            var selected = _runtimeUnits[selectedIndex].Unit;
            var fired = ViewerInput.SelectUnit(result.Runtime, selected, selected.OwnerId);
            Console.WriteLine($"  jass selection click: unit={Rawcode(selected.TypeId)} owner={selected.OwnerId} fired={fired} selected={result.Runtime.SelectedUnits(selected.OwnerId).Count}");
        }

        _mapUiLines.Add($"JASS: units {result.UnitsAlive} triggers {result.TriggerCount} timers {result.TimerCount} frames {result.FrameCount}");
        _mapUiLines.Add($"JASS callbacks {result.TimerCallbacks} lua {lua.History.Count} chatFired {result.ChatFired} messages {result.Runtime.DisplayMessages.Count}");
        _mapUiLines.Add($"JASS rendered {_runtimeUnits.Count} unresolvedArt {unresolved}");
    }

    private static string Rawcode(int value) => new(new[]
    {
        (char)(value & 0xFF),
        (char)((value >> 8) & 0xFF),
        (char)((value >> 16) & 0xFF),
        (char)((value >> 24) & 0xFF),
    });

    private static string ReadText(AssetStore store, string path)
    {
        try
        {
            return System.Text.Encoding.UTF8.GetString(store.ReadAllBytes(path, out _));
        }
        catch (FileNotFoundException)
        {
            return string.Empty;
        }
    }

    private Texture2D? LoadTextureByPath(string path)
    {
        try
        {
            var bytes = _store!.ReadAllBytes(path, out var source);
            var image = BlpDecoder.Decode(bytes);
            var texture = new Texture2D(GraphicsDevice, image.Width, image.Height, false, SurfaceFormat.Color);
            texture.SetData(image.Rgba);
            _standaloneTextures.Add(texture);
            Console.WriteLine($"  texture {path} <- {source} {image.Width}x{image.Height}");
            return texture;
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException or NotSupportedException)
        {
            Console.WriteLine($"  terrain texture {path} FAILED: {ex.Message}");
            return null;
        }
    }

    protected override void UnloadContent()
    {
        _overlay?.Dispose();
        _terrain?.Dispose();
        foreach (var model in _ownedModels)
        {
            model.Dispose();
        }

        foreach (var texture in _standaloneTextures)
        {
            texture.Dispose();
        }

        _store?.Dispose();
        base.UnloadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            Keyboard.GetState().IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        var mouse = Mouse.GetState();
        var leftClick = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        var uiClick = false;
        if (leftClick && _overlay is not null && _jassRuntime is { } dialogRuntime &&
            _overlay.TryHitDialog(mouse.Position, out var dialog, out var button))
        {
            uiClick = true;
            try
            {
                var fired = ViewerInput.DispatchDialogClick(dialogRuntime, dialog, button);
                Console.WriteLine($"dialog dispatch: fired={fired} button='{button.Label}' messages={dialogRuntime.DisplayMessages.Count}");
            }
            catch (JassRuntimeException ex)
            {
                Console.WriteLine($"dialog dispatch failed: {ex.Describe()}");
                _jassRuntime = null;
                _jass = null;
            }
        }

        if (leftClick && !uiClick && _jassRuntime is { } selectionRuntime &&
            TryPickUnit(mouse, out var picked))
        {
            uiClick = true;
            if (picked.Unit.OwnerId == 0)
            {
                try
                {
                    var fired = ViewerInput.SelectUnit(selectionRuntime, picked.Unit);
                    Console.WriteLine($"selection dispatch: unit={Rawcode(picked.Unit.TypeId)} fired={fired} selected={selectionRuntime.SelectedUnits(0).Count}");
                }
                catch (JassRuntimeException ex)
                {
                    Console.WriteLine($"selection dispatch failed: {ex.Describe()}");
                    _jassRuntime = null;
                    _jass = null;
                }
            }
        }

        if (!uiClick && (mouse.RightButton == ButtonState.Pressed || mouse.LeftButton == ButtonState.Pressed))
        {
            _camera.Orbit(mouse.X - _previousMouse.X, mouse.Y - _previousMouse.Y);
        }

        var scroll = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (scroll != 0)
        {
            _camera.Zoom(scroll / 120f);
        }

        _previousMouse = mouse;

        var keyboard = Keyboard.GetState();
        if (keyboard.IsKeyDown(Keys.Space) && !_previousKeyboard.IsKeyDown(Keys.Space))
        {
            _paused = !_paused;
        }

        if (keyboard.IsKeyDown(Keys.F5) && !_previousKeyboard.IsKeyDown(Keys.F5) && _jassRuntime is { } chatRuntime)
        {
            try
            {
                var fired = ViewerInput.DispatchChatHotkey(chatRuntime, _options.Chat);
                Console.WriteLine($"chat dispatch: fired={fired} messages={chatRuntime.DisplayMessages.Count}");
                if (_jass is { } jassResult && jassResult.Vm.TryGetGlobalValue("udg_guaibs", out var wave))
                {
                    Console.WriteLine($"  udg_guaibs={wave}");
                }
            }
            catch (JassRuntimeException ex)
            {
                Console.WriteLine($"chat dispatch failed: {ex.Describe()}");
                _jassRuntime = null;
                _jass = null;
            }
        }

        if (keyboard.IsKeyDown(Keys.OemOpenBrackets) && !_previousKeyboard.IsKeyDown(Keys.OemOpenBrackets))
        {
            CycleAnimation(-1);
        }

        if (keyboard.IsKeyDown(Keys.OemCloseBrackets) && !_previousKeyboard.IsKeyDown(Keys.OemCloseBrackets))
        {
            CycleAnimation(1);
        }

        _previousKeyboard = keyboard;

        if (_overlay is not null)
        {
            var selection = _overlay.PendingAnimationSelection;
            _overlay.ClearSelection();
            if (selection is { } index && _instance is { } selected)
            {
                selected.SequenceIndex = index;
                selected.SequenceTimeMs = 0;
            }
        }

        if (!_paused && _options.Screenshot is null)
        {
            _instance?.Advance(gameTime.ElapsedGameTime.TotalSeconds);
            foreach (var (_, instance) in _mapInstances)
            {
                instance.Advance(gameTime.ElapsedGameTime.TotalSeconds);
            }

            if (_jassRuntime is { } jass)
            {
                try
                {
                    jass.Advance(gameTime.ElapsedGameTime.TotalSeconds * 1000.0);
                    jass.RunDueTimers();
                    SyncRuntimeUnits();
                }
                catch (JassRuntimeException ex)
                {
                    Console.WriteLine($"jass update stopped: {ex.Describe()}");
                    _jassRuntime = null;
                    _jass = null;
                }
            }
        }

        _statusTimer += gameTime.ElapsedGameTime.TotalSeconds;
        if (_statusTimer >= 1.0 && _options.Screenshot is null)
        {
            _statusTimer = 0;
            if (_scene is not null)
            {
                Console.WriteLine($"[map] {_mapInstances.Count} instances, {_stats.DrawCalls} draws, {_stats.Triangles} tris");
            }
            else if (_instance is { } instance)
            {
                var sequence = instance.Sequence;
                var text = sequence is null ? "-" : $"{sequence.Name} {instance.SequenceFrame:F0}ms";
                Console.WriteLine($"[{_stats.DrawCalls} draws, {_stats.Triangles} tris] {text}");
            }
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_options.Screenshot is not null)
        {
            if (!_finished)
            {
                SaveScreenshot();
                _finished = true;
            }

            Exit();
            return;
        }

        GraphicsDevice.Clear(new Color(28, 32, 40));
        RenderScene(GraphicsDevice, null, GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight);
        DrawUi(Mouse.GetState());
        base.Draw(gameTime);
    }

    private void SaveScreenshot()
    {
        var path = Path.GetFullPath(_options.Screenshot!);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var target = new RenderTarget2D(GraphicsDevice, _options.Width, _options.Height, false, SurfaceFormat.Color, DepthFormat.Depth24);
        _camera.AspectRatio = (float)_options.Width / _options.Height;
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(new Color(28, 32, 40));
        RenderScene(GraphicsDevice, target, _options.Width, _options.Height);
        DrawUi(default);
        GraphicsDevice.SetRenderTarget(null);

        using (var stream = File.Create(path))
        {
            target.SaveAsPng(stream, _options.Width, _options.Height);
        }

        Console.WriteLine($"  screenshot: {path} ({_options.Width}x{_options.Height}) draws={_stats.DrawCalls} triangles={_stats.Triangles}");
    }

    private void SyncRuntimeUnits()
    {
        if (_artCache is null)
        {
            return;
        }

        foreach (var (_, instance, unit) in _runtimeUnits)
        {
            var z = _artCache.SampleHeight(unit.X, unit.Y) + unit.Z;
            var world = System.Numerics.Matrix4x4.CreateScale(1f)
                        * System.Numerics.Matrix4x4.CreateRotationZ(unit.Facing)
                        * System.Numerics.Matrix4x4.CreateTranslation(new System.Numerics.Vector3(unit.X, unit.Y, z));
            instance.World = War3Engine.Core.Wc3Space.Transform(world);
            instance.Advance(1.0 / 60.0);
        }
    }

    private bool TryPickUnit(MouseState mouse, out (RenderModel Model, ModelInstance Instance, UnitState Unit) hit)
    {
        hit = default;
        var best = 45f;
        var viewport = GraphicsDevice.Viewport;
        foreach (var entry in _runtimeUnits)
        {
            var projected = viewport.Project(entry.Instance.World.Translation, _camera.Projection, _camera.View, Matrix.Identity);
            if (projected.Z < 0f || projected.Z > 1f)
            {
                continue;
            }

            var dx = projected.X - mouse.X;
            var dy = projected.Y - mouse.Y;
            var distance = MathF.Sqrt((dx * dx) + (dy * dy));
            if (distance >= best)
            {
                continue;
            }

            best = distance;
            hit = entry;
        }

        return best < 45f;
    }

    private void DrawSelectionMarkers()
    {
        if (_overlay is null || _jassRuntime is not { } runtime)
        {
            return;
        }

        var viewport = GraphicsDevice.Viewport;
        foreach (var (player, units) in runtime.Selection)
        {
            foreach (var unit in units)
            {
                foreach (var (_, instance, candidate) in _runtimeUnits)
                {
                    if (!ReferenceEquals(candidate, unit))
                    {
                        continue;
                    }

                    var projected = viewport.Project(instance.World.Translation, _camera.Projection, _camera.View, Matrix.Identity);
                    if (projected.Z is >= 0f and <= 1f)
                    {
                        _overlay.DrawSelection(new Point((int)projected.X, (int)projected.Y), $"{Rawcode(unit.TypeId)} P{player}");
                    }
                }
            }
        }
    }

    private void CycleAnimation(int direction)
    {
        if (_instance is null || _instance.Model.Sequences.Length == 0)
        {
            return;
        }

        var count = _instance.Model.Sequences.Length;
        _instance.SequenceIndex = ((_instance.SequenceIndex + direction) % count + count) % count;
        _instance.SequenceTimeMs = 0;
    }

    private void DrawUi(MouseState mouse)
    {
        if (_overlay is null)
        {
            return;
        }

        if (_scene is not null)
        {
            var lines = new List<string>(_mapUiLines);
            if (_jassRuntime is { } runtime)
            {
                foreach (var (player, units) in runtime.Selection)
                {
                    foreach (var unit in units)
                    {
                        lines.Add($"selected P{player} {Rawcode(unit.TypeId)} hp {unit.Hp:F0}/{unit.MaxHp:F0} @{unit.X:F0},{unit.Y:F0}");
                    }
                }

                if (runtime.DisplayMessages.Count > 0)
                {
                    lines.Add(string.Empty);
                    foreach (var message in runtime.DisplayMessages.TakeLast(4))
                    {
                        lines.Add("> " + (message.Length > 96 ? message[..96] : message));
                    }
                }
            }

            _overlay.DrawMap($"Map: {_scene.MapName}", lines);
            _overlay.DrawDialog(_jassRuntime, mouse, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            DrawSelectionMarkers();
            return;
        }

        if (_instance is not { } instance)
        {
            return;
        }

        _overlay.DrawModel(
            new UiOverlay.ModelInfo
            {
                Title = instance.Model.Name,
                Subtitle = $"{instance.Model.Geosets.Length} geosets  {instance.Model.Nodes.Length} nodes  {instance.Model.Sequences.Length} sequences",
                Animations = instance.Model.Sequences.Select(s => s.Name).ToArray(),
                SelectedAnimation = instance.SequenceIndex,
                TimeMs = instance.SequenceFrame,
                Paused = _paused,
                DrawCalls = _stats.DrawCalls,
                Triangles = _stats.Triangles,
            },
            mouse);
    }

    private void RenderScene(GraphicsDevice device, RenderTarget2D? target, int width, int height)
    {
        if (_renderer is null)
        {
            return;
        }

        _stats = new RenderStats();
        var view = _camera.View;
        var projection = _camera.Projection;

        if (_scene is not null)
        {
            _terrain?.Draw(view, projection);
            foreach (var (model, instance) in _mapInstances)
            {
                _renderer.Draw(model, instance, view, projection, _stats);
            }

            foreach (var (model, instance, _) in _runtimeUnits)
            {
                _renderer.Draw(model, instance, view, projection, _stats);
            }

            return;
        }

        if (_renderModel is null || _instance is null)
        {
            return;
        }

        _renderer.Draw(_renderModel, _instance, view, projection, _stats, _options.Geosets);
    }

    private Texture2D? LoadTexture(TextureData texture)
    {
        var path = ResolveTexturePath(texture);
        if (path is null)
        {
            return null;
        }

        try
        {
            var bytes = _store!.ReadAllBytes(path, out var source);
            var image = BlpDecoder.Decode(bytes);
            var result = new Texture2D(GraphicsDevice, image.Width, image.Height, false, SurfaceFormat.Color);
            result.SetData(image.Rgba);
            Console.WriteLine($"  texture {path} <- {source} {image.Width}x{image.Height}");
            return result;
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException or NotSupportedException)
        {
            Console.WriteLine($"  texture {path} FAILED: {ex.Message}");
            return null;
        }
    }

    private static string? ResolveTexturePath(TextureData texture)
    {
        if (texture.ReplaceableId == 1)
        {
            return @"ReplaceableTextures\TeamColor\TeamColor00.blp";
        }

        if (texture.ReplaceableId == 2)
        {
            return @"ReplaceableTextures\TeamGlow\TeamGlow00.blp";
        }

        return string.IsNullOrWhiteSpace(texture.Path) ? null : texture.Path;
    }
}
