using ACE_Mission_Control.Core.Models;
using ACE_Mission_Control.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace ACE_Mission_Control.Helpers
{
    public class NavigationViewItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate WelcomePageTemplate { get; set; }
        public DataTemplate DroneTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return SelectTemplateCore(item);
        }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            if (item is WelcomeViewModel) return WelcomePageTemplate;
            if (item is Drone) return DroneTemplate;

            return base.SelectTemplateCore(item);
        }
    }
}
