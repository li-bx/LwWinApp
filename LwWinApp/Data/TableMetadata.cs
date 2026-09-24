using System.Collections.Concurrent;
using System.Reflection;
using LwWinApp.Utils;

namespace LwWinApp.Data;

/// <summary>单个属性到数据库列的映射信息。</summary>
public sealed class ColumnMapping
{
    public required string PropertyName { get; init; }
    public required string ColumnName { get; init; }
    public required Type PropertyType { get; init; }
    public required PropertyInfo Property { get; init; }
    public bool IsKey { get; init; }

    /// <summary>是否数据库自增（插入时不写这一列）。</summary>
    public bool IsIdentity { get; init; }

    /// <summary>可写（排除只读计算属性）。</summary>
    public bool CanWrite { get; init; }

    public object? GetValue(object entity) => Property.GetValue(entity);

    public void SetValue(object entity, object? value)
    {
        if (!CanWrite) return;
        Property.SetValue(entity, value);
    }
}

/// <summary>
/// 实体的表结构元数据。反射只做一次并缓存，
/// 之后每次查询都直接复用，不在热路径上重复反射。
/// </summary>
public sealed class TableMetadata
{
    private static readonly ConcurrentDictionary<Type, TableMetadata> Cache = new();

    public Type EntityType { get; }
    public string TableName { get; }

    /// <summary>主键。实体没有主键时为 null（此时只能用查询类方法）。</summary>
    public ColumnMapping? Key { get; }

    /// <summary>所有映射到数据库列的属性。</summary>
    public IReadOnlyList<ColumnMapping> Columns { get; }

    /// <summary>INSERT 时要写的列（排除自增主键）。</summary>
    public IReadOnlyList<ColumnMapping> InsertColumns { get; }

    /// <summary>UPDATE 时要写的列（排除主键）。</summary>
    public IReadOnlyList<ColumnMapping> UpdateColumns { get; }

    private TableMetadata(Type entityType)
    {
        EntityType = entityType;

        var tableAttr = entityType.GetCustomAttribute<TableAttribute>();
        TableName = tableAttr?.Name ?? StringHelper.ToSnakeCase(entityType.Name);

        var columns = new List<ColumnMapping>();
        foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetCustomAttribute<IgnoreAttribute>() is not null) continue;

            // 只读计算属性（如 GenderText）不参与数据库读写。
            var canWrite = prop.CanWrite && prop.GetSetMethod() is not null;
            if (!canWrite && prop.GetCustomAttribute<KeyAttribute>() is null) continue;

            var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
            var keyAttr = prop.GetCustomAttribute<KeyAttribute>();

            columns.Add(new ColumnMapping
            {
                PropertyName = prop.Name,
                ColumnName = colAttr?.Name ?? StringHelper.ToSnakeCase(prop.Name),
                PropertyType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType,
                Property = prop,
                CanWrite = canWrite,
                IsKey = keyAttr is not null,
                IsIdentity = keyAttr?.IsIdentity ?? false
            });
        }

        // 主键解析顺序：显式 [Key] → 名为 Id 的属性 → 没有主键
        var key = columns.FirstOrDefault(c => c.IsKey)
                  ?? columns.FirstOrDefault(c => string.Equals(c.PropertyName, "Id", StringComparison.OrdinalIgnoreCase));

        if (key is not null && !key.IsKey)
        {
            // 靠命名约定认出来的主键，默认当作自增列。
            key = new ColumnMapping
            {
                PropertyName = key.PropertyName,
                ColumnName = key.ColumnName,
                PropertyType = key.PropertyType,
                Property = key.Property,
                CanWrite = key.CanWrite,
                IsKey = true,
                IsIdentity = true
            };
            var idx = columns.FindIndex(c => c.PropertyName == key.PropertyName);
            columns[idx] = key;
        }

        Key = key;
        Columns = columns;
        InsertColumns = columns.Where(c => !(c.IsKey && c.IsIdentity)).ToList();
        UpdateColumns = columns.Where(c => !c.IsKey).ToList();
    }

    public static TableMetadata For<T>() => For(typeof(T));

    public static TableMetadata For(Type type) => Cache.GetOrAdd(type, t => new TableMetadata(t));

    /// <summary>取主键，没有则抛出可读的异常而不是空引用。</summary>
    public ColumnMapping RequireKey()
        => Key ?? throw new InvalidOperationException(
            $"实体 {EntityType.Name} 没有主键，无法按 Id 操作。请给某个属性加 [Key]，或命名为 Id。");

    /// <summary>`col1`, `col2` 形式的列清单。</summary>
    public string ColumnList(IEnumerable<ColumnMapping> columns)
        => string.Join(", ", columns.Select(c => $"`{c.ColumnName}`"));

    /// <summary>@Prop1, @Prop2 形式的参数清单。</summary>
    public string ParameterList(IEnumerable<ColumnMapping> columns)
        => string.Join(", ", columns.Select(c => "@" + c.PropertyName));
}
