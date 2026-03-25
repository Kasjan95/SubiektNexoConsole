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
                throw new ConfigurationException("Brak SqlServer.");

            if (string.IsNullOrWhiteSpace(Database.DatabaseName))
                throw new ConfigurationException("Brak DatabaseName.");

            if (Database.UseSqlAuth)
            {
                if (string.IsNullOrWhiteSpace(Database.SqlUser))
                    throw new ConfigurationException("UseSqlAuth=true, ale brak SqlUser.");

                if (string.IsNullOrWhiteSpace(Database.SqlPassword))
                    throw new ConfigurationException("UseSqlAuth=true, ale brak SqlPassword.");
            }

            if (string.IsNullOrWhiteSpace(SystemLogin.NexoUser))
                throw new ConfigurationException("Brak NexoUser.");

            if (string.IsNullOrWhiteSpace(SystemLogin.NexoPassword))
                throw new ConfigurationException("Brak NexoPassword.");
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
            : base($"Błędne dane w pliku {configFile}. Uzupełnij braki: {message}")
        {
        }
    }
}
