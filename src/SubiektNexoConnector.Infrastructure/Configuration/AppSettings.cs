using System;

namespace SubiektNexoConnector.Infrastructure.Configuration
{
    public class AppConfig
    {
        public DatabaseOptions Database { get; set; } = new();
        public SystemLoginOptions SystemLogin { get; set; } = new();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Database.SqlServer))
                throw new ConfigurationException("Missing SqlServer.");

            if (string.IsNullOrWhiteSpace(Database.DatabaseName))
                throw new ConfigurationException("Missing DatabaseName.");

            if (Database.UseSqlAuth)
            {
                if (string.IsNullOrWhiteSpace(Database.SqlUser))
                    throw new ConfigurationException("UseSqlAuth=true, but SqlUser is missing.");

                if (string.IsNullOrWhiteSpace(Database.SqlPassword))
                    throw new ConfigurationException("UseSqlAuth=true, but SqlPassword is missing.");
            }

            if (string.IsNullOrWhiteSpace(SystemLogin.NexoUser))
                throw new ConfigurationException("Missing NexoUser.");

            if (string.IsNullOrWhiteSpace(SystemLogin.NexoPassword))
                throw new ConfigurationException("Missing NexoPassword.");
        }
    }
    public class DatabaseOptions
    {
        public string SqlServer { get; set; } = "";
        public string DatabaseName { get; set; } = "";
        public bool UseSqlAuth { get; set; }
        public string SqlUser { get; set; } = "";
        public string SqlPassword { get; set; } = "";
    }
    public class SystemLoginOptions
    {
        public string NexoUser { get; set; } = "";
        public string NexoPassword { get; set; } = "";
    }
    public class ConfigurationException : Exception
    {
        public ConfigurationException(string message, string configFile = "appsettings.json")
            : base($"Invalid data in {configFile}. Fix the missing values: {message}")
        {
        }
    }
}
