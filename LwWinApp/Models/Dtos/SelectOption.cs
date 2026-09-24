namespace LwWinApp.Models.Dtos;

/// <summary>
/// 下拉框的一个选项。
///
/// Value 用 object 而不是泛型，是为了让所有下拉框在 XAML 里长得一样：
/// <c>ItemsSource="{x:Bind ViewModel.StatusOptions}"</c> + <c>SelectedItem</c> 双向绑定，
/// 不需要每个页面各自定义一个选项类型，也不需要写值转换器。
/// 取值时用 <c>Value is long id</c> 这种模式匹配即可。
/// </summary>
public sealed class SelectOption
{
    public SelectOption(string text, object? value = null)
    {
        Text = text;
        Value = value;
    }

    /// <summary>显示文本。</summary>
    public string Text { get; }

    /// <summary>实际值。null 表示「不限」。</summary>
    public object? Value { get; }

    /// <summary>「全部 / 不限」这种不筛选的选项。</summary>
    public static SelectOption All(string text = "全部") => new(text, null);

    public override string ToString() => Text;
}

/// <summary>
/// 取值辅助方法。刻意做成扩展方法、且接收者声明为可空：
///
/// <c>SelectedStatus</c> 这类绑定字段随时可能是 null —— 双向绑定的 SelectedItem 在
/// ItemsSource 被整体替换时会先被清成 null，列表异步加载完成前也是 null。
/// 写成实例方法就必须在每个调用点记得加 <c>?</c>，漏一个就是运行时 NullReferenceException；
/// 写成可空接收者的扩展方法，则「取不到值返回 null」由类型系统保证，
/// 调用点统一写成 <c>x.AsEnum&lt;T&gt;()</c> 即可。
///
/// 注意：扩展方法不能用 <c>?.</c> 调用（<c>?.</c> 只做成员查找，不看扩展方法），
/// 所以调用点不要再写成 <c>x?.AsEnum&lt;T&gt;()</c>。
/// </summary>
public static class SelectOptionExtensions
{
    /// <summary>取 long 型的值（部门 Id、角色 Id 等），取不到返回 null。</summary>
    public static long? AsLong(this SelectOption? option) => option?.Value is long value ? value : null;

    /// <summary>取枚举值，取不到返回 null。</summary>
    public static TEnum? AsEnum<TEnum>(this SelectOption? option) where TEnum : struct, Enum
        => option?.Value is TEnum value ? value : null;
}
