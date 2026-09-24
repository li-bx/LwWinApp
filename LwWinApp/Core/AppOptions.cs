namespace LwWinApp.Core;

/// <summary>
/// appsettings.json 里 <c>App</c> 节对应的强类型配置。
///
/// 用 <c>IOptionsMonitor&lt;AppOptions&gt;</c> 注入，不要直接读 IConfiguration 的字符串键——
/// 键名写错只有运行时才发现，绑到类上则有编译期检查。
/// </summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>窗口标题。</summary>
    public string Title { get; set; } = AppConstants.AppTitle;

    /// <summary>列表页默认每页条数。</summary>
    public int PageSize { get; set; } = 15;

    /// <summary>分页器下拉里可选的每页条数。</summary>
    public int[] PageSizeOptions { get; set; } = [10, 15, 20, 30, 50, 100];

    /// <summary>启动时的主题：Default / Light / Dark。</summary>
    public string DefaultTheme { get; set; } = "Default";

    /// <summary>是否记住窗口尺寸与位置。</summary>
    public bool RememberWindowState { get; set; } = true;

    /// <summary>导出文件的默认目录。留空则用「我的文档\LwWinApp」。</summary>
    public string ExportDirectory { get; set; } = string.Empty;

    /// <summary>旧格式密码哈希用的盐。必须与 Sql/init.sql 里的一致。</summary>
    public string PasswordSalt { get; set; } = "LwWinApp@2026";

    /// <summary>数据库命令超时秒数。</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>归一化后的分页选项：去重、去非正数、升序。配置写歪了也不至于让分页器崩掉。</summary>
    public IReadOnlyList<int> GetValidPageSizeOptions()
    {
        var options = (PageSizeOptions ?? [])
            .Where(size => size > 0)
            .Distinct()
            .OrderBy(size => size)
            .ToList();

        return options.Count > 0 ? options : [10, 20, 50];
    }
}
