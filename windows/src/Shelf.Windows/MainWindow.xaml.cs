using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Shelf.Windows.Models;
using Shelf.Windows.Services;

namespace Shelf.Windows;

public partial class MainWindow : Window
{
    public ObservableCollection<ShelfItem> Items { get; } = new();
    private readonly IDragDataProvider dragDataProvider = new FileDragDataProvider();
    private Point dragStart;
    private ShelfItem? dragCandidate;
    private bool dragging;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        // Stage 1: primary monitor work area, in WPF device-independent units.
        var area = SystemParameters.WorkArea;
        Height = Math.Min(Height, area.Height);
        Left = area.Left;
        Top = area.Top + Math.Max(0, (area.Height - Height) / 2);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = !dragging && e.Data.GetDataPresent(DataFormats.FileDrop, false)
            && (e.AllowedEffects & DragDropEffects.Copy) != 0
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        e.Effects = DragDropEffects.None;
        if (dragging || (e.AllowedEffects & DragDropEffects.Copy) == 0
            || e.Data.GetData(DataFormats.FileDrop, false) is not string[] paths)
            return;

        int added = 0, skipped = 0;
        foreach (string path in paths)
        {
            try
            {
                if (!Path.IsPathFullyQualified(path) || !File.Exists(path))
                {
                    skipped++;
                    continue;
                }
                string fullPath = Path.GetFullPath(path);
                if (Items.Any(item => string.Equals(item.FilePath, fullPath, StringComparison.OrdinalIgnoreCase)))
                {
                    skipped++;
                    continue;
                }
                Items.Add(new ShelfItem(fullPath));
                added++;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
            {
                skipped++;
            }
        }
        if (added > 0) e.Effects = DragDropEffects.Copy;
        Status.Text = $"{added}件を登録、{skipped}件をスキップ（重複・フォルダー・利用できないパス）。";
    }

    private void FileList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        dragStart = e.GetPosition(FileList);
        var container = ItemsControl.ContainerFromElement(FileList, e.OriginalSource as DependencyObject);
        dragCandidate = (container as ListBoxItem)?.DataContext as ShelfItem;
    }

    private void FileList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (dragging || e.LeftButton != MouseButtonState.Pressed || dragCandidate is null) return;
        Point current = e.GetPosition(FileList);
        if (Math.Abs(current.X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        ShelfItem item = dragCandidate;
        dragCandidate = null;
        if (!File.Exists(item.FilePath))
        {
            Status.Text = "元ファイルが見つかりません。項目は棚に残しています。";
            return;
        }
        try
        {
            dragging = true;
            // Never advertise Move, and never remove the item after the operation.
            DragDrop.DoDragDrop(FileList, dragDataProvider.CreateData(item), DragDropEffects.Copy);
            Status.Text = "項目は棚に残っています。取り出しはコピー操作です。";
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            Status.Text = "ドラッグを開始できませんでした。もう一度お試しください。";
        }
        finally { dragging = false; }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (FileList.SelectedItem is not ShelfItem item) return;
        Items.Remove(item);
        Status.Text = "棚の一覧から取り除きました。元ファイルは変更していません。";
    }
}
