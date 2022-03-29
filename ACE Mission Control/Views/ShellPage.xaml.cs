using System;

using ACE_Mission_Control.ViewModels;
using GalaSoft.MvvmLight.Messaging;
using Windows.UI.Xaml.Controls;

namespace ACE_Mission_Control.Views
{
    // TODO WTS: Change the icons and titles for all NavigationViewItems in ShellPage.xaml.
    public sealed partial class ShellPage : Page
    {
        private ShellViewModel ViewModel
        {
            get { return ViewModelLocator.Current.ShellViewModel; }
        }

        public ShellPage()
        {
            InitializeComponent();
            DataContext = ViewModel;
            ViewModel.Initialize(ContentFrame, navigationView, KeyboardAccelerators);

            Messenger.Default.Register<ScrollAlertDataGridMessage>(this, (msg) => AlertGridScrollToBottom(msg.newEntry));
            Messenger.Default.Register<AlertDataGridSizeChangeMessage>(this, (msg) => AlertDataGridSizeChange());
        }

        private void AlertGridScrollToBottom(object newItem)
        {
            AlertDataGrid.ScrollIntoView(newItem, AlertDataGrid.Columns[0]);
        }

        private void AlertDataGridSizeChange()
        {
            AlertDataGrid.UpdateLayout();

            //scroller.ChangeView
        }
    }
}
