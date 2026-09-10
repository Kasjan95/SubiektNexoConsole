using System.Security.Cryptography;

namespace SubiektNexoConnector.Deployment.Configuration;

public static class ApiKeyGenerator
{
    public static string Generate() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
