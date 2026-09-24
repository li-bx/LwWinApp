namespace LwWinApp.Core;

/// <summary>框架级常量。改这里即可全局生效。</summary>
public static class AppConstants
{
    /// <summary>应用名，用于日志分类、本地设置容器名。</summary>
    public const string AppName = "LwWinApp";

    /// <summary>窗口标题。</summary>
    public const string AppTitle = "LwWinApp Spring应用框架";

    /// <summary>版本号，设置页「关于」显示。</summary>
    public const string AppVersion = "1.0.0";

    /// <summary>appsettings.json 中连接串的名字。</summary>
    public const string DefaultConnectionName = "LwWinApp";

    /// <summary>本地设置（主题、窗口状态等）的容器键。</summary>
    public const string SettingsContainer = "LwWinApp.Settings";

    /// <summary>窗口最小尺寸，防止布局被压垮。</summary>
    public const int MinWindowWidth = 960;
    public const int MinWindowHeight = 640;

    /// <summary>窗口默认尺寸。</summary>
    public const int DefaultWindowWidth = 1440;
    public const int DefaultWindowHeight = 900;
}
