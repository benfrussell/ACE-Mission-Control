using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace ACE_Mission_Control.Helpers
{
    public class EnumToRYGColour : IValueConverter
    {
        public static SolidColorBrush Red = new SolidColorBrush(Colors.OrangeRed);
        public static SolidColorBrush Yellow = new SolidColorBrush(Colors.Yellow);
        public static SolidColorBrush Green = new SolidColorBrush(Colors.ForestGreen);

        // First value is red, last value is green, in-between is yellow
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return null;

            if (targetType != typeof(Brush))
                throw new InvalidCastException();

            IEnumerable<int> enumInts = Enum.GetValues(value.GetType()).Cast<int>();
            int enumValue = (int)value;

            if (enumValue == enumInts.First())
                return Red;
            else if (enumValue == enumInts.Last())
                return Green;
            else
                return Yellow;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class EnumToResourceString : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return null;

            if (targetType != typeof(string))
                throw new InvalidCastException();

            Enum statusValue = (Enum)value;

            string resource_string = value.GetType().Name + "_" + statusValue.ToString();
            string converted = resource_string.GetLocalized();

            if (converted.Length == 0)
                converted = resource_string;

            return converted;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
