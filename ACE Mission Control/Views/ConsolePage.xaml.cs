using ACE_Mission_Control.ViewModels;
using GalaSoft.MvvmLight.Messaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace ACE_Mission_Control.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ConsolePage : DroneBasePage
    {
        private ConsoleViewModel ViewModel
        {
            get { return ViewModelLocator.Current.ConsoleViewModel; }
        }
        public ConsolePage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (isInit)
            {
                base.OnNavigatedTo(e);
            }
            else
            {
                Messenger.Default.Register<ScrollToConsoleEndMessage>(this, (msg) => ConsoleScrollViewer.ChangeView(null, ConsoleScrollViewer.ScrollableHeight, null));

                base.OnNavigatedTo(e);
            }
        }
    }
}
