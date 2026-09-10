using System.IO;

namespace SubiektNexoConnector.Deployment.Nexo;

internal sealed class EmbeddedPackageDeployer
{
    internal const string ResourceName = "SubiektNexoConnector.Deployment.Resources.package-deployer.dll";

    public byte[] ReadAllBytes()
    {
        using var stream = typeof(EmbeddedPackageDeployer).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                "Instalator nie zawiera komponentu instalacji pakietów Nexo. " +
                "Opublikuj go skryptem scripts/Publish-Deployment.ps1.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
