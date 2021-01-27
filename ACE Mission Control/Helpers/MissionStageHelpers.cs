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
    public class MissionStageToString : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return null;

            if (targetType != typeof(string))
                throw new InvalidCastException();

            MissionStatus.Types.Stage stageValue = (MissionStatus.Types.Stage)value;

            string resource_string = "MissionStage_" + stageValue.ToString();
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
