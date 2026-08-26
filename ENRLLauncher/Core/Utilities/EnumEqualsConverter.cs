using System.Globalization;
using System.Windows.Data;

namespace ENRLLauncher.Core.Utilities
{
    //from https://github.com/ibroomallaz/Desktop-Support/blob/MVVM/DSAMVVM/Core/Utilities/EnumEqualsConverter.cs
    public class EnumEqualsConverter : IValueConverter
    {
        public EnumEqualsConverter() { }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value != null && parameter != null && value.Equals(parameter);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b && parameter != null ? parameter : Binding.DoNothing;
    }
}