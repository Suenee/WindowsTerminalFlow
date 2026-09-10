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
        Title = $"WTF 1.00 — {root}";
        var ws = WorkspaceService.Load(root, forceLoadAll);
        foreach (var folder in ws.Folders.Where(x => x.Enabled && Directory.Exists(Path.Combine(root, x.Name))))
            AddTab(folder.Name);
    }

    private string BuildCommand(string path)
    {
        var shell = SettingsService.Settings.Shell.ToLowerInvariant();
        var escapedPowerShellPath = path.Replace("'", "''");
        return shell switch
        {
            "powershell.exe" => $"powershell.exe -NoExit -Command \"Set-Location -LiteralPath '{escapedPowerShellPath}'\"",
            "pwsh.exe" => $"pwsh.exe -NoExit -Command \"Set-Location -LiteralPath '{escapedPowerShellPath}'\"",
            _ => $"cmd.exe /D /K cd /d \"{path}\""
        };
    }

    private void AddTab(string name)
    {
        var path = Path.Combine(_root, name);
        var terminal = new EasyTerminalControl
        {
            StartupCommandLine = BuildCommand(path),
            FontSizeWhenSettingTheme = SettingsService.Settings.FontSize
        };

        var tab = new TabItem { Content = terminal };
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new TextBlock { Text = name, Margin = new Thickness(6, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        var close = new Button { Content = "×", Width = 24, Height = 22, Padding = new Thickness(0), Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0), ToolTip = "Close" };
        close.Click += (_, _) =>
        {
            WorkspaceService.SetEnabled(_root, name, false);
            Tabs.Items.Remove(tab);
            Logger.Info($"Tab closed: {name}");
            if (Tabs.Items.Count == 0) Close();
        };
        header.Children.Add(close);
        tab.Header = header;
        Tabs.Items.Add(tab);
        if (Tabs.SelectedItem == null) Tabs.SelectedItem = tab;
    }
}
