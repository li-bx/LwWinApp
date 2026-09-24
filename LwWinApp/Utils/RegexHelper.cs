using System.Text.RegularExpressions;

namespace LwWinApp.Utils;

/// <summary>
/// 常用格式校验。
/// 正则用 <c>GeneratedRegex</c> 源生成器编译成代码，比每次 new Regex 快得多，
/// 而且表达式是编译期检查的。
/// </summary>
public static partial class RegexHelper
{
    /// <summary>
    /// 手机号正则原文。
    /// 单独暴露成常量是为了给 <c>[RegularExpression]</c> 用 ——
    /// 那个特性只接受编译期常量，拿不到下面的 GeneratedRegex 方法。
    /// 写字符串而不是复制一份，两者就永远不会对不上。
    /// </summary>
    public const string PhoneText = @"^1[3-9]\d{9}$";

    /// <summary>登录账号正则原文。同上，供数据注解使用。</summary>
    public const string UserNameText = @"^[A-Za-z][A-Za-z0-9_]{2,19}$";

    [GeneratedRegex(PhoneText)]
    private static partial Regex PhonePattern();

    /// <summary>邮箱。不做 RFC 全量校验，覆盖常见写法即可。</summary>
    [GeneratedRegex(@"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^\d{17}[\dXx]$")]
    private static partial Regex IdCardPattern();

    [GeneratedRegex(@"^((25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)\.){3}(25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)$")]
    private static partial Regex IPv4Pattern();

    [GeneratedRegex(@"^https?://[^\s]+$")]
    private static partial Regex UrlPattern();

    /// <summary>中文姓名：2~10 个汉字，允许「·」用于少数民族姓名。</summary>
    [GeneratedRegex(@"^[一-龥]{2,10}(·[一-龥]{2,10})*$")]
    private static partial Regex ChineseNamePattern();

    /// <summary>登录账号：字母开头，可含字母数字下划线，3~20 位。</summary>
    [GeneratedRegex(UserNameText)]
    private static partial Regex UserNamePattern();

    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex PostalCodePattern();

    /// <summary>整数（可带负号）。</summary>
    [GeneratedRegex(@"^-?\d+$")]
    private static partial Regex IntegerPattern();

    /// <summary>小数（可带负号、可带小数点）。</summary>
    [GeneratedRegex(@"^-?\d+(\.\d+)?$")]
    private static partial Regex DecimalPattern();

    /// <summary>密码强度：至少 6 位，且同时包含字母和数字。</summary>
    [GeneratedRegex(@"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d\W_]{6,}$")]
    private static partial Regex StrongPasswordPattern();

    public static bool IsPhone(string? value)
        => !string.IsNullOrWhiteSpace(value) && PhonePattern().IsMatch(value);

    public static bool IsEmail(string? value)
        => !string.IsNullOrWhiteSpace(value) && EmailPattern().IsMatch(value);

    public static bool IsIdCard(string? value)
        => !string.IsNullOrWhiteSpace(value) && IdCardPattern().IsMatch(value);

    public static bool IsIPv4(string? value)
        => !string.IsNullOrWhiteSpace(value) && IPv4Pattern().IsMatch(value);

    public static bool IsUrl(string? value)
        => !string.IsNullOrWhiteSpace(value) && UrlPattern().IsMatch(value);

    public static bool IsChineseName(string? value)
        => !string.IsNullOrWhiteSpace(value) && ChineseNamePattern().IsMatch(value);

    public static bool IsUserName(string? value)
        => !string.IsNullOrWhiteSpace(value) && UserNamePattern().IsMatch(value);

    public static bool IsPostalCode(string? value)
        => !string.IsNullOrWhiteSpace(value) && PostalCodePattern().IsMatch(value);

    public static bool IsInteger(string? value)
        => !string.IsNullOrWhiteSpace(value) && IntegerPattern().IsMatch(value);

    public static bool IsDecimal(string? value)
        => !string.IsNullOrWhiteSpace(value) && DecimalPattern().IsMatch(value);

    public static bool IsStrongPassword(string? value)
        => !string.IsNullOrWhiteSpace(value) && StrongPasswordPattern().IsMatch(value);

    /// <summary>把字符串里的正则元字符转义掉，用于拼接进更大的模式。</summary>
    public static string Escape(string input) => Regex.Escape(input);

    /// <summary>手机号打码：13812345678 → 138****5678。非手机号原样返回。</summary>
    public static string MaskPhone(string? value)
        => IsPhone(value) ? value![..3] + "****" + value[^4..] : value.OrEmpty();

    private static string OrEmpty(this string? value) => value ?? string.Empty;
}
