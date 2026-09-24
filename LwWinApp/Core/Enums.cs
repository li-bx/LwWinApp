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

/// <summary>
/// 弹窗窗口的关闭原因。
///
/// 刻意不用 <c>ContentDialogResult</c>：那个枚举只有 None / Primary / Secondary，
/// 「点了取消按钮」和「直接关了标题栏的 X」在它那里都是 None，分不出来。
/// 而这两件事在窗口上必须分得清 —— 一个是用户想撤，一个是用户想跑。
/// </summary>
public enum DialogResult
{
    /// <summary>没有结果：用户直接关了标题栏的 X（或按了 Esc 而窗口没有取消按钮）。</summary>
    None = 0,

    Primary = 1,

    Secondary = 2,

    /// <summary>点了取消 / 关闭按钮。</summary>
    Close = 3
}
