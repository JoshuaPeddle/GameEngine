using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace GameEngine.Editor.Converters
{
    public class SeverityBrushConverter : IValueConverter
    {
        public static readonly SeverityBrushConverter Instance = new();

        private static readonly IBrush Error = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
        private static readonly IBrush Warning = new SolidColorBrush(Color.FromRgb(0xE6, 0xC0, 0x7B));

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is true ? Error : Warning;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
