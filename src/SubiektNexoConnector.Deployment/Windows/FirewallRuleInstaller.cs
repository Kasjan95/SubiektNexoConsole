using System.Net;
using System.Runtime.InteropServices;
using SubiektNexoConnector.Deployment.Packaging;
namespace SubiektNexoConnector.Deployment.Windows;

public sealed class FirewallRuleInstaller
{
    public const string RemoteAddresses = "LocalSubnet";
    private const string Group = "SubiektNexoConnector";
    public FirewallRuleResult Install(string instanceName, int port, string addresses = RemoteAddresses, string profiles = "private,domain")
    {
        Validate(addresses, profiles);
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        var name = GetRuleName(instanceName);
        dynamic policy = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2")!)!;
        dynamic rules = policy.Rules;
        dynamic? rule = null;
        try
        {
            try { rule = rules.Item(name); } catch (Exception e) when (AdapterScheduledTaskInstaller.IsNotFound(e)) { }
            if (rule is not null && !string.Equals((string)rule.Grouping, Group, StringComparison.Ordinal))
                throw new InvalidOperationException("Reguła o tej nazwie nie ma potwierdzonego właściciela. Rozwiąż konflikt w Zaporze Windows.");
            if (rule is not null)
            {
                string oldPort = rule.LocalPorts, oldAddresses = rule.RemoteAddresses;
                int oldProfiles = rule.Profiles;
                try { rule.LocalPorts = port.ToString(); rule.RemoteAddresses = addresses; rule.Profiles = ProfileMask(profiles); rule.Enabled = true; }
                catch { rule.LocalPorts = oldPort; rule.RemoteAddresses = oldAddresses; rule.Profiles = oldProfiles; throw; }
            }
            else
            {
                rule = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule")!)!;
                rule.Name = name; rule.Grouping = Group; rule.Protocol = 6; rule.LocalPorts = port.ToString();
                rule.RemoteAddresses = addresses; rule.Direction = 1; rule.Action = 1;
                rule.Profiles = ProfileMask(profiles); rule.Enabled = true;
                rules.Add(rule);
            }
            return new FirewallRuleResult(name, port, addresses);
        }
        finally { Release(rule); Release(rules); Release(policy); }
    }
    public bool Matches(string instanceName, int port, string addresses, string profiles)
    {
        dynamic policy = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2")!)!;
        dynamic rules = policy.Rules;
        dynamic? rule = null;
        try
        {
            rule = rules.Item(GetRuleName(instanceName));
            return rule.Grouping == Group && rule.Enabled && rule.Protocol == 6 && rule.Direction == 1
                && rule.Action == 1 && rule.LocalPorts == port.ToString() && rule.Profiles == ProfileMask(profiles)
                && NormalizeAddresses((string)rule.RemoteAddresses) == NormalizeAddresses(addresses);
        }
        catch (Exception e) when (AdapterScheduledTaskInstaller.IsNotFound(e)) { return false; }
        finally { Release(rule); Release(rules); Release(policy); }
    }
    public void Remove(string instanceName)
    {
        dynamic policy = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2")!)!;
        dynamic rules = policy.Rules;
        dynamic? rule = null;
        try
        {
            rule = rules.Item(GetRuleName(instanceName));
            if (rule.Grouping != Group) throw new InvalidOperationException("Nie usunięto reguły bez potwierdzonego właściciela.");
            rules.Remove(GetRuleName(instanceName));
        }
        catch (Exception e) when (AdapterScheduledTaskInstaller.IsNotFound(e)) { }
        finally { Release(rule); Release(rules); Release(policy); }
    }
    public void AdoptPilotRule(string instance, int port)
    {
        dynamic policy = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2")!)!;
        dynamic rules = policy.Rules; dynamic? rule = null;
        try
        {
            rule = rules.Item(GetRuleName(instance));
            if (rule.Grouping == Group) return;
            if (!string.IsNullOrEmpty((string)rule.Grouping) || rule.Protocol != 6 || rule.LocalPorts != port.ToString()
                || rule.Direction != 1 || rule.Action != 1 || !string.Equals((string)rule.RemoteAddresses, "LocalSubnet", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Reguła nie pasuje do starego pilota. Nie przejęto jej.");
            rule.Grouping = Group;
        }
        catch (Exception e) when (AdapterScheduledTaskInstaller.IsNotFound(e)) { }
        finally { Release(rule); Release(rules); Release(policy); }
    }
    public static string NormalizeAddresses(string addresses) => string.Join(",", addresses.Split(',').Select(value =>
    {
        var parts = value.Trim().Split('/');
        if (!IPAddress.TryParse(parts[0], out var ip)) return value.Trim().ToLowerInvariant();
        var bytes = ip.GetAddressBytes(); var prefix = bytes.Length * 8;
        if (parts.Length == 2)
        {
            if (!int.TryParse(parts[1], out prefix))
            {
                var mask = IPAddress.Parse(parts[1]).GetAddressBytes();
                prefix = mask.Sum(b => System.Numerics.BitOperations.PopCount((uint)b));
            }
        }
        for (var index = 0; index < bytes.Length; index++) bytes[index] &= (byte)(255 << (8 - Math.Clamp(prefix - index * 8, 0, 8)));
        return $"{new IPAddress(bytes)}/{prefix}";
    }).OrderBy(s => s, StringComparer.Ordinal));

    public static void Validate(string addresses, string profiles)
    {
        if (string.IsNullOrWhiteSpace(addresses)) throw new InvalidOperationException("Podaj IP/CIDR backendu albo LocalSubnet.");
        foreach (var entry in addresses.Split(','))
        {
            var parts = entry.Trim().Split('/');
            if (parts.Length == 1 && parts[0].Equals("LocalSubnet", StringComparison.OrdinalIgnoreCase)) continue;
            if (parts.Length > 2 || !IPAddress.TryParse(parts[0], out var ip)
                || (parts.Length == 2 && (!int.TryParse(parts[1], out var prefix) || prefix < 0 || prefix > (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128))))
                throw new InvalidOperationException("Niepoprawny adres backendu. Użyj IP, CIDR lub LocalSubnet; oddziel adresy przecinkiem.");
        }
        _ = ProfileMask(profiles);
    }
    public static int ProfileMask(string profiles)
    {
        var value = 0;
        foreach (var profile in profiles.Split(',')) value |= profile.Trim().ToLowerInvariant() switch
        { "domain" => 1, "private" => 2, "public" => 4, _ => throw new InvalidOperationException("Profile: domain, private, public.") };
        return value;
    }
    public static string GetRuleName(string instanceName)
    {
        DeploymentNames.ValidateInstanceName(instanceName);
        return $"SubiektNexoConnector - {instanceName}";
    }
    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
}
