using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsTerminalFlow.Models;
using WindowsTerminalFlow.Services;

namespace WindowsTerminalFlow;

public partial class WorkspaceConfigWindow : Window
{
    private readonly string _root;
    private readonly ObservableCollection<WorkspaceFolder> _items;
    private WorkspaceFolder? _dragged;
    private Point _dragStart;

    public WorkspaceConfigWindow(string root)
    {
        InitializeComponent();
        _root = root;
        RootText.Text = root;
        _items = new ObservableCollection<WorkspaceFolder>(WorkspaceService.Load(root).Folders.Select(x => new WorkspaceFolder { Name = x.Name, Enabled = x.Enabled }));
        FolderList.ItemsSource = _items;
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        var en = SettingsService.Settings.Language.Equals("en", StringComparison.OrdinalIgnoreCase);
        Title = en ? "WTF Workspace configuration" : "WTF Konfigurace workspace";
        SaveButton.Content = en ? "Save" : "Uložit";
        CancelButton.Content = en ? "Cancel" : "Zrušit";
    }

    private static ListBoxItem? FindItem(DependencyObject? source)
    {
        while (source != null && source is not ListBoxItem) source = VisualTreeHelper.GetParent(source);
        return source as ListBoxItem;
    }

    private void FolderList_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(FolderList);
        _dragged = FindItem(e.OriginalSource as DependencyObject)?.DataContext as WorkspaceFolder;
    }

    private void FolderList_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragged == null) return;
        var pos = e.GetPosition(FolderList);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        DragDrop.DoDragDrop(FolderList, _dragged, DragDropEffects.Move);
    }

    private void FolderList_Drop(object sender, DragEventArgs e)
    {
        if (_dragged == null) return;
        var targetItem = FindItem(FolderList.InputHitTest(e.GetPosition(FolderList)) as DependencyObject);
        if (targetItem?.DataContext is not WorkspaceFolder targetFolder) return;
        var oldIndex = _items.IndexOf(_dragged);
        var newIndex = _items.IndexOf(targetFolder);
        if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex) _items.Move(oldIndex, newIndex);
        _dragged = null;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        WorkspaceService.Save(_root, new WorkspaceDefinition { Folders = _items.ToList() });
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
