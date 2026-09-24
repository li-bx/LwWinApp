namespace LwWinApp.Models.Dtos;

/// <summary>分页查询结果。总页数、上下页判断都由它算好，UI 直接用。</summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    /// <summary>符合条件的总记录数（不是当前页条数）。</summary>
    public long Total { get; init; }

    /// <summary>当前页码，从 1 开始。</summary>
    public int PageIndex { get; init; } = 1;

    public int PageSize { get; init; } = 15;

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);

    public bool HasPrevious => PageIndex > 1;

    public bool HasNext => PageIndex < TotalPages;

    /// <summary>当前页第一条记录在整个结果集中的序号（从 1 开始），没有记录时为 0。</summary>
    public long FirstItemIndex => Total == 0 ? 0 : ((long)(PageIndex - 1) * PageSize) + 1;

    /// <summary>当前页最后一条记录的序号。</summary>
    public long LastItemIndex => Total == 0 ? 0 : FirstItemIndex + Items.Count - 1;

    public static PagedResult<T> Empty(int pageIndex = 1, int pageSize = 15)
        => new() { PageIndex = pageIndex, PageSize = pageSize };

    /// <summary>把结果集映射成另一种元素类型（实体 → ViewModel 行）。</summary>
    public PagedResult<TOut> Map<TOut>(Func<T, TOut> selector) => new()
    {
        Items = Items.Select(selector).ToList(),
        Total = Total,
        PageIndex = PageIndex,
        PageSize = PageSize
    };
}
