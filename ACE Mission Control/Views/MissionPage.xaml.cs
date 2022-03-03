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
using ACE_Mission_Control.ViewModels;
using ACE_Mission_Control.Core.Models;
using GalaSoft.MvvmLight.Messaging;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls.Maps;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace ACE_Mission_Control.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MissionPage : DroneBasePage
    {
        private MissionViewModel ViewModel
        {
            get { return ViewModelLocator.Current.MissionViewModel; }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (isInit)
            {
                // Things to do on every navigation
            }
            else
            {
            }
            base.OnNavigatedTo(e);

            Messenger.Default.Register<StopMapActionsMessage>(this, (msg) => StopMapActions());
        }

        public MissionPage() : base()
        {
            this.InitializeComponent();

            Loaded += MissionPage_Loaded;
        }

        private void MissionPage_Loaded(object sender, RoutedEventArgs e)
        {
            
        }

        private void StopMapActions()
        {
            PlannerGrid.Children.Remove(EntryMapControl);
            var map = CreateMapControl(ViewModel.MapLayers, ViewModel.MapCentre);
            
            PlannerGrid.Children.Add(map);
        }

        private MapControl CreateMapControl(IList<MapLayer> layers, Windows.Devices.Geolocation.Geopoint point)
        {
            var map = new MapControl();
            map.SetValue(Grid.ColumnProperty, 1);
            map.SetValue(Grid.RowProperty, 0);
            map.SetValue(Grid.RowSpanProperty, 2);
            map.Margin = new Thickness(4, 8, 8, 8);
            map.HorizontalAlignment = HorizontalAlignment.Stretch;
            map.VerticalAlignment = VerticalAlignment.Stretch;
            map.ZoomLevel = 15;
            //map.Layers = layers;
            map.Center = point;
            map.Style = MapStyle.AerialWithRoads;
            map.MapServiceToken = "4l0tkAAsXEnnNSOK8679~ik4pAeyowqts0oKyLzbkjg~As5-mUredKyQUQkVkC2zR9xB2hFgDWVfvp_OP7MbrTfQbF4goOfA-Sfa3nSj1EJs";
            return map;
        }
    }
}
