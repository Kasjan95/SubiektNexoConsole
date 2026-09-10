namespace SubiektNexoConnector.Deployment.Nexo;

public sealed record SqlConnectionParameters(bool WindowsAuthentication, string User, string Password)
{
    public static SqlConnectionParameters Create(bool windowsAuthentication, string? user, string? password)
    {
        if (windowsAuthentication)
            return new(true, "", "");

        var normalizedUser = user?.Trim() ?? "";
        if (normalizedUser.Length == 0)
            throw new InvalidOperationException("Podaj login SQL. Hasło może pozostać puste.");

        return new(false, normalizedUser, password ?? "");
    }
}
