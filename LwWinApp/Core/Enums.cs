namespace LwWinApp.Core;

/// <summary>性别。</summary>
public enum Gender
{
    Unknown = 0,
    Male = 1,
    Female = 2
}

/// <summary>启用状态，与数据库 status 列对应（1 启用 / 0 停用）。</summary>
public enum EnabledStatus
{
    Disabled = 0,
    Enabled = 1
}

/// <summary>日志级别，用于 sys_log.level 列与筛选下拉框。</summary>
public enum LogLevelKind
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3,
    Fatal = 4
}

/// <summary>主题模式。</summary>
public enum ThemeMode
{
    /// <summary>跟随系统。</summary>
    Default = 0,
    Light = 1,
    Dark = 2
}

/// <summary>轻提示（Toast 风格）的类型。</summary>
public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>提示对话框的语义类型，决定图标与配色。</summary>
public enum DialogKind
{
    Info,
    Success,
    Warning,
    Error,
    Question
}
