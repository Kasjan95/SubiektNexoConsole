public static class NexoConnectionModeResolver
{
    public static bool UseConfig(string[] args)
    {
        return args.Any(a => a.Equals("--config", StringComparison.OrdinalIgnoreCase));
    }
}