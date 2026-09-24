namespace LwWinApp.Core;

/// <summary>
/// 业务异常。Service 层校验失败时抛这个类型，
/// UI 层捕获后直接把它当成「可以展示给用户看的信息」，
/// 而其他异常则统一提示「系统异常」并记日志。
/// </summary>
public sealed class BusinessException : Exception
{
    public BusinessException(string message) : base(message) { }

    public BusinessException(string message, Exception inner) : base(message, inner) { }

    /// <summary>可选：出错字段名，便于表单高亮到具体控件。</summary>
    public string? FieldName { get; init; }

    public static BusinessException Required(string fieldName)
        => new($"「{fieldName}」不能为空") { FieldName = fieldName };

    public static BusinessException NotFound(string what)
        => new($"{what}不存在或已被删除");
}
