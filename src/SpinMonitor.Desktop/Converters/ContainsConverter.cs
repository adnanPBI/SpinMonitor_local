using System;
using System.Globalization;
using System.Windows.Data;

namespace SpinMonitor.Converters
{
    /// <summary>
    /// Returns true if the bound string contains the ConverterParameter string (case-insensitive).
    /// </summary>
    public sealed class ContainsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value as string ?? string.Empty;
            var needle = parameter as string ?? string.Empty;
            return text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}