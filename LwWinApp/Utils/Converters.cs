using Microsoft.UI.Xaml.Data;

namespace LwWinApp.Utils;

/// <summary>
/// 绑定值转换器集合。
///
/// 只放「到处都要用、而且语义没有歧义」的几种。带业务含义的转换
/// （比如「状态 → 颜色」）不要做成转换器：转换器拿不到上下文、没法写测试、
/// 出问题只能在界面上一处处找。那种情况在 ViewModel 里算好一个属性更省事。
///
/// 在 App.xaml 里注册一次，页面直接用 {StaticResource ...} 引用。
/// </summary>
public sealed class BoolNegationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool flag && !flag;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is bool flag && !flag;
}

/// <summary>true → Visible，false → Collapsed。</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool flag && flag ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility visibility && visibility == Visibility.Visible;
}

/// <summary>true → Collapsed，false → Visible。给「没有数据时才显示的提示」用。</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool flag && flag ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException("这个转换器只用于单向绑定。");
}

/// <summary>字符串非空 → Visible。用来在有值时显示错误提示、在没值时隐藏。</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException("这个转换器只用于单向绑定。");
}

/// <summary>字符串为空 → Visible。空列表提示就用它。</summary>
public sealed class EmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException("这个转换器只用于单向绑定。");
}

/// <summary>集合为空 → Visible。列表为空时的占位提示用它。</summary>
public sealed class EmptyCollectionToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is System.Collections.ICollection { Count: > 0 } ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException("这个转换器只用于单向绑定。");
}

/// <summary>枚举值等于参数时 → Visible。用来在同一块区域里按状态切换显示内容。</summary>
public sealed class EnumEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is null || parameter is null) return Visibility.Collapsed;

        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException("这个转换器只用于单向绑定。");
}
