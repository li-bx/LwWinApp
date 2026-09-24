using System.Text;

namespace LwWinApp.Utils;

/// <summary>
/// 把日志写到本地文件的日志提供程序。
///
/// 为什么需要它：桌面应用打包成 exe 分发后没有控制台，出问题时用户手上
/// 什么线索都没有。写文件是唯一可靠的手段。
///
/// 文件位置：<c>%LOCALAPPDATA%\LwWinApp\logs\app-yyyyMMdd.log</c>，按天分文件，
/// 保留最近 <see cref="RetentionDays"/> 天。
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    /// <summary>单条日志超过这个长度就截断，防止异常堆栈把文件撑爆。</summary>
    internal const int MaxMessageLength = 8000;

    /// <summary>日志保留天数，启动时清掉更早的文件。</summary>
    internal const int RetentionDays = 7;

    private readonly LogWriter _writer;

    public FileLoggerProvider(string directory, LogLevel minimumLevel = LogLevel.Information)
    {
        _writer = new LogWriter(directory, minimumLevel);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _writer);

    public void Dispose() => _writer.Dispose();
}

/// <inheritdoc />
internal sealed class FileLogger : ILogger
{
    private readonly string _category;
    private readonly LogWriter _writer;

    public FileLogger(string category, LogWriter writer)
    {
        _category = category;
        _writer = writer;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => _writer.IsEnabled(logLevel);

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        string message;
        try
        {
            message = formatter(state, exception);
        }
        catch
        {
            // 格式化函数由调用方提供，出错不能把写日志这件事带崩。
            message = "(日志格式化失败)";
        }

        var builder = new StringBuilder()
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            .Append(' ')
            .Append(LevelText(logLevel))
            .Append(' ')
            .Append(_category)
            .Append(" | ")
            .Append(message);

        if (exception is not null)
        {
            builder.AppendLine().Append(exception);

            // InnerException 不会自己出现在 ToString 里，但排查时往往正是关键。
            var inner = exception.InnerException;
            var depth = 0;

            while (inner is not null && depth++ < 5)
            {
                builder.AppendLine().Append("  ---> ").Append(inner);
                inner = inner.InnerException;
            }
        }

        var text = builder.ToString();

        if (text.Length > FileLoggerProvider.MaxMessageLength)
            text = text[..FileLoggerProvider.MaxMessageLength] + " …(已截断)";

        _writer.Write(text);
    }

    private static string LevelText(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???"
    };
}

/// <summary>
/// 实际的文件写入。按天切换文件，写入时加锁并立即冲刷。
///
/// 立即冲刷是刻意的：应用崩溃时缓冲区里的内容正是我们最想看的部分。
/// 桌面应用的日志量下，这点 IO 开销可以接受。
/// </summary>
internal sealed class LogWriter : IDisposable
{
    private readonly string _directory;
    private readonly LogLevel _minimumLevel;
    private readonly object _gate = new();

    private StreamWriter? _stream;
    private DateOnly _currentDate;

    public LogWriter(string directory, LogLevel minimumLevel)
    {
        _directory = directory;
        _minimumLevel = minimumLevel;

        try
        {
            Directory.CreateDirectory(directory);
            CleanupOldFiles();
        }
        catch
        {
            // 目录建不出来（权限、磁盘满）时后续写入会失败并被吞掉，
            // 应用本身要能照常跑。
        }
    }

    public bool IsEnabled(LogLevel level) => level >= _minimumLevel && level != LogLevel.None;

    public void Write(string text)
    {
        lock (_gate)
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.Now);

                // 跨天时换文件。长时间开着的应用会正好撞上这个分支。
                if (_stream is null || today != _currentDate)
                    RollTo(today);

                _stream?.WriteLine(text);
                _stream?.Flush();
            }
            catch
            {
                // 磁盘写不进去不能让业务逻辑崩，丢弃这一条即可。
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            try
            {
                _stream?.Dispose();
            }
            catch
            {
                // 忽略
            }
            finally
            {
                _stream = null;
            }
        }
    }

    private void RollTo(DateOnly date)
    {
        _stream?.Dispose();

        var path = Path.Combine(_directory, $"app-{date:yyyyMMdd}.log");

        _stream = new StreamWriter(path, append: true, Encoding.UTF8);
        _currentDate = date;
    }

    /// <summary>启动时清掉过期日志，避免日志目录无限增长。</summary>
    private void CleanupOldFiles()
    {
        var cutoff = DateTime.Now.AddDays(-FileLoggerProvider.RetentionDays);

        foreach (var file in Directory.EnumerateFiles(_directory, "app-*.log"))
        {
            try
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
            catch
            {
                // 单个文件删不掉（被占用等）不影响其他文件。
            }
        }
    }
}
