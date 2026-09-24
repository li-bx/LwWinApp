namespace LwWinApp.Models.Dtos;

/// <summary>
/// 查询条件基类。具体业务查询继承它加自己的筛选字段
/// （如 UserQueryFilter 加 Status、DeptId）。
/// </summary>
public class QueryFilter
{
    /// <summary>允许的最大每页条数，防止前端传入超大值拖垮数据库。</summary>
    public const int MaxPageSize = 500;

    public const int DefaultPageSize = 15;

    private int _pageIndex = 1;
    private int _pageSize = DefaultPageSize;

    /// <summary>页码，从 1 开始。小于 1 会被纠正为 1。</summary>
    public int PageIndex
    {
        get => _pageIndex;
        set => _pageIndex = value < 1 ? 1 : value;
    }

    /// <summary>每页条数，会被夹在 1..<see cref="MaxPageSize"/> 之间。</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value <= 0 ? DefaultPageSize : value, 1, MaxPageSize);
    }

    /// <summary>模糊搜索关键字。</summary>
    public string? Keyword { get; set; }

    /// <summary>排序列名（数据库列名）。为空时用仓储默认排序。</summary>
    public string? OrderBy { get; set; }

    /// <summary>是否降序。</summary>
    public bool Descending { get; set; } = true;

    /// <summary>SQL LIMIT 用的偏移量。</summary>
    public int Offset => (PageIndex - 1) * PageSize;

    /// <summary>当前筛选条件下是否有关键字。</summary>
    public bool HasKeyword => !string.IsNullOrWhiteSpace(Keyword);

    /// <summary>把关键字包装成 LIKE 模式，并转义 % 和 _ 以免被当通配符。</summary>
    public string LikeKeyword => "%" + (Keyword ?? string.Empty)
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_") + "%";

    public void Reset() => PageIndex = 1;
}
