using System.Windows;
using System.Windows.Input;
using SubiektNexoConnector.Deployment.Nexo;

namespace SubiektNexoConnector.Deployment;

public partial class NexoDatabaseSelectionWindow : Window
{
    public NexoDatabaseSelectionWindow(IReadOnlyList<NexoDatabaseInfo> databases, string preferredName)
    {
        InitializeComponent();
        DataContext = databases;
        DatabaseListBox.SelectedItem = databases.FirstOrDefault(database =>
            database.CanDeploy && database.Name.Contains(preferredName, StringComparison.OrdinalIgnoreCase))
            ?? databases.FirstOrDefault(database => database.CanDeploy);
    }

    public NexoDatabaseInfo? SelectedDatabase => DatabaseListBox.SelectedItem as NexoDatabaseInfo;

    private void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDatabase?.CanDeploy != true)
        {
            MessageBox.Show(this, "Wybierz dostępny podmiot Nexo.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }

    private void DatabaseListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedDatabase?.CanDeploy == true)
            DialogResult = true;
    }
}
