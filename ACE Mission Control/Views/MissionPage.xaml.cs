﻿using System;
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
using Windows.UI;
using Windows.Devices.Geolocation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace ACE_Mission_Control.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MissionPage : DroneBasePage
    { 
        private bool diagCanClose = false;

        private MissionViewModel ViewModel
        {
            get { return ViewModelLocator.Current.MissionViewModel; }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (isInit)
            {
                base.OnNavigatedTo(e);
            }
            else
            {
                Messenger.Default.Register<ShowPassphraseDialogMessage>(this, async (msg) => await PassphraseDialog.ShowAsync());

                Messenger.Default.Register<HidePassphraseDialogMessage>(this, (msg) =>
                {
                    diagCanClose = true;
                    PassphraseDialog.Hide();
                });

                Messenger.Default.Register<ShowSetupMissionDialogMessage>(this, async (msg) => await SetupMissionDialog.ShowAsync());

                Messenger.Default.Register<ShowSelectEntryDialogMessage>(this, async (msg) => await SelectEntryDialog.ShowAsync());

                Messenger.Default.Register<ScrollAlertDataGridMessage>(this, (msg) => AlertGridScrollToBottom(msg.newEntry));

                Messenger.Default.Register<AddEntryMapAreasMessage>(this, (msg) => SetMapPolygons(msg.areas));

                EntryMapControl.MapServiceToken = "Av_Cfm7_8qnq4khKZCRO5ywWQD0h2NDiuRVYZ1l2ArUEmrOM3ttdXQv6R_Wck_Lj";

                base.OnNavigatedTo(e);
            }
        }

        public MissionPage() : base()
        {
            this.InitializeComponent();

            Loaded += MissionPage_Loaded;
        }

        private void AlertGridScrollToBottom(object newItem)
        {
            AlertDataGrid.ScrollIntoView(newItem, AlertDataGrid.Columns[0]);
        }

        private void MissionPage_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void PassphraseDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            // Only allow the dialog to close if the diagCanClose is true.
            if (args.Result == ContentDialogResult.Primary)
            {
                if (diagCanClose)
                    PassphraseDialogInput.Password = "";

                args.Cancel = !diagCanClose;
                diagCanClose = false;
            }
            else
            {
                PassphraseDialogInput.Password = "";
            }
        }

        private void SetupMissionDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            Messenger.Default.Send(new SetupMissionDialogClosedMessage());
        }

        private void SetMapPolygons(object areaMsg)
        {
            List<AreaScanRoute> areaScanRoutes = (List<AreaScanRoute>)areaMsg;

            // Make layers or clear them
            if (EntryMapControl.Layers.Count == 0)
            {
                MapElementsLayer entryMapAreaLayer = new MapElementsLayer();
                EntryMapControl.Layers.Add(entryMapAreaLayer);
            }
            else
            {
                ((MapElementsLayer)EntryMapControl.Layers[0]).MapElements.Clear();
            }

            // Make the area polygons
            foreach (AreaScanRoute area in areaScanRoutes)
            {
                MapPolygon polygon = new MapPolygon();
                polygon.Path = area.Area;
                polygon.ZIndex = 1;
                polygon.StrokeColor = Colors.Orange;
                polygon.StrokeThickness = 4;
                polygon.StrokeDashed = false;
                var color = Colors.Orange;
                color.A = 100;
                polygon.FillColor = color;
                ((MapElementsLayer)EntryMapControl.Layers[0]).MapElements.Add(polygon);  
            }

            // Add point icons around the first area scan
            for (int i = 0; i < areaScanRoutes[0].Area.Positions.Count; i++)
            {
                MapIcon icon = new MapIcon();
                icon.Location = new Geopoint(areaScanRoutes[0].Area.Positions[i]);
                icon.Tag = i;
                if (i == areaScanRoutes[0].EntryVertex)
                    icon.Title = "Selected";
                ((MapElementsLayer)EntryMapControl.Layers[0]).MapElements.Add(icon);
            }

            // Centre the map
            var areaLayerElements = ((MapElementsLayer)EntryMapControl.Layers[0]).MapElements;
            if (areaLayerElements.Count > 0)
            {
                Geopoint centrePoint = new Geopoint(((MapPolygon)areaLayerElements[0]).Path.Positions[0]);
                EntryMapControl.Center = centrePoint;
            }
        }

        private void EntryMapControl_MapElementClick(MapControl sender, MapElementClickEventArgs args)
        {
            // Clear MapIcon titles
            List<MapElement> elements = ((MapElementsLayer)sender.Layers[0]).MapElements.ToList();
            foreach (MapElement element in elements)
            {
                if (element.GetType() == typeof(MapIcon))
                    ((MapIcon)element).Title = "";
            }

            // Set the selected point
            foreach (MapElement element in args.MapElements)
            {
                if (element.GetType() == typeof(MapIcon))
                {
                    ((MapIcon)element).Title = "Selected";
                    System.Diagnostics.Debug.WriteLine((int)element.Tag);
                    Messenger.Default.Send(new AddEntryMapPointSelected() { index = (int)element.Tag });
                    break;
                }
            }
        }
    }
}
