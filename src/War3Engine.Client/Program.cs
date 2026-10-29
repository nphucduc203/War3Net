using War3Engine.Client;

try
{
    var options = ViewerOptions.Parse(args);
    using var game = new ViewerGame(options);
    game.Run();
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    Console.Error.WriteLine(ex);
    return 1;
}
