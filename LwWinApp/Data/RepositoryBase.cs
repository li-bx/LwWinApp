using System.Data;
using Dapper;
using MySqlConnector;
using LwWinApp.Models.Dtos;

namespace LwWinApp.Data;

/// <summary>
/// 泛型仓储基类。基于 Dapper + 实体元数据自动拼 SQL，
/// 单表 CRUD 不用写一行 SQL。
///
/// 需要在查询里 JOIN 别的表时，子类覆写 <see cref="TableAlias"/>、
/// <see cref="FromClause"/>、<see cref="SelectClause"/> 三个属性即可，
/// 写操作始终指向实体自己的表，不受影响。
/// </summary>
public abstract class RepositoryBase<T> : IRepository<T> where T : class, new()
{
    private static readonly IReadOnlySet<string> NoExtraSortColumns =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    protected readonly IDatabaseFactory Db;
    protected readonly ILogger Logger;

    protected RepositoryBase(IDatabaseFactory db, ILogger logger)
    {
        Db = db;
        Logger = logger;
        Meta = TableMetadata.For<T>();
    }

    /// <summary>实体元数据（表名、主键、列映射）。</summary>
    protected TableMetadata Meta { get; }

    // ---------------------------------------------------------------
    // 子类可覆写的 SQL 片段，用于 JOIN 查询
    // ---------------------------------------------------------------

    /// <summary>表别名。设为非 null 后，生成的列引用会自动带上别名限定。</summary>
    protected virtual string? TableAlias => null;

    /// <summary>FROM 子句。默认是裸表名，子类可换成带 JOIN 的写法。</summary>
    protected virtual string FromClause
        => TableAlias is null ? $"`{Meta.TableName}`" : $"`{Meta.TableName}` {TableAlias}";

    /// <summary>SELECT 的列清单。</summary>
    protected virtual string SelectClause => TableAlias is null ? "*" : $"{TableAlias}.*";

    /// <summary>
    /// 允许参与排序的额外列（JOIN 出来的列等）。
    /// 只有在这个集合或实体真实列里的名字才能进 ORDER BY，以此挡住 SQL 注入。
    /// </summary>
    protected virtual IReadOnlySet<string> ExtraSortableColumns => NoExtraSortColumns;

    /// <summary>带别名限定的列引用。</summary>
    protected string Col(ColumnMapping column)
        => TableAlias is null ? $"`{column.ColumnName}`" : $"{TableAlias}.`{column.ColumnName}`";

    /// <summary>写入操作固定指向实体自己的表，不带 JOIN 和别名。</summary>
    private string WriteTable => $"`{Meta.TableName}`";

    // ---------------------------------------------------------------
    // 查询
    // ---------------------------------------------------------------

    public virtual async Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
    {
        var key = Meta.RequireKey();
        var sql = $"SELECT {SelectClause} FROM {FromClause} WHERE {Col(key)} = @__id LIMIT 1;";
        return await QueryFirstOrDefaultAsync(sql, new { __id = id }, ct);
    }

    public virtual Task<IReadOnlyList<T>> GetAllAsync(
        string? sortColumn = null, bool descending = false, CancellationToken ct = default)
        => QueryAsync(null, null, sortColumn, descending, null, ct);

    public virtual async Task<IReadOnlyList<T>> QueryAsync(
        string? where = null,
        object? param = null,
        string? sortColumn = null,
        bool descending = false,
        int? limit = null,
        CancellationToken ct = default)
    {
        var sql = $"SELECT {SelectClause} FROM {FromClause}"
                + BuildWhere(where)
                + BuildOrderBy(sortColumn, descending)
                + (limit is > 0 ? $" LIMIT {limit.Value}" : string.Empty)
                + ";";

        await using var conn = await Db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<T>(new CommandDefinition(sql, param, cancellationToken: ct));
        return rows.AsList();
    }

    public virtual async Task<T?> QueryFirstAsync(
        string? where = null,
        object? param = null,
        string? sortColumn = null,
        bool descending = true,
        CancellationToken ct = default)
    {
        var sql = $"SELECT {SelectClause} FROM {FromClause}"
                + BuildWhere(where)
                + BuildOrderBy(sortColumn, descending)
                + " LIMIT 1;";

        return await QueryFirstOrDefaultAsync(sql, param, ct);
    }

