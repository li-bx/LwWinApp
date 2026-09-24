using System.Text;

namespace LwWinApp.Utils;

/// <summary>文件与目录工具。所有方法都不抛异常，失败时返回 false 或默认值。</summary>
public static class FileHelper
{
    /// <summary>
    /// 导出目录，默认「我的文档\LwWinApp」。
    /// App 启动时会用 appsettings.json 的 App:ExportDirectory 覆盖它（配置为空则保持默认）。
    /// </summary>
    public static string? ExportDirectoryOverride { get; set; }

    /// <summary>项目常用的导出目录。返回前会确保目录存在。</summary>
    public static string DefaultExportDirectory
    {
        get
        {
            var configured = ExportDirectoryOverride;

            // 配置里可能写了相对路径或环境变量，展开后再判断是否可用。
            if (!string.IsNullOrWhiteSpace(configured))
            {
                var expanded = Environment.ExpandEnvironmentVariables(configured.Trim());

                try
                {
                    var full = Path.GetFullPath(expanded);

                    if (EnsureDirectory(full))
                        return full;
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    // 配错了就退回默认目录，不因为一个路径让导出功能整个不可用。
                }
            }

            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Core.AppConstants.AppName);

            EnsureDirectory(path);
            return path;
        }
    }

    /// <summary>把字节数格式化成 B / KB / MB / GB。</summary>
    public static string FormatSize(long bytes) => StringHelper.FormatSize(bytes);

    /// <summary>目录不存在就创建。返回是否可用。</summary>
    public static bool EnsureDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            Directory.CreateDirectory(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>确保文件所在目录存在。</summary>
    public static bool EnsureParentDirectory(string filePath)
        => EnsureDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);

    public static bool Exists(string path) => !string.IsNullOrWhiteSpace(path) && File.Exists(path);

    public static bool DirectoryExists(string path) => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

    /// <summary>读文本，默认按 UTF-8 处理。失败返回 null。</summary>
    public static async Task<string?> ReadTextAsync(string path, CancellationToken ct = default)
    {
        try
        {
            return await File.ReadAllTextAsync(path, Encoding.UTF8, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>写文本，自动创建父目录。返回是否成功。</summary>
    public static async Task<bool> WriteTextAsync(string path, string content, CancellationToken ct = default)
    {
        try
        {
            EnsureParentDirectory(path);
            await File.WriteAllTextAsync(path, content, new UTF8Encoding(false), ct);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// 生成不冲突的文件名：report.csv 已存在时变成 report(1).csv、report(2).csv…
    /// 用于「导出」这类不应该静默覆盖用户文件的场景。
    /// </summary>
    public static string MakeUniquePath(string path)
    {
        if (!Exists(path)) return path;

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (var i = 1; i < 10_000; i++)
        {
            var candidate = Path.Combine(directory, $"{name}({i}){extension}");
            if (!Exists(candidate)) return candidate;
        }

        // 极端情况下退回带时间戳的名字。
        return Path.Combine(directory, $"{name}_{DateTime.Now.ForFileName()}{extension}");
    }

    /// <summary>安全删除文件，失败返回 false。</summary>
    public static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// 在文件资源管理器中定位到该文件（打开所在文件夹并选中它）。
    /// </summary>
    public static bool RevealInExplorer(string path)
    {
        try
        {
            if (!Exists(path)) return false;

            // /select 让资源管理器打开父目录并高亮该文件。
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>用系统默认程序打开文件或网址。</summary>
    public static bool OpenWithShell(string pathOrUrl)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = pathOrUrl,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>取目录下第一个可用的新文件名，避免重名冲突。</summary>
    public static string CombineUnique(string directory, string fileName)
        => MakeUniquePath(Path.Combine(directory, fileName));
}
