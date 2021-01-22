using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;
using ACE_Mission_Control.Core.Models;
using Windows.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.Xaml;

namespace ACE_Mission_Control.Helpers
{
    public class AlertLevelToColour : IValueConverter
    {
        public static SolidColorBrush RedAlert = new SolidColorBrush(Color.FromArgb(255, 115, 38, 38));
        public static SolidColorBrush YellowAlert = new SolidColorBrush(Color.FromArgb(255, 115, 96, 38));
        public static SolidColorBrush StandardAlert = new SolidColorBrush((Color)Application.Current.Resources["SystemBaseLowColor"]);
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return null;

            if (targetType != typeof(Brush))
                throw new InvalidCastException();

            AlertEntry.AlertLevel alertValue = (AlertEntry.AlertLevel)value;

            switch (alertValue)
            {
                case AlertEntry.AlertLevel.None:
                    return null;
                case AlertEntry.AlertLevel.Info:
                    return StandardAlert;
                case AlertEntry.AlertLevel.Medium:
                    return YellowAlert;
                case AlertEntry.AlertLevel.High:
                    return RedAlert;
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
