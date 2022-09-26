using ACE_Mission_Control.Core.Models;
using ACE_Mission_Control.ViewModels;
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
    public sealed partial class FlightTimePage : Page
    {
        private FlightTimeViewModel ViewModel
        {
            get { return ViewModelLocator.Current.FlightTimeViewModel; }
        }

        public FlightTimePage()
        {
            this.InitializeComponent();
        }

        private void DataGrid_LoadingRowGroup(object sender, Microsoft.Toolkit.Uwp.UI.Controls.DataGridRowGroupHeaderEventArgs e)
        {
            ICollectionViewGroup group = e.RowGroupHeader.CollectionViewGroup;
            IFlightTimeEntry item = group.GroupItems[0] as IFlightTimeEntry;
            e.RowGroupHeader.PropertyValue = ViewModel.MachineColumnsVisible ? item.Machine : item.Pilot;
        }
    }
}
