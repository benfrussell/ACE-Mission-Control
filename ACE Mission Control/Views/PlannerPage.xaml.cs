using ACE_Mission_Control.ViewModels;
using ACE_Mission_Control.Core.Models;
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
using GalaSoft.MvvmLight.Messaging;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI;
using Windows.Devices.Geolocation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace ACE_Mission_Control.Views
{

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PlannerPage : DroneBasePage
    {
        private static List<Color> MapColours = new List<Color>
        {
            Colors.Orange,
            Colors.DarkBlue,
            Colors.Green,
            Colors.Purple,
            Colors.Gray,
            Colors.LightBlue
        };

        private PlannerViewModel ViewModel
        {
            get { return ViewModelLocator.Current.PlannerViewModel; }
        }

        public PlannerPage() : base()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (isInit)
            {
                // Things to do on every navigation
            }
            else
            {
                EntryMapControl.Layers.Add(new MapElementsLayer());
                EntryMapControl.Layers.Add(new MapElementsLayer());

                Messenger.Default.Register<UpdatePlannerMapAreas>(this, (msg) => UpdatePlannerMapAreas());
                Messenger.Default.Register<UpdatePlannerMapPoints>(this, (msg) => UpdatePlannerMapPoints(msg.Instruction));
                EntryMapControl.MapServiceToken = "Av_Cfm7_8qnq4khKZCRO5ywWQD0h2NDiuRVYZ1l2ArUEmrOM3ttdXQv6R_Wck_Lj";
            }

            base.OnNavigatedTo(e);
        }

        private Geopath CoordsToGeopath(IEnumerable<Tuple<double, double>> coords)
        {
            List<BasicGeoposition> positions = new List<BasicGeoposition>();
            foreach (Tuple<double, double> coord in coords)
            {
                positions.Add(
                    new BasicGeoposition()
                    {
                        Longitude = (coord.Item1 / Math.PI) * 180,
                        Latitude = (coord.Item2 / Math.PI) * 180
                    });
            }
            return new Geopath(positions);
        }

        private Geopoint CoordToGeopoint(Tuple<double, double> coord)
        {
            return new Geopoint(
                new BasicGeoposition
                {
                    Longitude = (coord.Item1 / Math.PI) * 180,
                    Latitude = (coord.Item2 / Math.PI) * 180
                });
        }

        private void UpdatePlannerMapAreas()
        {
            // Clear elements
            ((MapElementsLayer)EntryMapControl.Layers[0]).MapElements.Clear();

            var colorIndex = 0;
            // Make the area polygons
            foreach (TreatmentInstruction instruction in ViewModel.TreatmentInstructions)
            {
                var colour = MapColours[colorIndex % MapColours.Count];
                MapPolygon polygon = new MapPolygon();
                polygon.Path = CoordsToGeopath(instruction.TreatmentPolygon.GetBasicCoordinates());
                polygon.ZIndex = 1;
                polygon.StrokeColor = colour;
                polygon.StrokeThickness = 4;
                polygon.StrokeDashed = false;
                colour.A = 100;
                polygon.FillColor = colour;
                ((MapElementsLayer)EntryMapControl.Layers[0]).MapElements.Add(polygon);

                colorIndex++;
            }

            // Centre the map
            var areaLayerElements = ((MapElementsLayer)EntryMapControl.Layers[0]).MapElements;
            if (areaLayerElements.Count > 0)
            {
                Geopoint centrePoint = new Geopoint(((MapPolygon)areaLayerElements[0]).Path.Positions[0]);
                EntryMapControl.Center = centrePoint;
            }
        }

        private void UpdatePlannerMapPoints(TreatmentInstruction instruction)
        {
            if (instruction == null)
                return;

            // Clear elements
            var pointsToRemove = ((MapElementsLayer)EntryMapControl.Layers[1]).MapElements.Where(e => (int)e.Tag == instruction.TreatmentPolygon.Id).ToList();
            foreach (MapElement element in pointsToRemove)
                ((MapElementsLayer)EntryMapControl.Layers[1]).MapElements.Remove(element);

            // Add Unlock and Lock icons
            if (instruction.PayloadUnlockCoordinate != null)
            {
                MapIcon unlockIcon = new MapIcon();
                unlockIcon.Location = CoordToGeopoint(instruction.PayloadUnlockCoordinate);
                unlockIcon.Tag = instruction.TreatmentPolygon.Id;
                ((MapElementsLayer)EntryMapControl.Layers[1]).MapElements.Add(unlockIcon);
            }

            if (instruction.PayloadLockCoordinate != null)
            {
                MapIcon lockIcon = new MapIcon();
                lockIcon.Location = CoordToGeopoint(instruction.PayloadLockCoordinate);
                lockIcon.Tag = instruction.TreatmentPolygon.Id;
                ((MapElementsLayer)EntryMapControl.Layers[1]).MapElements.Add(lockIcon);
            }
        }
    }
}
