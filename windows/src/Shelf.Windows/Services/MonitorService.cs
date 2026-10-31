using System.ComponentModel;
using System.Runtime.InteropServices;
using Shelf.Core;

namespace Shelf.Windows.Services;

public sealed record MonitorInfo(nint Handle, string DeviceName, PixelRect Bounds, PixelRect WorkArea, bool Primary)
{
    // DISPLAYn is the Windows device identifier, not our enumeration order.
    public string Label => $"{DeviceName.Replace(@"\\.\", "")} {(Primary ? "（メイン）" : "")} | {Bounds.Width}×{Bounds.Height} px | 左上 ({Bounds.Left}, {Bounds.Top})";
}

public static class MonitorService
{
    public static IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();
        MonitorEnum callback = (nint handle, nint dc, ref NativeRect rect, nint data) =>
        {
            var info = new NativeMonitorInfo { Size = Marshal.SizeOf<NativeMonitorInfo>(), DeviceName = "" };
            if (!GetMonitorInfo(handle, ref info)) return false;
            PixelRect workArea = info.WorkArea.ToPixels();
            if (workArea.Width > 0 && workArea.Height > 0)
                monitors.Add(new(handle, info.DeviceName, info.Bounds.ToPixels(), workArea, (info.Flags & 1) != 0));
            return true;
        };
        if (!EnumDisplayMonitors(0, 0, callback, 0) || monitors.Count == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "接続モニターを取得できません。");
        return monitors.OrderByDescending(m => m.Primary).ThenBy(m => m.DeviceName, StringComparer.Ordinal).ToArray();
    }

    public static void Place(nint window, MonitorInfo monitor, bool collapsed)
    {
        // Move onto the destination first. GetDpiForWindow then returns its actual DPI;
        // WPF also receives WM_DPICHANGED and updates its own device transform.
        if (MonitorFromWindow(window, 2) != monitor.Handle)
            SetPosition(window, monitor.WorkArea.Left, monitor.WorkArea.Top, 0, 0, 0x0001 | 0x0010);
        uint dpi = GetDpiForWindow(window);
        PixelRect frame = ShelfPlacement.Calculate(monitor.WorkArea, dpi == 0 ? 96u : dpi, collapsed);
        SetPosition(window, frame.Left, frame.Top, frame.Width, frame.Height, 0x0010);
    }

    private static void SetPosition(nint window, int x, int y, int width, int height, uint flags)
    {
        if (!SetWindowPos(window, new nint(-1), x, y, width, height, flags))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "棚を画面端に配置できません。");
    }

    private delegate bool MonitorEnum(nint monitor, nint dc, ref NativeRect rect, nint data);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
        public readonly PixelRect ToPixels() => new(Left, Top, Right, Bottom);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeMonitorInfo
    {
        public int Size;
        public NativeRect Bounds, WorkArea;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorEnum callback, nint data);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref NativeMonitorInfo info);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
}
