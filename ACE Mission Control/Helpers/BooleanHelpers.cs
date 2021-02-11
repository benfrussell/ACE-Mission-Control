using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace ACE_Mission_Control.Helpers
{
    public class NegateBoolean : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return null;

            // Handle this case because I don't know how to pull up Windows' bool -> Visibility converter generically
            if (targetType == typeof(Visibility))
                return (bool)value ? Visibility.Collapsed : Visibility.Visible;

            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class ErrorBooleanToColour : IValueConverter
    {
        // False returns a normal colour, True returns an error colour
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return null;

            if (targetType != typeof(Brush))
                throw new InvalidCastException();

            if (!(bool)value)
                return new SolidColorBrush((Color)Application.Current.Resources["SystemBaseMediumHighColor"]);
            else
                return new SolidColorBrush((Color)Application.Current.Resources["SystemErrorTextColor"]);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
