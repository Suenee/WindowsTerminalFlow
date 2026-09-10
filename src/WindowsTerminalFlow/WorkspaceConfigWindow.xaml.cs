using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using WindowsTerminalFlow.Models;
using WindowsTerminalFlow.Services;

namespace WindowsTerminalFlow;

public partial class WorkspaceConfigWindow : Window
{
    private readonly string _root;
    private readonly ObservableCollection<WorkspaceFolder> _items;
    private WorkspaceFolder? _dragged;

    public WorkspaceConfigWindow(string root)
    {
        InitializeComponent();
        _root = root;
        RootText.Text = root;
        _items = new ObservableCollection<WorkspaceFolder>(WorkspaceService.Load(root).Folders.Select(x => new WorkspaceFolder { Name = x.Name, Enabled = x.Enabled }));
        FolderList.ItemsSource = _items;
    }

    private void FolderList_MouseDown(object sender, MouseButtonEventArgs e) => _dragged = FolderList.SelectedItem as WorkspaceFolder;

    private void FolderList_Drop(object sender, DragEventArgs e)
    {
        if (_dragged == null) return;
        var target = FolderList.InputHitTest(e.GetPosition(FolderList)) as DependencyObject;
        while (target != null && target is not System.Windows.Controls.ListBoxItem) target = System.Windows.Media.VisualTreeHelper.GetParent(target);
        if (target is not System.Windows.Controls.ListBoxItem item || item.DataContext is not WorkspaceFolder targetFolder) return;
        var oldIndex = _items.IndexOf(_dragged);
        var newIndex = _items.IndexOf(targetFolder);
        if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex) _items.Move(oldIndex, newIndex);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.LeftButton == MouseButtonState.Pressed && _dragged != null) DragDrop.DoDragDrop(FolderList, _dragged, DragDropEffects.Move);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        WorkspaceService.Save(_root, new WorkspaceDefinition { Folders = _items.ToList() });
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
