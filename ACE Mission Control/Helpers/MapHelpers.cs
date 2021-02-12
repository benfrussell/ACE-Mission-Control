using ACE_Mission_Control.Core.Models;
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
    public class InstructionNumberToColour : IValueConverter
    {
        private static List<Color> MapColours = new List<Color>
        {
            Colors.Purple,
            Colors.Blue,
            Colors.Green,
            Colors.Yellow,
            Colors.Orange,
            Colors.Red
        };

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return null;

            if (targetType != typeof(Brush) && targetType != typeof(Color))
                throw new InvalidCastException();

            var num = (int?)value;

            var colour = num != null ? MapColours[(int)num % (MapColours.Count - 1)] : Colors.Gray;

            if (targetType == typeof(Brush))
                return new SolidColorBrush(colour);
            else
                return colour;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
