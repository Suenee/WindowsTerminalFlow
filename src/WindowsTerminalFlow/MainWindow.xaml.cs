using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EasyWindowsTerminalControl;
using WindowsTerminalFlow.Services;

namespace WindowsTerminalFlow;

public partial class MainWindow : Window
{
    private readonly string _root;

    public MainWindow(string root, bool forceLoadAll)
    {
        InitializeComponent();
        _root = root;
        Title = $"WTF {App.Version} — {root}";
        Logger.Info($"Loading workspace: {root}; forceLoadAll={forceLoadAll}");
        var ws = WorkspaceService.Load(root, forceLoadAll);
        foreach (var folder in ws.Folders.Where(x => x.Enabled && Directory.Exists(Path.Combine(root, x.Name))))
            AddTab(folder.Name);
        Logger.Info($"Workspace opened with {Tabs.Items.Count} tab(s).");
    }

    private string BuildCommand(string path)
    {
        var shell = SettingsService.Settings.Shell.ToLowerInvariant();
        var escapedPowerShellPath = path.Replace("'", "''");
        return shell switch
        {
            "powershell.exe" => $"powershell.exe -NoExit -Command \"Set-Location -LiteralPath '{escapedPowerShellPath}'\"",
            "pwsh.exe" => $"pwsh.exe -NoExit -Command \"Set-Location -LiteralPath '{escapedPowerShellPath}'\"",
            _ when path.StartsWith("\\\\", StringComparison.Ordinal) => $"cmd.exe /D /K pushd \"{path}\"",
            _ => $"cmd.exe /D /K cd /d \"{path}\""
        };
    }

    private void AddTab(string name)
    {
        var path = Path.Combine(_root, name);
        var command = BuildCommand(path);
        Logger.Info($"Opening tab: {name}; path={path}; shell={SettingsService.Settings.Shell}; command={command}");
        var terminal = new EasyTerminalControl
        {
            StartupCommandLine = command,
            FontSizeWhenSettingTheme = SettingsService.Settings.FontSize
        };

        var tab = new TabItem { Content = terminal };
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new TextBlock { Text = name, Margin = new Thickness(6, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        var close = new Button
        {
            Content = "×",
            Width = 24,
            Height = 22,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            ToolTip = LocalizationService.Get("close_tab")
        };
        close.Click += (_, _) =>
        {
            WorkspaceService.SetEnabled(_root, name, false);
            Tabs.Items.Remove(tab);
            Logger.Info($"Tab closed and disabled: {name}");
            if (Tabs.Items.Count == 0) Close();
        };
        header.Children.Add(close);
        tab.Header = header;
        Tabs.Items.Add(tab);
        if (Tabs.SelectedItem == null) Tabs.SelectedItem = tab;
    }
}
