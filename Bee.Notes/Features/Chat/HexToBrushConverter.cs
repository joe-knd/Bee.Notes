using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfNotes.Features.Chat;

/// <summary>
/// Converts a hex color string to a <see cref="SolidColorBrush"/> with optional alpha.
/// Pass <c>"solid"</c> as the converter parameter for full opacity; otherwise a translucent brush is returned.
/// </summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        byte alpha = parameter is string s && s == "solid" ? (byte)255 : (byte)60;
        if (value is string hex && hex.Length >= 4)
        {
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
            }
            catch { }
        }
        return new SolidColorBrush(Color.FromArgb(30, 128, 128, 128));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
