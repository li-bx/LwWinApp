namespace LwWinApp.Data;

/// <summary>
/// 指定实体对应的数据库表名。不标则用类名的 snake_case 形式。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class TableAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

/// <summary>
/// 标记主键。默认是自增列，插入时不写该列、插入后回填生成的 Id。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class KeyAttribute : Attribute
{
    /// <summary>是否数据库自增。false 表示由业务赋值（如 GUID、业务编码）。</summary>
    public bool IsIdentity { get; set; } = true;
}

/// <summary>
/// 显式指定列名，优先级高于 snake_case 约定。
/// 只有列名不符合约定时才需要标。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ColumnAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

/// <summary>
/// 不参与数据库读写。用于挂载 JOIN 出来的显示字段、UI 临时状态等。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class IgnoreAttribute : Attribute
{
}