    public virtual async Task<long> CountAsync(
        string? where = null, object? param = null, CancellationToken ct = default)
    {
        var sql = $"SELECT COUNT(1) FROM {FromClause}{BuildWhere(where)};";
        await using var conn = await Db.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(sql, param, cancellationToken: ct));
    }

    public virtual async Task<bool> ExistsAsync(object id, CancellationToken ct = default)
    {
        var key = Meta.RequireKey();
        var sql = $"SELECT EXISTS(SELECT 1 FROM {WriteTable} WHERE `{key.ColumnName}` = @__id LIMIT 1);";
        await using var conn = await Db.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(sql, new { __id = id }, cancellationToken: ct)) == 1;
    }

    public virtual async Task<PagedResult<T>> QueryPagedAsync(
        QueryFilter filter,
        string? where = null,
        object? param = null,
        string? defaultSortColumn = null,
        CancellationToken ct = default)
    {
        var whereSql = BuildWhere(where);
        var orderSql = BuildOrderBy(
            string.IsNullOrWhiteSpace(filter.OrderBy) ? defaultSortColumn : filter.OrderBy,
            filter.Descending);

        var countSql = $"SELECT COUNT(1) FROM {FromClause}{whereSql};";
        var dataSql = $"SELECT {SelectClause} FROM {FromClause}{whereSql}{orderSql} LIMIT @__offset, @__size;";

        // 把分页参数和业务参数合并到一个参数袋里。
        var dataParams = new DynamicParameters(param);
        dataParams.Add("__offset", filter.Offset);
        dataParams.Add("__size", filter.PageSize);

        await using var conn = await Db.OpenConnectionAsync(ct);
        var total = await conn.ExecuteScalarAsync<long>(new CommandDefinition(countSql, param, cancellationToken: ct));
        var rows = await conn.QueryAsync<T>(new CommandDefinition(dataSql, dataParams, cancellationToken: ct));

        return new PagedResult<T>
        {
            Items = rows.AsList(),
            Total = total,
            PageIndex = filter.PageIndex,
            PageSize = filter.PageSize
        };
    }

    // ---------------------------------------------------------------
    // 写入
    // ---------------------------------------------------------------

    public virtual async Task<long> InsertAsync(T entity, IDbTransaction? tx = null, CancellationToken ct = default)
    {
        var columns = Meta.InsertColumns;
        if (columns.Count == 0)
            throw new InvalidOperationException($"实体 {typeof(T).Name} 没有可写入的列。");

        var sql = $"INSERT INTO {WriteTable} ({Meta.ColumnList(columns)}) VALUES ({Meta.ParameterList(columns)});";

        return await WithConnectionAsync(tx, async conn =>
        {
            await conn.ExecuteAsync(new CommandDefinition(sql, entity, transaction: tx, cancellationToken: ct));

            var key = Meta.Key;
            if (key is null || !key.IsIdentity) return 0L;

            // 必须复用同一个连接，LAST_INSERT_ID() 才是本次插入的值。
            var id = await conn.ExecuteScalarAsync<long>(
                new CommandDefinition("SELECT LAST_INSERT_ID();", transaction: tx, cancellationToken: ct));

            if (key.CanWrite && id > 0)
                key.SetValue(entity, Convert.ChangeType(id, key.PropertyType));

            return id;
        }, ct);
    }

    public virtual async Task<int> UpdateAsync(T entity, IDbTransaction? tx = null, CancellationToken ct = default)
    {
        var key = Meta.RequireKey();
        var columns = Meta.UpdateColumns;
        if (columns.Count == 0)
            throw new InvalidOperationException($"实体 {typeof(T).Name} 没有可更新的列。");

        var setClause = string.Join(", ", columns.Select(c => $"`{c.ColumnName}` = @{c.PropertyName}"));
        var sql = $"UPDATE {WriteTable} SET {setClause} WHERE `{key.ColumnName}` = @{key.PropertyName};";

        return await WithConnectionAsync(tx, async conn =>
            await conn.ExecuteAsync(new CommandDefinition(sql, entity, transaction: tx, cancellationToken: ct)), ct);
    }

    public virtual async Task<int> DeleteAsync(object id, IDbTransaction? tx = null, CancellationToken ct = default)
    {
        var key = Meta.RequireKey();
        var sql = $"DELETE FROM {WriteTable} WHERE `{key.ColumnName}` = @__id;";

        return await WithConnectionAsync(tx, async conn =>
            await conn.ExecuteAsync(new CommandDefinition(sql, new { __id = id }, transaction: tx, cancellationToken: ct)), ct);
    }

    public virtual async Task<int> DeleteWhereAsync(
        string where, object? param = null, IDbTransaction? tx = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(where))
            throw new ArgumentException("批量删除必须指定条件，拒绝全表删除。", nameof(where));

        var sql = $"DELETE FROM {WriteTable} WHERE {where};";

        return await WithConnectionAsync(tx, async conn =>
            await conn.ExecuteAsync(new CommandDefinition(sql, param, transaction: tx, cancellationToken: ct)), ct);
    }

    // ---------------------------------------------------------------
    // 内部辅助
    // ---------------------------------------------------------------

    /// <summary>执行一条返回单值的查询（子类做聚合时用）。</summary>
    protected async Task<TValue?> ScalarAsync<TValue>(
        string sql, object? param = null, IDbTransaction? tx = null, CancellationToken ct = default)
        => await WithConnectionAsync(tx, async conn =>
            await conn.ExecuteScalarAsync<TValue>(new CommandDefinition(sql, param, transaction: tx, cancellationToken: ct)), ct);

    /// <summary>
    /// 执行一条自定义查询（子类做多表关联查询时用）。
    /// 这里开放的是完全自定义的 SQL，只允许传常量字符串，不要把用户输入拼进来。
    /// </summary>
    protected async Task<IReadOnlyList<TResult>> QueryCustomAsync<TResult>(
        string sql, object? param = null, CancellationToken ct = default)
    {
        await using var conn = await Db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<TResult>(new CommandDefinition(sql, param, cancellationToken: ct));
        return rows.AsList();
    }

    protected async Task<TResult?> QueryCustomFirstAsync<TResult>(
        string sql, object? param = null, CancellationToken ct = default)
    {
        await using var conn = await Db.OpenConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<TResult>(new CommandDefinition(sql, param, cancellationToken: ct));
    }

    private async Task<T?> QueryFirstOrDefaultAsync(string sql, object? param, CancellationToken ct)
    {
        await using var conn = await Db.OpenConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<T>(new CommandDefinition(sql, param, cancellationToken: ct));
    }

    /// <summary>
    /// 事务内的调用复用事务所在连接，否则自己开一个新连接并负责释放。
    /// 不这样做的话，事务写入和自己开的新连接会变成两条独立会话。
    /// </summary>
    private async Task<TResult> WithConnectionAsync<TResult>(
        IDbTransaction? tx, Func<MySqlConnection, Task<TResult>> action, CancellationToken ct)
    {
        if (tx?.Connection is MySqlConnection transactional)
            return await action(transactional);

        await using var conn = await Db.OpenConnectionAsync(ct);
        return await action(conn);
    }

    private static string BuildWhere(string? where)
        => string.IsNullOrWhiteSpace(where) ? string.Empty : $" WHERE {where}";

    /// <summary>
    /// 拼 ORDER BY。列名必须能在实体列或子类声明的额外排序列里找到，
    /// 找不到就退回主键排序 —— 把没校验过的字符串拼进 SQL 会有注入风险。
    /// </summary>
    protected string BuildOrderBy(string? sortColumn, bool descending)
    {
        var column = ResolveSortColumn(sortColumn) ?? ResolveKeyColumn();
        if (column is null) return string.Empty;

        var order = $" ORDER BY {column} {(descending ? "DESC" : "ASC")}";

        // 排序列有重复值时（同一秒创建的一批数据很常见），MySQL 不保证
        // 稳定的行序，翻页就可能重复或漏掉记录。统一补一个主键做决定性次序。
        var keyColumn = ResolveKeyColumn();
        if (keyColumn is not null && !string.Equals(keyColumn, column, StringComparison.Ordinal))
            order += $", {keyColumn} DESC";

        return order;
    }

    private string? ResolveSortColumn(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var known = Meta.Columns.FirstOrDefault(
            c => string.Equals(c.ColumnName, name, StringComparison.OrdinalIgnoreCase));

        if (known is not null) return Col(known);

        if (ExtraSortableColumns.Contains(name))
            return TableAlias is null ? $"`{name}`" : $"{TableAlias}.`{name}`";

        Logger.LogDebug("排序列 {Column} 不在白名单内，已忽略", name);
        return null;
    }

    private string? ResolveKeyColumn()
    {
        var key = Meta.Key;
        if (key is not null) return Col(key);

        var first = Meta.Columns.FirstOrDefault();
        return first is not null ? Col(first) : null;
    }
}
