using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;

namespace LwWinApp.Utils;

/// <summary>
/// 窗口相关的底层操作。
/// 一部分走 WinUI 的 AppWindow（尺寸、位置），一部分必须调 Win32
/// （暗色标题栏、圆角、最小尺寸限制——WinUI 没有开放这几个能力）。
/// 所有方法都做了容错，失败只会静默跳过，不会让应用崩掉。
/// </summary>
public static class WindowHelper
{
    /// <summary>取窗口句柄。文件选择器、对话框定位都要用。</summary>
    public static IntPtr GetHandle(Window window)
    {
        try
        {
            return WindowNative.GetWindowHandle(window);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    /// <summary>按逻辑像素设置窗口尺寸（内部会按当前 DPI 换算成物理像素）。</summary>
    public static void SetSize(Window window, int logicalWidth, int logicalHeight)
    {
        try
        {
            var scale = GetScale(window);
            window.AppWindow.Resize(new SizeInt32(
                (int)Math.Round(logicalWidth * scale),
                (int)Math.Round(logicalHeight * scale)));
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            // 窗口还没创建完时调用会失败，忽略即可。
        }
    }

    /// <summary>把窗口移到屏幕工作区正中。</summary>
    public static void CenterOnScreen(Window window)
    {
        try
        {
            var appWindow = window.AppWindow;
            var display = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Nearest);
            var work = display.WorkArea;
            var size = appWindow.Size;

            appWindow.Move(new PointInt32(
                work.X + Math.Max(0, (work.Width - size.Width) / 2),
                work.Y + Math.Max(0, (work.Height - size.Height) / 2)));
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
        }
    }

    /// <summary>按逻辑像素设置尺寸并居中，启动时用。</summary>
    public static void SetSizeAndCenter(Window window, int logicalWidth, int logicalHeight)
    {
        SetSize(window, logicalWidth, logicalHeight);
        CenterOnScreen(window);
    }

    /// <summary>
    /// 设置标题栏亮暗。WinUI 不会自动跟随应用主题，
    /// 切到深色主题时必须手动调一下，否则标题栏还是白的，很割裂。
    /// </summary>
    public static void SetDarkTitleBar(Window window, bool isDark)
    {
        var hwnd = GetHandle(window);
        if (hwnd == IntPtr.Zero) return;

        try
        {
            var value = isDark ? 1 : 0;
            // 20 是 Win10 20H1+ 的属性号，19 是老版本号，两个都试一遍。
            if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeLegacy, ref value, sizeof(int));
        }
        catch (DllNotFoundException)
        {
        }
    }

