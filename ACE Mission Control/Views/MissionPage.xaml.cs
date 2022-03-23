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
using Windows.Storage.Streams;
using Windows.UI;
using Windows.Devices.Geolocation;
using ACE_Mission_Control.Helpers;
using System.Collections.ObjectModel;
using Windows.Media.SpeechSynthesis;
using Windows.Globalization;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace ACE_Mission_Control.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MissionPage : DroneBasePage
    {
        private MapControl mapControl;
        private bool mapCentred;
        private SpeechSynthesizer synth;
        private Queue<string> speechQueue;

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

            RemakeMapControl();
            Messenger.Default.Register<RemakeMapMessage>(this, (msg) => RemakeMapControl());
            Messenger.Default.Register<SetMapPointsMessage>(this, (msg) => UpdatePlannerMapPoints(msg.Instructions));
            Messenger.Default.Register<SetMapPolygonsMessage>(this, (msg) => UpdatePlannerMapAreas(msg.Instructions));
            Messenger.Default.Register<SpeakMessage>(this, (msg) => Speak(msg.Message));
        }

        public MissionPage() : base()
        {
            this.InitializeComponent();

            Loaded += MissionPage_Loaded;
            speechQueue = new Queue<string>();

            synth = new SpeechSynthesizer();
            synth.Options.SpeakingRate = 1.05;
            var setVoice = SpeechSynthesizer.AllVoices.FirstOrDefault(v => v.Language == ApplicationLanguages.PrimaryLanguageOverride);
            if (setVoice != null)
                synth.Voice = setVoice;
        }

        private void MissionPage_Loaded(object sender, RoutedEventArgs e)
        {
            mapCentred = false;
            SpeechMediaElement.MediaEnded += SpeechMediaElement_MediaEnded;
        }

        private void SpeechMediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (speechQueue.Count > 0)
                Speak(speechQueue.Dequeue());
        }

        private async void Speak(string text)
        {
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (settings.Values.Keys.Contains("UseVoice") && !(bool)settings.Values["UseVoice"])
                return;

            if (SpeechMediaElement.CurrentState == MediaElementState.Playing)
            {
                speechQueue.Enqueue(text);
                return;
            }

            SpeechSynthesisStream stream = await synth.SynthesizeTextToStreamAsync(text);
            // Send the stream to the media object.
            SpeechMediaElement.SetSource(stream, stream.ContentType);
            SpeechMediaElement.Play();
        }

        private void RemakeMapControl()
        {
            var map = new MapControl();
            map.SetValue(Grid.ColumnProperty, 1);
            map.SetValue(Grid.RowProperty, 0);
            map.SetValue(Grid.RowSpanProperty, 2);
            map.Margin = new Thickness(4, 8, 8, 8);
            map.HorizontalAlignment = HorizontalAlignment.Stretch;
            map.VerticalAlignment = VerticalAlignment.Stretch;
            map.Style = MapStyle.AerialWithRoads;
            map.MapServiceToken = "4l0tkAAsXEnnNSOK8679~ik4pAeyowqts0oKyLzbkjg~As5-mUredKyQUQkVkC2zR9xB2hFgDWVfvp_OP7MbrTfQbF4goOfA-Sfa3nSj1EJs";
            map.MapElementClick += Map_MapElementClick;
            map.Layers = new ObservableCollection<MapLayer>();

            if (PlannerGrid.Children.Contains(mapControl))
            {
                map.ZoomLevel = mapControl.ZoomLevel;
                map.Center = mapControl.Center;
                foreach (MapElementsLayer layer in mapControl.Layers)
                {
                    var layerCopy = new MapElementsLayer();
                    foreach (MapElement element in layer.MapElements)
                        layerCopy.MapElements.Add(element);
                    map.Layers.Add(layerCopy);
                }
                PlannerGrid.Children.Remove(mapControl);
            }
            else
            {
                map.ZoomLevel = 15;
                map.Center = new Geopoint(new BasicGeoposition() { Latitude = 46.352215, Longitude = -72.537158, Altitude = 0 });
            }

            mapControl = map;
            PlannerGrid.Children.Add(mapControl);
        }

        private void Map_MapElementClick(MapControl sender, MapElementClickEventArgs args)
        {
            if (ViewModel.AttachedDrone.Mission.StartMode != StartTreatmentParameters.Mode.SelectedWaypoint)
                return;

            foreach (MapElement element in args.MapElements)
            {
                if (args.MapElements[0].Tag == null)
                    continue;

                ViewModel.AttachedDrone.Mission.SetSelectedStartWaypoint(args.MapElements[0].Tag as string);
                break;
            }
        }

        private static RandomAccessStreamReference StartImage = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/Start.png"));
        private static RandomAccessStreamReference StopImage = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/Stop.png"));
        private static RandomAccessStreamReference NextImage = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/Next.png"));
        private static RandomAccessStreamReference FlagImage = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/Flag.png"));
        private static RandomAccessStreamReference PointImage = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/Point.png"));

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

        // Input is in radians and output is in degrees
        private Geopoint CoordToGeopoint(double longitude, double latitude)
        {
            return new Geopoint(
                new BasicGeoposition
                {
                    Longitude = (longitude / Math.PI) * 180,
                    Latitude = (latitude / Math.PI) * 180
                });
        }

        private void AddMapLayersUntilIndex(int index)
        {
            var layersToAdd = (index + 1) - mapControl.Layers.Count();

            if (layersToAdd <= 0)
                return;

            // Add map layers until it reaches the index
            for (int i = 0; i < layersToAdd; i++)
                mapControl.Layers.Add(new MapElementsLayer());
        }

        private void UpdatePlannerMapAreas(IEnumerable<ITreatmentInstruction> instructions)
        {
            foreach (ITreatmentInstruction instruction in instructions)
            {
                if (instruction == null)
                    continue;

                var layerIndex = instruction.ID * 2;

                // Add the layer or clear elements at the existing layer
                if (mapControl.Layers.ElementAtOrDefault(layerIndex) == null)
                    AddMapLayersUntilIndex(layerIndex);
                else
                    ((MapElementsLayer)mapControl.Layers[layerIndex]).MapElements.Clear();

                if (!instruction.Enabled)
                    continue;

                Color colour = (Color)new InstructionNumberToColour().Convert(instruction.ID, typeof(Color), null, null);

                if (instruction.FirstInstruction && instruction.IsTreatmentRouteValid())
                {
                    MapPolyline polyline = new MapPolyline();
                    polyline.Path = CoordsToGeopath(instruction.TreatmentRoute.GetBasicCoordinates());
                    polyline.ZIndex = 2;
                    polyline.StrokeColor = colour;
                    polyline.StrokeThickness = 2;
                    polyline.StrokeDashed = true;
                    ((MapElementsLayer)mapControl.Layers[layerIndex]).MapElements.Add(polyline);
                }

                MapPolygon polygon = new MapPolygon();
                polygon.Path = CoordsToGeopath(instruction.TreatmentPolygon.GetBasicCoordinates());
                polygon.ZIndex = 1;
                polygon.StrokeColor = colour;
                polygon.StrokeThickness = 3;
                polygon.StrokeDashed = false;
                colour.A = 60;
                polygon.FillColor = colour;
                ((MapElementsLayer)mapControl.Layers[layerIndex]).MapElements.Add(polygon);
            }
        }

        private void UpdatePlannerMapPoints(IEnumerable<ITreatmentInstruction> instructions)
        {
            foreach (ITreatmentInstruction instruction in instructions)
            {
                var layerIndex = (instruction.ID * 2) + 1;

                // Add the layer or clear elements at the existing layer
                if (mapControl.Layers.ElementAtOrDefault(layerIndex) == null)
                    AddMapLayersUntilIndex(layerIndex);
                else
                    ((MapElementsLayer)mapControl.Layers[layerIndex]).MapElements.Clear();

                if (!instruction.Enabled)
                    continue;

                var layer = (MapElementsLayer)mapControl.Layers[layerIndex];

                MapIcon startIcon = new MapIcon();

                if (instruction.FirstInstruction)
                {
                    // Add a MapIcon for the last position if there is a last position
                    if (ViewModel.AttachedDrone.Mission.LastPosition != null)
                    {
                        MapIcon lastPosIcon = new MapIcon();
                        lastPosIcon.Location = CoordToGeopoint(
                            ViewModel.AttachedDrone.Mission.LastPosition.X,
                            ViewModel.AttachedDrone.Mission.LastPosition.Y);
                        lastPosIcon.Image = FlagImage;
                        lastPosIcon.NormalizedAnchorPoint = new Point(0.15, 1);
                        lastPosIcon.Title = "Planner_MapLastPositionLabel".GetLocalized();
                        lastPosIcon.ZIndex = -1;
                        layer.MapElements.Add(lastPosIcon);
                    }

                    // Add a MapIcon for each waypoint in the first instruction's route if in SelectedWaypoint mode
                    if (ViewModel.AttachedDrone.Mission.StartMode == StartTreatmentParameters.Mode.SelectedWaypoint)
                    {
                        foreach (Waypoint idCoord in instruction.TreatmentRoute.Waypoints)
                        {
                            MapIcon waypointIcon = new MapIcon();
                            waypointIcon.Location = CoordToGeopoint(idCoord.Coordinate.X, idCoord.Coordinate.Y);
                            waypointIcon.Image = PointImage;
                            waypointIcon.Tag = idCoord.ID;
                            waypointIcon.ZIndex = -2;
                            layer.MapElements.Add(waypointIcon);
                        }
                    }

                    // Add a MapIcon for the starting point and each area entry point that follows
                    var startCoord = ViewModel.AttachedDrone.Mission.GetStartCoordinate();
                    if (startCoord != null)
                    {
                        var startGeopoint = CoordToGeopoint(startCoord.X, startCoord.Y);
                        startIcon.Location = startGeopoint;

                        if (!mapCentred)
                        {
                            mapControl.Center = startGeopoint;
                            mapCentred = true;
                        }
                    }
                }
                else
                {
                    var startCoord = ViewModel.AttachedDrone.Mission.GetStartCoordinate(instruction.ID);
                    startIcon.Location = CoordToGeopoint(startCoord.X, startCoord.Y);
                }

                startIcon.Image = StartImage;
                startIcon.Title = instruction.Name;
                layer.MapElements.Add(startIcon);

                // Add a MapIcon for the exit point for each area
                MapIcon stopIcon = new MapIcon();
                var stopCoord = ViewModel.AttachedDrone.Mission.GetStopCoordinate(instruction.ID);
                stopIcon.Location = CoordToGeopoint(stopCoord.X, stopCoord.Y);
                stopIcon.Image = instruction.LastInstruction ? StopImage : NextImage;
                layer.MapElements.Add(stopIcon);
            }
        }
    }
}
