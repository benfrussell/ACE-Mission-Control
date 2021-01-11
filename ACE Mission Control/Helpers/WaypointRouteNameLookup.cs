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
    public class WaypointRouteNameLookup : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
                return null;

            //if (targetType != typeof(string))
            //    throw new InvalidCastException();

            List<int> routeIDs;
            int routeID;

            try
            {
                routeIDs = (List<int>)value;
                return routeIDs.ConvertAll(id => MissionData.WaypointRoutes.First(i => i.ID == id).Name);
            }
            catch (InvalidCastException)
            {
                routeID = (int)value;
                return MissionData.WaypointRoutes.First(i => i.ID == routeID).Name;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
