using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;
using ACE_Mission_Control.Core.Models;

namespace ACE_Mission_Control.Helpers
{
    public class AlertToString : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return null;

            if (targetType != typeof(string))
                throw new InvalidCastException();
            AlertEntry alertValue = (AlertEntry)value;

            string resource_string = "Alert_" + alertValue.Type.ToString();
            string converted = resource_string.GetLocalized();
            if (converted.Length == 0)
                converted = resource_string;

            if (alertValue.Info != null && alertValue.Info.Length > 0)
                return converted + " " + alertValue.Info;
            else
                return converted;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
