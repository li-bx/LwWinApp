using System.Text;

namespace LwWinApp.Utils;

/// <summary>表格数据导出成 CSV。</summary>
public static class CsvExporter
{
    /// <summary>
    /// UTF-8 BOM。少了它 Excel 会按本地代码页解析，
    /// 中文列名会变成乱码 —— 这是导出功能最常见的坑。
    /// </summary>
    private const string Utf8Bom = "﻿";

    /// <summary>按显式指定的列导出。列顺序即传入顺序，推荐用这个。</summary>
    public static string ToCsv<T>(
        IEnumerable<T> rows,
        params (string Header, Func<T, object?> Selector)[] columns)
    {
        var builder = new StringBuilder(Utf8Bom);

        builder.AppendLine(string.Join(",", columns.Select(c => Escape(c.Header))));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", columns.Select(c =>
            {
                var value = c.Selector(row);
                return Escape(Format(value));
            })));
        }

        return builder.ToString();
    }

    /// <summary>按实体属性反射导出，表头用属性名。列顺序按声明顺序。</summary>
    public static string ToCsv<T>(IEnumerable<T> rows, bool includeComputedProperties = false)
    {
        var properties = typeof(T)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .Where(p => includeComputedProperties || p.CanWrite || IsSimpleType(p.PropertyType))
            .ToList();

        var builder = new StringBuilder(Utf8Bom);
        builder.AppendLine(string.Join(",", properties.Select(p => Escape(p.Name))));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", properties.Select(p =>
                Escape(Format(p.GetValue(row))))));
        }

        return builder.ToString();
    }

    /// <summary>导出并写入文件。文件名冲突时自动加序号，不覆盖已有文件。</summary>
    public static async Task<string?> ExportAsync<T>(
        IEnumerable<T> rows,
        string fileName,
        CancellationToken ct = default)
        => await ExportAsync(rows, FileHelper.DefaultExportDirectory, fileName, ct);

    public static async Task<string?> ExportAsync<T>(
        IEnumerable<T> rows,
        string directory,
        string fileName,
        CancellationToken ct = default)
    {
        if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            fileName += ".csv";

        var path = FileHelper.CombineUnique(directory, fileName);
        var content = ToCsv(rows);

        return await FileHelper.WriteTextAsync(path, content, ct) ? path : null;
    }

    /// <summary>同上，按显式列导出。</summary>
    public static async Task<string?> ExportAsync<T>(
        IEnumerable<T> rows,
        IEnumerable<(string Header, Func<T, object?> Selector)> columns,
        string fileName,
        string? directory = null,
        CancellationToken ct = default)
    {
        if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            fileName += ".csv";

        var path = FileHelper.CombineUnique(directory ?? FileHelper.DefaultExportDirectory, fileName);
        var content = ToCsv(rows, columns.ToArray());

        return await FileHelper.WriteTextAsync(path, content, ct) ? path : null;
    }

    /// <summary>CSV 转义：含逗号、引号、换行的值要用双引号包起来，内部引号翻倍。</summary>
    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var needsQuoting = value.Contains(',')
                        || value.Contains('"')
                        || value.Contains('\n')
                        || value.Contains('\r');

        return needsQuoting ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        DateTime dt => dt.ToString(DateTimeHelper.FormatDateTime),
        DateTimeOffset dto => dto.ToString(DateTimeHelper.FormatDateTime),
        bool b => b ? "是" : "否",
        Enum e => e.ToString(),
        _ => value.ToString() ?? string.Empty
    };

    private static bool IsSimpleType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive
            || underlying.IsEnum
            || underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(TimeSpan)
            || underlying == typeof(Guid);
    }
}