    /// <summary>给窗口加 Win11 圆角。</summary>
    public static void SetRoundedCorners(Window window)
    {
        var hwnd = GetHandle(window);
        if (hwnd == IntPtr.Zero) return;

        try
        {
            // DWMWCP_ROUND = 2
            var preference = 2;
            DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref preference, sizeof(int));
        }
        catch (DllNotFoundException)
        {
        }
    }

    /// <summary>
    /// 设置窗口图标 —— 标题栏左上角、任务栏按钮、Alt+Tab 用的都是它。
    ///
    /// 刻意不用 <c>AppWindow.SetIcon</c>：在无打包（unpackaged）模式下它会正常返回、
    /// 不抛异常，但窗口图标纹丝不动，从日志上完全看不出来。
    /// 这里直接发 WM_SETICON，那是 Windows 自己的机制，跟有没有包标识无关。
    /// </summary>
    public static void SetIcon(Window window, string iconPath)
    {
        var hwnd = GetHandle(window);
        if (hwnd == IntPtr.Zero || !File.Exists(iconPath)) return;

        try
        {
            // 系统按两个尺寸分别取用：大图标给任务栏和 Alt+Tab，小图标给标题栏。
            // 只给一张的话另一处会拿它硬缩，糊。
            var dpi = GetDpiForWindow(hwnd);
            if (dpi == 0) dpi = 96;

            var big = LoadImage(IntPtr.Zero, iconPath, ImageIcon,
                GetSystemMetricsForDpi(SmCxIcon, dpi), GetSystemMetricsForDpi(SmCyIcon, dpi), LrLoadFromFile);

            var small = LoadImage(IntPtr.Zero, iconPath, ImageIcon,
                GetSystemMetricsForDpi(SmCxSmIcon, dpi), GetSystemMetricsForDpi(SmCySmIcon, dpi), LrLoadFromFile);

            if (big != IntPtr.Zero)
                SendMessage(hwnd, WmSetIcon, IconvBig, big);

            if (small != IntPtr.Zero)
            {
                SendMessage(hwnd, WmSetIcon, IconvSmall, small);

                // ICON_SMALL2 是给不认 ICON_SMALL 的那些老路径用的，设上更稳。
                SendMessage(hwnd, WmSetIcon, IconvSmall2, small);
            }

            // 这两个 HICON 必须一直活着：窗口持有的是句柄而不是副本，
            // 在这里销毁等于把标题栏上的图标也一起抹掉。
            WindowIcons[hwnd] = (big, small);
        }
        catch (DllNotFoundException)
        {
            // 拿不到 user32 就退回系统默认图标，不影响窗口本身。
        }
    }

    /// <summary>
    /// 限制窗口最小尺寸（逻辑像素）。
    /// WinUI 没有公开这个能力，只能子类化窗口过程拦截 WM_GETMINMAXINFO。
    /// </summary>
    public static void SetMinSize(Window window, int logicalWidth, int logicalHeight)
    {
        var hwnd = GetHandle(window);
        if (hwnd == IntPtr.Zero) return;

        var scale = GetScale(window);
        MinSizes[hwnd] = ((int)Math.Round(logicalWidth * scale), (int)Math.Round(logicalHeight * scale));

        try
        {
            // 回调委托必须由静态字段持有，否则会被 GC 回收，
            // 之后系统回调到已释放的地址就是崩溃。
            if (SetWindowSubclass(hwnd, SubclassProc, SubclassId, IntPtr.Zero))
                SubclassedWindows.TryAdd(hwnd, 0);
        }
        catch (DllNotFoundException)
        {
            // 拿不到 comctl32 就放弃最小尺寸限制，不影响其他功能。
        }
    }

    /// <summary>取窗口缩放比例（1.0 = 100% DPI）。</summary>
    public static double GetScale(Window window)
    {
        var hwnd = GetHandle(window);
        if (hwnd == IntPtr.Zero) return 1.0;

        try
        {
            var dpi = GetDpiForWindow(hwnd);
            return dpi <= 0 ? 1.0 : dpi / 96.0;
        }
        catch (DllNotFoundException)
        {
            return 1.0;
        }
    }

    // =============================================================
    //  Win32 互操作
    // =============================================================

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;
    private const int DwmwaWindowCornerPreference = 33;
    private const uint WmGetMinMaxInfo = 0x0024;
    private const uint SubclassId = 0x1A2B;

    private const uint WmSetIcon = 0x0080;
    private static readonly IntPtr IconvBig = 1;
    private static readonly IntPtr IconvSmall = 0;
    private static readonly IntPtr IconvSmall2 = 2;
    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x00000010;
    private const int SmCxIcon = 11;
    private const int SmCyIcon = 12;
    private const int SmCxSmIcon = 49;
    private const int SmCySmIcon = 50;

    private static readonly ConcurrentDictionary<IntPtr, (int Width, int Height)> MinSizes = new();
    private static readonly ConcurrentDictionary<IntPtr, byte> SubclassedWindows = new();

    /// <summary>窗口当前挂着的 HICON。留着是为了不被 GC/释放掉，见 SetIcon 里的说明。</summary>
    private static readonly ConcurrentDictionary<IntPtr, (IntPtr Big, IntPtr Small)> WindowIcons = new();

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetricsForDpi(int index, uint dpi);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hinst, string name, uint type, int cx, int cy, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr SubclassProcDelegate(
        IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProcDelegate callback, uint uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    private static readonly SubclassProcDelegate SubclassProc = OnSubclassMessage;

    private static IntPtr OnSubclassMessage(
        IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == WmGetMinMaxInfo && MinSizes.TryGetValue(hWnd, out var minSize))
        {
            var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            info.MinTrackSize.X = minSize.Width;
            info.MinTrackSize.Y = minSize.Height;
            Marshal.StructureToPtr(info, lParam, fDeleteOld: false);
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }
}
