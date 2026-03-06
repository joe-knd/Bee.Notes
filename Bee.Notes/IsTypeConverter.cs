using System.Globalization;
using System.Windows.Data;

namespace WpfNotes;

/// <summary>
/// Value converter that returns <see langword="true"/> when the value is an instance of the specified <see cref="Type"/> parameter.
/// </summary>
public sealed class IsTypeConverter : IValueConverter
{
    public static readonly IsTypeConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is Type type && type.IsInstanceOfType(value);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
