using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Interop;
using System.Windows.Threading;
using Shelf.Core;
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

    private readonly JsonStore<ShelfItemsState> itemsStore;
    private readonly JsonStore<ShelfSettings> settingsStore;
    private readonly Dictionary<string, string> storageWarnings = new();
    private ShelfSettings settings = new();
    private IReadOnlyList<MonitorInfo> monitors = [];
    private HwndSource? windowSource;
    private bool updatingPicker, placementPending, refreshRequested, closed;
    private bool itemsUnsaved, settingsUnsaved;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shelf");
        itemsStore = new(Path.Combine(directory, "items.json"), () => new(), ShelfItemsState.IsValid);
        settingsStore = new(Path.Combine(directory, "settings.json"), () => new(), ShelfSettings.IsValid);
        var loadedItems = itemsStore.Load();
        foreach (string path in loadedItems.Value.Paths.Distinct(StringComparer.OrdinalIgnoreCase))
            Items.Add(new ShelfItem(path));
        var loadedSettings = settingsStore.Load();
        settings = loadedSettings.Value;
        if (loadedItems.Warning is not null) storageWarnings["items"] = loadedItems.Warning;
        if (loadedSettings.Warning is not null) storageWarnings["settings"] = loadedSettings.Warning;
        ApplyCollapsedContent();
        UpdateWarnings();
        Loaded += (_, _) => QueuePlacement(refresh: false);
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        windowSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        windowSource.AddHook(WindowMessage);
        RefreshMonitors();
    }

    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        // Let WPF process WM_DPICHANGED before applying our final pixel rectangle.
        // Also handle topology, taskbar/work area, and user-requested window moves.
        if (message is 0x007E or 0x001A || message == 0x0218 && wParam is 0x0007 or 0x0012)
            QueuePlacement(refresh: true);
        else if (message is 0x02E0 or 0x0232) QueuePlacement(refresh: false);
        return 0;
    }

    private void QueuePlacement(bool refresh)
    {
        if (closed) return;
        refreshRequested |= refresh;
        if (placementPending) return;
        placementPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            placementPending = false;
            if (closed) return;
            bool refreshNow = refreshRequested;
            refreshRequested = false;
            if (refreshNow) RefreshMonitors();
            else PlaceSelectedMonitor();
        }));
    }

    private void RefreshMonitors()
    {
        try
        {
            monitors = MonitorService.GetMonitors();
            MonitorInfo selected = monitors.FirstOrDefault(m => m.DeviceName == settings.MonitorId)
                ?? monitors.FirstOrDefault(m => m.Primary) ?? monitors[0];
            bool fallback = settings.MonitorId is not null && selected.DeviceName != settings.MonitorId;
            updatingPicker = true;
            try
            {
                MonitorPicker.ItemsSource = monitors;
                MonitorPicker.SelectedItem = selected;
            }
            finally { updatingPicker = false; }
            if (settings.MonitorId != selected.DeviceName)
            {
                settings = settings with { MonitorId = selected.DeviceName };
                SaveSettings();
            }
            if (fallback) Status.Text = "配置先が見つからないため、利用できるメイン画面へ戻しました。";
            UpdateContextMenus();
            PlaceSelectedMonitor();
        }
        catch (Win32Exception ex) { Status.Text = ex.Message; }
    }

    private void PlaceSelectedMonitor()
    {
        MonitorInfo? selected = monitors.FirstOrDefault(m => m.DeviceName == settings.MonitorId);
        if (selected is null || windowSource is null) return;
        try { MonitorService.Place(windowSource.Handle, selected, settings.IsCollapsed); }
        catch (Win32Exception ex) { Status.Text = ex.Message; }
    }

    private void MonitorPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!updatingPicker && MonitorPicker.SelectedItem is MonitorInfo monitor) SelectMonitor(monitor);
    }

    private void SelectMonitor(MonitorInfo monitor)
    {
        settings = settings with { MonitorId = monitor.DeviceName };
        updatingPicker = true;
        try { MonitorPicker.SelectedItem = monitor; }
        finally { updatingPicker = false; }
        SaveSettings();
        UpdateContextMenus();
        PlaceSelectedMonitor();
    }

    private void SetCollapsed(bool collapsed)
    {
        settings = settings with { IsCollapsed = collapsed };
        ApplyCollapsedContent();
        SaveSettings();
        UpdateContextMenus();
        PlaceSelectedMonitor();
    }

    private void ApplyCollapsedContent()
    {
        ExpandedContent.Visibility = settings.IsCollapsed ? Visibility.Collapsed : Visibility.Visible;
        CollapsedStrip.Visibility = settings.IsCollapsed ? Visibility.Visible : Visibility.Collapsed;
        Width = settings.IsCollapsed ? 36 : 320;
        Height = settings.IsCollapsed ? 128 : 500;
        if (!settings.IsCollapsed) FileList.Items.Refresh();
        UpdateCount();
    }

    private void UpdateContextMenus()
    {
        ContextMenu BuildMenu()
        {
            var menu = new ContextMenu();
            var toggle = new MenuItem { Header = settings.IsCollapsed ? "展開" : "折り畳む" };
            toggle.Click += (_, _) => SetCollapsed(!settings.IsCollapsed);
            menu.Items.Add(toggle);
            var screens = new MenuItem { Header = "配置先モニター（解像度 / 左上座標）" };
            foreach (MonitorInfo monitor in monitors)
            {
                var item = new MenuItem { Header = monitor.Label, IsCheckable = true,
                    IsChecked = monitor.DeviceName == settings.MonitorId };
                item.Click += (_, _) => SelectMonitor(monitor);
                screens.Items.Add(item);
            }
            menu.Items.Add(screens);
            menu.Items.Add(new Separator());
            var exit = new MenuItem { Header = "Shelfを終了" };
            exit.Click += (_, _) => Close();
            menu.Items.Add(exit);
            return menu;
        }
        ContextMenu = BuildMenu();
        CollapsedStrip.ContextMenu = BuildMenu();
        UpdateCount();
    }

    private void UpdateCount()
    {
        CollapsedCount.Text = Items.Count.ToString();
        CollapsedStrip.ToolTip = $"{Items.Count}件 / 左クリックで展開 / 右クリックでモニター選択・終了\n"
            + (monitors.FirstOrDefault(m => m.DeviceName == settings.MonitorId)?.Label ?? "")
            + (storageWarnings.Count > 0 ? "\n" + string.Join("\n", storageWarnings.Values) : "");
    }

    private void UpdateWarnings()
    {
        PersistenceWarning.Text = string.Join("\n", storageWarnings.Values);
        CollapsedWarning.Visibility = storageWarnings.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateCount();
    }

    private void SaveItems()
    {
        try
        {
            itemsStore.Save(new ShelfItemsState { Paths = Items.Select(item => item.FilePath).ToList() });
            storageWarnings.Remove("items");
            itemsUnsaved = false;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            storageWarnings["items"] = "一覧を保存できません。現在の一覧はメモリ上に残しています。" + ex.Message;
            itemsUnsaved = true;
        }
        UpdateWarnings();
    }

    private void SaveSettings()
    {
        try
        {
            settingsStore.Save(settings);
            storageWarnings.Remove("settings");
            settingsUnsaved = false;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            storageWarnings["settings"] = "設定を保存できません。" + ex.Message;
            settingsUnsaved = true;
        }
        UpdateWarnings();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (itemsUnsaved) SaveItems();
        if (settingsUnsaved) SaveSettings();
        if ((itemsUnsaved || settingsUnsaved) && MessageBox.Show(this,
            "保存できていない変更があります。終了するとその変更は失われます。終了しますか？",
            "Shelf", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }
        closed = true;
        windowSource?.RemoveHook(WindowMessage);
    }

    private void Collapse_Click(object sender, RoutedEventArgs e) => SetCollapsed(true);
    private void Expand_Click(object sender, RoutedEventArgs e) => SetCollapsed(false);
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

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
        if (added > 0)
        {
            e.Effects = DragDropEffects.Copy;
            SaveItems();
        }
        UpdateCount();
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
            FileList.Items.Refresh();
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
        SaveItems();
        UpdateCount();
        Status.Text = "棚の一覧から取り除きました。元ファイルは変更していません。";
    }
}
