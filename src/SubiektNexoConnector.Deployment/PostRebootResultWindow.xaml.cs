using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using SubiektNexoConnector.Deployment.Testing;

namespace SubiektNexoConnector.Deployment;

public partial class PostRebootResultWindow : Window
{
    private readonly PostRebootResultPresentation _result;

    public PostRebootResultWindow(PostRebootResultPresentation result)
    {
        InitializeComponent();
        _result = result;
        HeadingText.Text = result.Heading;
        StatusText.Text = result.Status;
        SummaryText.Text = result.Summary;
        VerifiedAtText.Text = result.VerifiedAt;
        WindowsStartedAtText.Text = result.WindowsStartedAt;
        RecommendationText.Text = result.Recommendation;
        DetailsBox.Text = result.Details;
        ReportPathText.Text = $"Raport: {result.ReportPath}";

        var accent = new SolidColorBrush((Color)ColorConverter.ConvertFromString(result.IsSuccess ? "#23804A" : "#B45309"));
        var background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(result.IsSuccess ? "#EAF7EF" : "#FFF4E5"));
        StatusBadge.Background = accent;
        StatusBorder.Background = background;
    }

    private void CopyDetails_Click(object sender, RoutedEventArgs e) =>
        Clipboard.SetText($"{_result.Heading}{Environment.NewLine}{_result.Summary}{Environment.NewLine}{Environment.NewLine}{_result.Details}{Environment.NewLine}Raport: {_result.ReportPath}");

    private void OpenReportFolder_Click(object sender, RoutedEventArgs e)
    {
        var directory = Path.GetDirectoryName(_result.ReportPath);
        if (directory is not null && Directory.Exists(directory))
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
