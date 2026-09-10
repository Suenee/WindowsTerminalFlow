using System.Windows;
using System.Windows.Controls;
using WindowsTerminalFlow.Services;

namespace WindowsTerminalFlow;

public partial class SetupWindow : Window
{
    public SetupWindow()
    {
        InitializeComponent();
        SelectByTag(LanguageBox, SettingsService.Settings.Language);
        SelectByContent(LoggingBox, SettingsService.Settings.LoggingMode);
        SelectByContent(ShellBox, SettingsService.Settings.Shell);
        NewFoldersBox.IsChecked = SettingsService.Settings.NewFoldersEnabled;
        AdminStatus.Text = ElevatedLauncher.IsAdministrator() ? "Elevated: active" : "Elevated: inactive";
    }

    private static void SelectByTag(ComboBox box, string value) => box.SelectedItem = box.Items.Cast<ComboBoxItem>().FirstOrDefault(x => Equals(x.Tag, value)) ?? box.Items[0];
    private static void SelectByContent(ComboBox box, string value) => box.SelectedItem = box.Items.Cast<ComboBoxItem>().FirstOrDefault(x => string.Equals(x.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase)) ?? box.Items[0];

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SettingsService.Settings.Language = ((ComboBoxItem)LanguageBox.SelectedItem).Tag?.ToString() ?? "cs";
        SettingsService.Settings.LoggingMode = ((ComboBoxItem)LoggingBox.SelectedItem).Content?.ToString() ?? "single";
        SettingsService.Settings.Shell = ((ComboBoxItem)ShellBox.SelectedItem).Content?.ToString() ?? "cmd.exe";
        SettingsService.Settings.NewFoldersEnabled = NewFoldersBox.IsChecked == true;
        SettingsService.Save();
        Close();
    }

    private void RepairLauncher_Click(object sender, RoutedEventArgs e)
    {
        ElevatedLauncher.RegisterTask();
        MessageBox.Show("Elevated launcher registered.", "WindowsTerminalFlow", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
