using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace GameEngine.Editor.Converters
{
    public class GridCellSizeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double actualSize)
            {
                // Assuming the grid is square, calculate the size of each cell
                return actualSize / 10; // 10 is the number of columns or rows
            }
            return 0;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}