using System.Text;

namespace LwWinApp.Utils;

/// <summary>字符串处理工具。</summary>
public static class StringHelper
{
    /// <summary>为空或纯空白。</summary>
    public static bool IsBlank(this string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>非空且非纯空白。</summary>
    public static bool IsNotBlank(this string? value) => !string.IsNullOrWhiteSpace(value);

    /// <summary>为 null 时退回默认值。</summary>
    public static string OrDefault(this string? value, string fallback = "")
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    /// <summary>超出长度则截断并加省略号。</summary>
    public static string Truncate(this string? value, int maxLength, string ellipsis = "…")
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength] + ellipsis;
    }

    /// <summary>
    /// PascalCase → snake_case。仓储靠它把实体属性名映射成数据库列名，
    /// 所以必须处理连续大写（IP → ip、URLPath → url_path）这类情况。
    /// </summary>
    public static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var sb = new StringBuilder(input.Length + 8);
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (!char.IsUpper(c))
            {
                sb.Append(c);
                continue;
            }

            // 前一个字符是小写或数字（userName → user_name），
            // 或者处于「连续大写 + 后面跟小写」的收尾位置（URLPath → url_path），
            // 才需要在这里插入下划线。
            var prevIsLowerOrDigit = i > 0 && (char.IsLower(input[i - 1]) || char.IsDigit(input[i - 1]));
            var nextIsLower = i + 1 < input.Length && char.IsLower(input[i + 1]);
            var prevIsUpper = i > 0 && char.IsUpper(input[i - 1]);

            if (i > 0 && (prevIsLowerOrDigit || (nextIsLower && prevIsUpper)))
                sb.Append('_');

            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    /// <summary>snake_case → PascalCase（数据库列名反推属性名时用）。</summary>
    public static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var sb = new StringBuilder(input.Length);
        var upperNext = true;
        foreach (var c in input)
        {
            if (c == '_')
            {
                upperNext = true;
                continue;
            }

            sb.Append(upperNext ? char.ToUpperInvariant(c) : c);
            upperNext = false;
        }

        return sb.ToString();
    }

    /// <summary>手机号掩码：13812345678 → 138****5678。</summary>
    public static string MaskPhone(this string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 7) return phone.OrDefault();
        return phone[..3] + "****" + phone[^4..];
    }

    /// <summary>邮箱掩码：zhangsan@a.com → zh****an@a.com。</summary>
    public static string MaskEmail(this string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;
        var at = email.IndexOf('@');
        if (at <= 1) return email;
        var name = email[..at];
        var domain = email[at..];
        if (name.Length <= 3) return name[..1] + "***" + domain;
        return name[..2] + new string('*', Math.Min(4, name.Length - 3)) + name[^2..] + domain;
    }

    /// <summary>取姓名首字（头像占位用）。</summary>
    public static string Initial(this string? value)
        => string.IsNullOrWhiteSpace(value) ? "?" : value.Trim()[..1].ToUpperInvariant();

    /// <summary>按存储字节数格式化成 B / KB / MB / GB。</summary>
    public static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{size:0.##} {units[unit]}";
    }
}
