namespace SubiektNexoConnector.Deployment.Testing;

public sealed record AdapterSmokeTestResult(Uri HealthUri, Uri WarehousesUri, int WarehouseCount);
