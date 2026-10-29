namespace War3Engine.Client;

public sealed class ViewerOptions
{
    public string? Model { get; private set; }

    public string? Map { get; private set; }

    public string? Animation { get; private set; }

    public float TimeMs { get; private set; }

    public string? Screenshot { get; private set; }

    public int Width { get; private set; } = 1280;

    public int Height { get; private set; } = 720;

    public float Speed { get; private set; } = 1f;

    public float Yaw { get; private set; } = 1.35f;

    public float Pitch { get; private set; } = 0.12f;

    public float? Distance { get; private set; }

    public HashSet<int>? Geosets { get; private set; }

    public System.Numerics.Vector3? Target { get; private set; }

    public int MaxInstances { get; private set; } = 2000;

    public static ViewerOptions Parse(string[] args)
    {
        var options = new ViewerOptions();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--model":
                    options.Model = Next(args, ref i);
                    break;
                case "--map":
                    options.Map = Next(args, ref i);
                    break;
                case "--anim":
                    options.Animation = Next(args, ref i);
                    break;
                case "--time":
                    options.TimeMs = float.Parse(Next(args, ref i), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--screenshot":
                    options.Screenshot = Next(args, ref i);
                    break;
                case "--size":
                    var size = Next(args, ref i).Split('x');
                    options.Width = int.Parse(size[0]);
                    options.Height = int.Parse(size[1]);
                    break;
                case "--speed":
                    options.Speed = float.Parse(Next(args, ref i), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--yaw":
                    options.Yaw = float.Parse(Next(args, ref i), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--pitch":
                    options.Pitch = float.Parse(Next(args, ref i), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--distance":
                    options.Distance = float.Parse(Next(args, ref i), System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "--geosets":
                    options.Geosets = Next(args, ref i).Split(',').Select(int.Parse).ToHashSet();
                    break;
                case "--instances":
                    options.MaxInstances = int.Parse(Next(args, ref i));
                    break;
                case "--target":
                    var target = Next(args, ref i).Split(',');
                    options.Target = new System.Numerics.Vector3(
                        float.Parse(target[0], System.Globalization.CultureInfo.InvariantCulture),
                        float.Parse(target[1], System.Globalization.CultureInfo.InvariantCulture),
                        float.Parse(target[2], System.Globalization.CultureInfo.InvariantCulture));
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }

        return options;
    }

    private static string Next(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value after {args[index]}");
        }

        return args[++index];
    }
}
