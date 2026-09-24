namespace LwWinApp.Models.Dtos;

/// <summary>
/// 聚合查询的通用返回：一个名字 + 一个计数。
///
/// 用类而不是 record：Dapper 对「属性可写」的映射最稳妥，
/// 位置构造函数要额外依赖参数名匹配，换编译器版本时容易踩坑。
/// </summary>
public sealed class NameCount
{
    public string Name { get; set; } = string.Empty;

    public long Count { get; set; }
}
