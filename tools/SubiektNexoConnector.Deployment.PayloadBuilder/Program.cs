using SubiektNexoConnector.Deployment.Packaging;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: PayloadBuilder <adapter-publish-directory> <output-zip>");
    return 2;
}

try
{
    var fileCount = new AdapterPayloadArchiveBuilder().Build(args[0], args[1]);
    Console.WriteLine($"Adapter payload: {fileCount} files -> {Path.GetFullPath(args[1])}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
