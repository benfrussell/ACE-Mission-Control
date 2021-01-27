using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;
using ACE_Mission_Control.Core.Models;
using Pbdrone;
using Windows.UI;
using Windows.UI.Xaml.Media;

namespace ACE_Mission_Control.Helpers
{
    public class MissionActivatedToString : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return null;

            if (targetType != typeof(string))
                throw new InvalidCastException();

            bool activated  = (bool)value;

            string resource_string = "MissionActivated_" + activated.ToString();
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

    public class MissionActivatedToColour : IValueConverter
    {
        public static SolidColorBrush Red = new SolidColorBrush(Colors.OrangeRed);
        public static SolidColorBrush Green = new SolidColorBrush(Colors.ForestGreen);
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return null;

            if (targetType != typeof(Brush))
                throw new InvalidCastException();

            bool activated = (bool)value;

            if (activated)
                return Green;

            return Red;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
