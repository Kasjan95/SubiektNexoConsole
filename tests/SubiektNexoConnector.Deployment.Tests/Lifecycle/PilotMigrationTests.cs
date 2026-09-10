using System.IO.Compression;
using System.Net;
using System.Xml.Linq;
using SubiektNexoConnector.Deployment.Lifecycle;
using SubiektNexoConnector.Deployment.Testing;

namespace SubiektNexoConnector.Deployment.Tests.Lifecycle;

public sealed class PilotMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "connector-pilot-tests-" + Guid.NewGuid().ToString("N"));

    private static XElement Manifest() => new("Package", new XAttribute("Identity", "Pilot-1.0.0.2"),
        new XElement("LaunchScenarios", new XElement("LaunchScenario", new XAttribute("Name", "SubiektNexoConnector.Pilot"),
            new XElement("LaunchAction", new XAttribute("ExePath", "SubiektNexoConnector.Api.exe"),
                new XAttribute("CommandLineArguments", "/UruchomionePrzezInsLauncher --instance Pilot"),
                new XAttribute("RedirectedInput", "!LauncherMessageFormatB!"),
                new XAttribute("RedirectedInputEncoding", "utf-8")))));

    private string Package(XElement? manifest)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".mpkg");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        if (manifest is not null)
        {
            using var writer = new StreamWriter(archive.CreateEntry("manifest.xml").Open());
            writer.Write(manifest.ToString());
        }
        return path;
    }

    [Fact]
    public void InspectionPreservesLegacyIdentityVersionAndInstanceCase()
    {
        var result = PilotImporter.Inspect(Package(Manifest()));
        Assert.Equal("Pilot", result.Instance);
        Assert.Equal("Pilot-1.0.0.2", result.Identity);
        Assert.Equal("1.0.0.2", result.Version);
    }

    [Fact]
    public void MissingManifestIsRejected() => Assert.Throws<InvalidDataException>(() => PilotImporter.Inspect(Package(null)));

    [Theory]
    [InlineData("ExePath", "other.exe")]
    [InlineData("CommandLineArguments", "/UruchomionePrzezInsLauncher")]
    [InlineData("CommandLineArguments", "/UruchomionePrzezInsLauncher --instance Other")]
    public void AmbiguousOrDifferentAdapterActionIsRejected(string attribute, string value)
    {
        var manifest = Manifest();
        manifest.Descendants("LaunchAction").Single().SetAttributeValue(attribute, value);
        Assert.Throws<InvalidDataException>(() => PilotImporter.Inspect(Package(manifest)));
    }

    [Fact]
    public void MultipleScenariosAreRejected()
    {
        var manifest = Manifest();
        manifest.Element("LaunchScenarios")!.Add(new XElement(manifest.Descendants("LaunchScenario").Single()));
        Assert.Throws<InvalidOperationException>(() => PilotImporter.Inspect(Package(manifest)));
    }

    // Characterization tests: these document defects, not a desired security contract.
    // Replace these assertions with rejection assertions when the importer is fixed.
    [Fact]
    [Trait("Category", "KnownMigrationDefect")]
    public void CurrentlyAcceptsASecondUncheckedLaunchAction()
    {
        var manifest = Manifest();
        manifest.Descendants("LaunchScenario").Single().Add(new XElement("LaunchAction", new XAttribute("ExePath", "other.exe")));
        Assert.Equal("Pilot", PilotImporter.Inspect(Package(manifest)).Instance);
    }

    [Fact]
    [Trait("Category", "KnownMigrationDefect")]
    public void CurrentlyAcceptsMissingLauncherInputProtocol()
    {
        var manifest = Manifest();
        var action = manifest.Descendants("LaunchAction").Single();
        action.Attribute("RedirectedInput")!.Remove();
        action.Attribute("RedirectedInputEncoding")!.Remove();
        Assert.Equal("Pilot", PilotImporter.Inspect(Package(manifest)).Instance);
    }

    [Fact]
    [Trait("Category", "KnownMigrationDefect")]
    public void MissingActionCurrentlyThrowsNullReferenceInsteadOfValidationError()
    {
        var manifest = Manifest();
        manifest.Descendants("LaunchAction").Single().Remove();
        Assert.Throws<NullReferenceException>(() => PilotImporter.Inspect(Package(manifest)));
    }

    [Fact]
    public async Task Legacy404PreventsShutdownEvenWhenHealthAndWarehousesPass()
    {
        var paths = new List<string>();
        using var http = new HttpClient(new Handler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            paths.Add(path);
            if (path != "/health") Assert.Equal("TEST-KEY", request.Headers.GetValues("X-Api-Key").Single());
            Assert.Equal(HttpMethod.Get, request.Method); // No unauthenticated / blind stop fallback.
            return new HttpResponseMessage(path == "/maintenance/info" ? HttpStatusCode.NotFound : HttpStatusCode.OK)
                { Content = new StringContent("[]") };
        }));
        await new AdapterSmokeTester(http).RunAsync(15001, "TEST-KEY", TimeSpan.FromSeconds(2), TimeSpan.Zero);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AdapterControl(http).StopAsync(15001, "TEST-KEY", "Pilot", default));
        Assert.Contains("Starszy pilot", error.Message);
        Assert.Contains("/health", paths);
        Assert.Contains("/maintenance/info", paths);
        Assert.DoesNotContain("/maintenance/stop", paths);
    }

    [Fact]
    public async Task ImportedDatabaseCannotBeChangedByRepair()
    {
        await using var service = new DeploymentService();
        service.UseState(new DeploymentState { Instance = "Pilot", Database = "Original" });
        Assert.Throws<InvalidOperationException>(() => service.SelectDatabase("Other"));
        Assert.Equal("Original", service.State!.Database);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
