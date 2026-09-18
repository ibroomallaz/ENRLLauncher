using System.Globalization;
using System.Windows.Data;

namespace ENRLLauncher.Core.Utilities
{

    public class EnumEqualsConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value != null && parameter != null && value.Equals(parameter);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool and true && parameter != null ? parameter : Binding.DoNothing;
    }
}