using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SubiektNexoConnector.Deployment;
using SubiektNexoConnector.Deployment.Lifecycle;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 4 && args[0] == "--update-contract")
        {
            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(args[2], "SubiektNexoConnector.Api.dll"));
            var version = Version.Parse(versionInfo.FileVersion!);
            var package = new SubiektNexoConnector.Deployment.Packaging.MpkgBuilder().Build(
                new(args[2], args[3], "Pilot", version, "60,*", ExistingPackageIdentity: "Pilot-1.0.0.2"));
            var worker = new SubiektNexoConnector.Deployment.Nexo.NexoWorker(args[1]);
            try
            {
                var info = worker.Invoke(d => d.GetPackageInfo(package.OutputPath)).GetAwaiter().GetResult();
                if (info.Package == "ERROR" || !info.Scenarios.Contains("SubiektNexoConnector.Pilot"))
                    throw new InvalidDataException("InsERT rejected the update manifest.");
                Console.WriteLine($"Update contract OK: {package.PackageIdentity}; {info.Package}; scenarios: {info.Scenarios.Length}; binary: {versionInfo.ProductVersion}");
            }
            finally { worker.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            return;
        }
        if (args.Length == 3 && args[0] == "--nexo-contract")
        {
            var worker = new SubiektNexoConnector.Deployment.Nexo.NexoWorker(args[1]);
            try
            {
                var info = worker.Invoke(d => d.GetPackageInfo(args[2])).GetAwaiter().GetResult();
                Console.WriteLine($"Package contract OK: {info.Package}; scenarios: {info.Scenarios.Length}");
            }
            finally { worker.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            return;
        }
        if (args.Length != 1) throw new ArgumentException("Supply the output directory.");
        Directory.CreateDirectory(args[0]);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow(preview: true);
        ((ListBox)window.FindName("InstancesList")).ItemsSource = new[] { new DeploymentState { Instance = "magazyn-warszawa", Database = "Firma", Port = 15001, Status = OperationStatus.Ready } };
        ((ListBox)window.FindName("InstancesList")).SelectedIndex = 0;
        ((TextBlock)window.FindName("EmptyInstancesText")).Visibility = Visibility.Collapsed;
        ((Button)window.FindName("HomeRepairButton")).IsEnabled = true;
        ((Button)window.FindName("HomeRemoveButton")).IsEnabled = true;
        ((TextBlock)window.FindName("DatabaseText")).Text = "SERWER\\INSERTNEXO → Firma\nWersja adaptera: 1.0.0.0";
        ((TextBlock)window.FindName("AccountText")).Text = "FIRMA\\adapter";
        ((TextBlock)window.FindName("SummaryText")).Text = "Instancja: magazyn-warszawa\nPodmiot: Firma\nSerwer: SERWER\\INSERTNEXO\nKonto: FIRMA\\adapter\nPort: 15001\nDostęp: 192.168.100.20 (private,domain)\nSeq: pominięty — logi lokalne\nSkrót: tak\nTest po restarcie: pominięty";
        ((TextBlock)window.FindName("StepsText")).Text = "✓ Konfiguracja i chroniony klucz API\n✓ Pakiet i podmiot Nexo\n✓ Ograniczenie dostępu sieciowego\n✓ Autostart bez zalogowanego użytkownika\n✗ Health, klucz API i Sfera\n— Seq (pominięty)";
        ((TextBlock)window.FindName("ResultText")).Text = "Pakiet zainstalowany. Test Sfery nie powiódł się. Sprawdź licencję podmiotu i ponów sam test.";
        var pageMethod = typeof(MainWindow).GetMethod("ShowPage", BindingFlags.Instance | BindingFlags.NonPublic)!;
        for (var page = 0; page < 6; page++)
        {
            pageMethod.Invoke(window, [page]);
            var visual = (FrameworkElement)window.Content;
            visual.Measure(new Size(1240, 740)); visual.Arrange(new Rect(0, 0, 1240, 740)); visual.UpdateLayout();
            var bitmap = new RenderTargetBitmap(1280, 780, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(args[0], $"page-{page}.png")); encoder.Save(stream);
        }
        window.Close(); app.Shutdown();
        Console.WriteLine("Rendered six wizard pages without starting an installation.");
    }
}
