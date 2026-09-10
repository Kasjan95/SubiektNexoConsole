namespace SubiektNexoConnector.Deployment.Windows;

public sealed record FirewallRuleResult(string RuleName, int Port, string RemoteAddresses);
