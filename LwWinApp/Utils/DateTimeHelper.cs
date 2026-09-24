namespace LwWinApp.Utils;

/// <summary>时间日期工具。</summary>
public static class DateTimeHelper
{
    /// <summary>Unix 时间戳起点。</summary>
    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public const string FormatDate = "yyyy-MM-dd";
    public const string FormatDateTime = "yyyy-MM-dd HH:mm:ss";
    public const string FormatTime = "HH:mm:ss";
    public const string FormatCompact = "yyyyMMddHHmmss";

    /// <summary>本地时间 → Unix 秒级时间戳。</summary>
    public static long ToUnixSeconds(DateTime time)
        => (long)(time.ToUniversalTime() - UnixEpoch).TotalSeconds;

    /// <summary>Unix 秒级时间戳 → 本地时间。</summary>
    public static DateTime FromUnixSeconds(long seconds)
        => UnixEpoch.AddSeconds(seconds).ToLocalTime();

    /// <summary>Unix 毫秒级时间戳（前端常用）。</summary>
    public static long ToUnixMilliseconds(DateTime time)
        => (long)(time.ToUniversalTime() - UnixEpoch).TotalMilliseconds;

    public static DateTime FromUnixMilliseconds(long milliseconds)
        => UnixEpoch.AddMilliseconds(milliseconds).ToLocalTime();

    public static string ToDateString(this DateTime time) => time.ToString(FormatDate);
    public static string ToDateTimeString(this DateTime time) => time.ToString(FormatDateTime);

    /// <summary>可空时间格式化，null 显示成占位符。</summary>
    public static string ToDisplayString(this DateTime? time, string placeholder = "—")
        => time?.ToString(FormatDateTime) ?? placeholder;

    /// <summary>
    /// 友好的相对时间：刚刚 / 3 分钟前 / 2 小时前 / 昨天 15:04 / 03-12 / 2025-08-01。
    /// 越久远越省略细节，列表里更易读。
    /// </summary>
    public static string ToFriendlyString(this DateTime time)
    {
        var now = DateTime.Now;
        var span = now - time;

        // 时间在未来（时钟偏差、或数据有问题）时不显示「-3 分钟前」这种怪东西。
        if (span < TimeSpan.Zero) return time.ToString(FormatDateTime);

        if (span.TotalSeconds < 60) return "刚刚";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} 分钟前";
        if (span.TotalHours < 24 && time.Date == now.Date) return $"{(int)span.TotalHours} 小时前";

        if (time.Date == now.Date.AddDays(-1)) return $"昨天 {time:HH:mm}";
        if (time.Year == now.Year) return time.ToString("MM-dd HH:mm");
        return time.ToString(FormatDate);
    }

    /// <summary>一天的开始（00:00:00）。</summary>
    public static DateTime StartOfDay(this DateTime time) => time.Date;

    /// <summary>一天的结束（23:59:59.999）。</summary>
    public static DateTime EndOfDay(this DateTime time) => time.Date.AddDays(1).AddTicks(-1);

    /// <summary>所在周的周一（以周一为一周开始）。</summary>
    public static DateTime StartOfWeek(this DateTime time, DayOfWeek firstDay = DayOfWeek.Monday)
    {
        var diff = (7 + (time.DayOfWeek - firstDay)) % 7;
        return time.Date.AddDays(-diff);
    }

    /// <summary>所在月的第一天。</summary>
    public static DateTime StartOfMonth(this DateTime time) => new(time.Year, time.Month, 1);

    /// <summary>所在月的最后一天。</summary>
    public static DateTime EndOfMonth(this DateTime time)
        => new DateTime(time.Year, time.Month, 1).AddMonths(1).AddDays(-1);

    /// <summary>自然语言式的时长：1 小时 23 分钟。</summary>
    public static string ToDurationString(this TimeSpan span)
    {
        if (span.TotalSeconds < 1) return $"{span.TotalMilliseconds:0} 毫秒";
        if (span.TotalMinutes < 1) return $"{span.TotalSeconds:0.#} 秒";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} 分钟 {span.Seconds} 秒";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours} 小时 {span.Minutes} 分钟";
        return $"{(int)span.TotalDays} 天 {span.Hours} 小时";
    }

    /// <summary>按当前时间给出问候语，仪表盘用。</summary>
    public static string Greeting()
    {
        var hour = DateTime.Now.Hour;
        return hour switch
        {
            >= 5 and < 9 => "早上好",
            >= 9 and < 12 => "上午好",
            >= 12 and < 14 => "中午好",
            >= 14 and < 18 => "下午好",
            >= 18 and < 23 => "晚上好",
            _ => "夜深了"
        };
    }

    /// <summary>2026-09-22 12:30 → 2026年09月22日 12时30分，用于文件名等不能带特殊字符的场景。</summary>
    /// <summary>
    /// 生成适合放进文件名的「年月日_时分秒」，用当前时间。
    ///
    /// 单独留这个无参重载，是因为扩展方法的 <c>this</c> 参数不能有默认值，
    /// 想写 <c>DateTimeHelper.ForFileName()</c> 就只能再开一个。
    /// </summary>
    public static string ForFileName() => DateTime.Now.ForFileName();

    /// <summary>同上，但用指定的时间。</summary>
    public static string ForFileName(this DateTime time)
        => time.ToString("yyyyMMdd_HHmmss");
}
