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
    public class AreaStatusToString : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return null;

            if (targetType != typeof(string))
                throw new InvalidCastException();

            AreaResult.Types.Status statusValue = (AreaResult.Types.Status)value;

            string resource_string = "AreaStatus_" + statusValue.ToString();
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

    public class AreaStatusToColour : IValueConverter
    {
        public static SolidColorBrush Red = new SolidColorBrush(Colors.OrangeRed);
        public static SolidColorBrush Yellow = new SolidColorBrush(Colors.Yellow);
        public static SolidColorBrush Green = new SolidColorBrush(Colors.ForestGreen);
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return null;

            if (targetType != typeof(Brush))
                throw new InvalidCastException();

            AreaResult.Types.Status statusValue = (AreaResult.Types.Status)value;

            switch (statusValue)
            {
                case AreaResult.Types.Status.NotStarted:
                    return Red;
                case AreaResult.Types.Status.InProgress:
                    return Yellow;
                case AreaResult.Types.Status.Finished:
                    return Green;
                default:
                    return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
