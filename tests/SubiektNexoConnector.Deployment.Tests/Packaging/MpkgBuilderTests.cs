using System.IO.Compression;
using System.IO;
using System.Xml.Linq;
using SubiektNexoConnector.Deployment.Packaging;

namespace SubiektNexoConnector.Deployment.Tests.Packaging;

public sealed class MpkgBuilderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"SubiektNexoConnector-{Guid.NewGuid():N}");

    [Fact]
    public void Build_CreatesDynamicManifestAndZipPayload()
    {
        var publish = CreatePublishDirectory();
        Directory.CreateDirectory(Path.Combine(publish, "data"));
        File.WriteAllText(Path.Combine(publish, "data", "sample.txt"), "payload");
        var output = Path.Combine(_root, "out", "package.mpkg");

        var result = new MpkgBuilder().Build(new MpkgBuildRequest(
            publish, output, "firma-01", new Version(1, 2, 3, 4), "60,*"));

        Assert.Equal("SubiektNexoConnector.v2.firma_h01-1.2.3.4", result.PackageIdentity);
        Assert.Equal("SubiektNexoConnector.firma-01", result.LaunchScenarioName);
        Assert.Equal(
            "SubiektNexoConnector.firma-01-1.2.3.4.mpkg",
            DeploymentNames.GetMpkgFileName("firma-01", new Version(1, 2, 3, 4)));
        using var archive = ZipFile.OpenRead(output);
        Assert.NotNull(archive.GetEntry(MpkgBuilder.AdapterExecutableName));
        Assert.Null(archive.GetEntry("data/sample.txt"));
        var manifestEntry = Assert.Single(archive.Entries, entry => entry.FullName == MpkgBuilder.ManifestEntryName);
        using var reader = new StreamReader(manifestEntry.Open());
        var manifest = XDocument.Parse(reader.ReadToEnd());
        var package = Assert.IsType<XElement>(manifest.Root);
        Assert.Equal(result.PackageIdentity, package.Attribute("Identity")?.Value);
        Assert.Equal("SubiektNexoConnector.firma-01", package.Attribute("DisplayName")?.Value);
        Assert.Equal("1.2.3.4", package.Attribute("VersionDisplayName")?.Value);
        Assert.Equal("60,*", package.Attribute("RequiredProductVersion")?.Value);
        Assert.Empty(package.Element("Dependencies")!.Elements());
        var scenario = package.Element("LaunchScenarios")!.Element("LaunchScenario")!;
        Assert.Equal(result.LaunchScenarioName, scenario.Attribute("Name")?.Value);
        var action = scenario.Element("LaunchAction")!;
        Assert.Equal("/UruchomionePrzezInsLauncher --instance firma-01", action.Attribute("CommandLineArguments")?.Value);
        Assert.All(package.Descendants("File"), file =>
        {
            Assert.NotNull(file.Attribute("Size"));
            Assert.NotNull(file.Attribute("Time"));
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("firma 01")]
    [InlineData("firma/01")]
    [InlineData("zażółć")]
    public void Build_RejectsInvalidInstanceName(string instanceName)
    {
        var publish = CreatePublishDirectory();
        var output = Path.Combine(_root, "package.mpkg");

        var action = () => new MpkgBuilder().Build(new MpkgBuildRequest(
            publish, output, instanceName, new Version(1, 0, 0, 0), "60,*"));

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void Build_RejectsOutputInsidePublishDirectory()
    {
        var publish = CreatePublishDirectory();

        var action = () => new MpkgBuilder().Build(new MpkgBuildRequest(
            publish, Path.Combine(publish, "package.mpkg"), "pilot-01", new Version(1, 0, 0, 0), "60,*"));

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void AdapterPayloadArchive_ContainsOnlySelectedRuntimeFiles()
    {
        var publish = CreatePublishDirectory();
        Directory.CreateDirectory(Path.Combine(publish, "data"));
        File.WriteAllText(Path.Combine(publish, "data", "sample.txt"), "must not be distributed");
        var output = Path.Combine(_root, "payload", "adapter-payload.zip");

        var fileCount = new AdapterPayloadArchiveBuilder().Build(publish, output);

        using var archive = ZipFile.OpenRead(output);
        Assert.Equal(fileCount, archive.Entries.Count);
        Assert.NotNull(archive.GetEntry(MpkgBuilder.AdapterExecutableName));
        Assert.NotNull(archive.GetEntry("LICENSE"));
        Assert.Null(archive.GetEntry("data/sample.txt"));
    }

    [Fact]
    public void EmbeddedPayloadExtraction_RejectsPathTraversal()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("../outside.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("unsafe");
        }
        stream.Position = 0;

        var action = () => EmbeddedAdapterPayload.ExtractArchive(stream, Path.Combine(_root, "extract"));

        Assert.Throws<InvalidOperationException>(action);
    }

    private string CreatePublishDirectory()
    {
        var publish = Path.Combine(_root, $"publish-{Guid.NewGuid():N}");
        Directory.CreateDirectory(publish);
        File.WriteAllText(Path.Combine(publish, MpkgBuilder.AdapterExecutableName), "test executable");
        File.WriteAllText(Path.Combine(publish, "SubiektNexoConnector.Api.dll"), "test library");
        File.WriteAllText(Path.Combine(publish, "LICENSE"), "test license");
        File.WriteAllText(Path.Combine(publish, "SubiektNexoConnector.Api.runtimeconfig.json"), "{}");
        File.WriteAllText(Path.Combine(publish, "SubiektNexoConnector.Api.deps.json"), """
            {
              "runtimeTarget": { "name": ".NETCoreApp,Version=v8.0/win-x64" },
              "targets": {
                ".NETCoreApp,Version=v8.0/win-x64": {
                  "SubiektNexoConnector.Api/1.0.0": {
                    "runtime": {
                      "SubiektNexoConnector.Api.dll": {}
                    }
                  }
                }
              },
              "libraries": {
                "SubiektNexoConnector.Api/1.0.0": {
                  "type": "project"
                }
              }
            }
            """);
        return publish;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
