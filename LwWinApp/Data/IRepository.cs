using System.Data;
using LwWinApp.Models.Dtos;

namespace LwWinApp.Data;

/// <summary>
/// 通用仓储契约。常规单表 CRUD 直接用这些方法；
/// 复杂查询在具体仓储里加自己的方法，内部仍可复用基类的连接与分页能力。
/// </summary>
public interface IRepository<T> where T : class
{
    /// <summary>按主键取一条。</summary>
    Task<T?> GetByIdAsync(object id, CancellationToken ct = default);

    /// <summary>取全部（表数据量大时慎用）。</summary>
    Task<IReadOnlyList<T>> GetAllAsync(string? sortColumn = null, bool descending = false, CancellationToken ct = default);

    /// <summary>
    /// 条件查询。where 只写条件本身（不带 WHERE 关键字），
    /// 例如 "`status` = @Status"，参数通过 param 传入。
    /// sortColumn 必须是实体真实列名，不在白名单内会被忽略。
    /// </summary>
    Task<IReadOnlyList<T>> QueryAsync(
        string? where = null,
        object? param = null,
        string? sortColumn = null,
        bool descending = false,
        int? limit = null,
        CancellationToken ct = default);

    Task<T?> QueryFirstAsync(
        string? where = null,
        object? param = null,
        string? sortColumn = null,
        bool descending = true,
        CancellationToken ct = default);

    Task<long> CountAsync(string? where = null, object? param = null, CancellationToken ct = default);

    Task<bool> ExistsAsync(object id, CancellationToken ct = default);

    /// <summary>插入并回填自增主键，返回新 Id（非自增主键时返回 0）。</summary>
    Task<long> InsertAsync(T entity, IDbTransaction? tx = null, CancellationToken ct = default);

    /// <summary>按主键整行更新，返回受影响行数。</summary>
    Task<int> UpdateAsync(T entity, IDbTransaction? tx = null, CancellationToken ct = default);

    /// <summary>按主键删除，返回受影响行数。</summary>
    Task<int> DeleteAsync(object id, IDbTransaction? tx = null, CancellationToken ct = default);

    /// <summary>按条件批量删除，返回受影响行数。</summary>
    Task<int> DeleteWhereAsync(string where, object? param = null, IDbTransaction? tx = null, CancellationToken ct = default);

    /// <summary>分页查询，返回带总数的结果集。</summary>
    Task<PagedResult<T>> QueryPagedAsync(
        QueryFilter filter,
        string? where = null,
        object? param = null,
        string? defaultSortColumn = null,
        CancellationToken ct = default);
}
