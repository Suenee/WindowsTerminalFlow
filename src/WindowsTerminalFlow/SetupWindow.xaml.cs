using System.Windows;
using System.Windows.Controls;
using WindowsTerminalFlow.Services;

namespace WindowsTerminalFlow;

public partial class SetupWindow : Window
{
    public SetupWindow()
    {
        InitializeComponent();
        VersionText.Text = $"WindowsTerminalFlow {App.Version}";
        SelectByTag(LanguageBox, SettingsService.Settings.Language);
        SelectByContent(LoggingBox, SettingsService.Settings.LoggingMode);
        SelectByContent(ShellBox, SettingsService.Settings.Shell);
        NewFoldersBox.IsChecked = SettingsService.Settings.NewFoldersEnabled;
        ApplyLanguage(SettingsService.Settings.Language);
        Logger.Info("Setup window opened.");
    }

    private static void SelectByTag(ComboBox box, string value) => box.SelectedItem = box.Items.Cast<ComboBoxItem>().FirstOrDefault(x => Equals(x.Tag, value)) ?? box.Items[0];
    private static void SelectByContent(ComboBox box, string value) => box.SelectedItem = box.Items.Cast<ComboBoxItem>().FirstOrDefault(x => string.Equals(x.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase)) ?? box.Items[0];

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || LanguageBox.SelectedItem is not ComboBoxItem item) return;
        ApplyLanguage(item.Tag?.ToString() ?? "cs");
    }

    private void ApplyLanguage(string language)
    {
        Title = LocalizationService.Get("setup_title", language);
        LanguageLabel.Text = LocalizationService.Get("language", language);
        LoggingLabel.Text = LocalizationService.Get("logging", language);
        ShellLabel.Text = LocalizationService.Get("shell", language);
        NewFoldersBox.Content = LocalizationService.Get("new_folders", language);
        AdminStatus.Text = ElevatedLauncher.IsAdministrator()
            ? LocalizationService.Get("elevated_active", language)
            : LocalizationService.Get("elevated_inactive", language);
        RepairLauncherButton.Content = LocalizationService.Get("repair_launcher", language);
        SaveButton.Content = LocalizationService.Get("save", language);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SettingsService.Settings.Language = ((ComboBoxItem)LanguageBox.SelectedItem).Tag?.ToString() ?? "cs";
        SettingsService.Settings.LoggingMode = ((ComboBoxItem)LoggingBox.SelectedItem).Content?.ToString() ?? "single";
        SettingsService.Settings.Shell = ((ComboBoxItem)ShellBox.SelectedItem).Content?.ToString() ?? "cmd.exe";
        SettingsService.Settings.NewFoldersEnabled = NewFoldersBox.IsChecked == true;
        SettingsService.Save();
        Logger.Info($"Settings saved; language={SettingsService.Settings.Language}; logging={SettingsService.Settings.LoggingMode}; shell={SettingsService.Settings.Shell}; newFolders={SettingsService.Settings.NewFoldersEnabled}");
        Close();
    }

    private void RepairLauncher_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ElevatedLauncher.RegisterTask();
            Logger.Info("Elevated launcher repaired from setup.");
            var language = ((ComboBoxItem)LanguageBox.SelectedItem).Tag?.ToString() ?? SettingsService.Settings.Language;
            MessageBox.Show(LocalizationService.Get("launcher_registered", language), "WindowsTerminalFlow", MessageBoxButton.OK, MessageBoxImage.Information);
            ApplyLanguage(language);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Elevated launcher repair failed");
            throw;
        }
    }
}
