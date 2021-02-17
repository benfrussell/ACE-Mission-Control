using Microsoft.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;

namespace ACE_Mission_Control.Behaviors
{
    public class ToggleFlyoutAction : DependencyObject, IAction
    {
        public object Execute(object sender, object parameter)
        {
            var flyout = FlyoutBase.GetAttachedFlyout((FrameworkElement)sender);
            if (flyout.IsOpen)
                flyout.Hide();
            else
                FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);

            return null;
        }
    }

    public class OpenFlyoutAction : DependencyObject, IAction
    {
        public object Execute(object sender, object parameter)
        {
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);

            return null;
        }
    }

    public class CloseFlyoutAction : DependencyObject, IAction
    {
        public object Execute(object sender, object parameter)
        {
            FlyoutBase.GetAttachedFlyout((FrameworkElement)sender).Hide();

            return null;
        }
    }
}
