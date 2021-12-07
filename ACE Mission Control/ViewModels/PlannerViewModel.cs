using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ACE_Mission_Control.Core.Models;
using ACE_Mission_Control.Helpers;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using Windows.ApplicationModel.Core;
using Windows.Devices.Geolocation;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Media;
using Windows.UI.Notifications;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Globalization;

namespace ACE_Mission_Control.ViewModels
{
    public class PlannerViewModel : DroneViewModelBase
    {
        private ObservableCollection<ITreatmentInstruction> treatmentInstructions;
        public ObservableCollection<ITreatmentInstruction> TreatmentInstructions
        {
            get => treatmentInstructions;
            set
            {
                if (treatmentInstructions == value)
                    return;
                treatmentInstructions = value;
                RaisePropertyChanged();
            }
        }

        private string _missionActivatedText;
        public string MissionActivatedText
        {
            set
            {
                if (value == _missionActivatedText)
                    return;
                _missionActivatedText = value;
                RaisePropertyChanged();
            }
            get
            {
                return _missionActivatedText;
            }
        }

        private string _treatmentDuration;
        public string TreatmentDuration
        {
            get { return _treatmentDuration; }
            set
            {
                if (_treatmentDuration == value)
                    return;
                _treatmentDuration = value;
                if (isTreatmentDurationValid(_treatmentDuration))
                    TreatmentDurationError = false;
                else
                    TreatmentDurationError = true;
                RaisePropertyChanged("TreatmentDuration");
            }
        }

        private bool _treatmentDurationError;
        public bool TreatmentDurationError
        {
            get { return _treatmentDurationError; }
            set
            {
                if (_treatmentDurationError == value)
                    return;
                _treatmentDurationError = value;
                RaisePropertyChanged();
            }
        }

        public List<string> AvailablePayloads
        {
            get { return AttachedDrone.Mission.AvailablePayloads; }
        }

        private int _selectedPayload;
        public int SelectedPayload
        {
            get { return _selectedPayload; }
            set
            {
                if (_selectedPayload == value)
                    return;
                _selectedPayload = value;
                RaisePropertyChanged();
            }
        }

        private int _selectedStartMode;
        public int SelectedStartMode
        {
            get { return _selectedStartMode; }
            set
            {
                if (_selectedStartMode == value)
                    return;
                _selectedStartMode = value;
                RaisePropertyChanged();
            }
        }

        private bool startModeError;
        public bool StartModeError
        {
            get { return startModeError; }
            set
            {
                if (startModeError == value)
                    return;
                startModeError = value;
                RaisePropertyChanged();
            }
        }

        private ObservableCollection<MapLayer> mapLayers;
        public ObservableCollection<MapLayer> MapLayers
        {
            get => mapLayers;
            set
            {
                if (mapLayers == value)
                    return;
                mapLayers = value;
                RaisePropertyChanged();
            }
        }

        private Geopoint mapCentre;
        public Geopoint MapCentre
        {
            get => mapCentre;
            set
            {
                if (mapCentre == value)
                    return;
                mapCentre = value;
                RaisePropertyChanged();
            }
        }

        private bool suppressPayloadCommand;
        private bool startModeErrorNotificationSent;
        private bool mapCentred;

        public PlannerViewModel()
        {
            StartModeError = false;
            TreatmentDurationError = false;
            MapLayers = new ObservableCollection<MapLayer>();
            suppressPayloadCommand = false;
            startModeErrorNotificationSent = false;
            mapCentred = false;
        }

        protected override void DroneAttached(bool firstTime)
        {
            TreatmentInstructions = AttachedDrone.Mission.TreatmentInstructions;

            UpdatePlannerMapAreas();
            UpdatePlannerMapPoints();
            CheckStartModeError();

            AttachedDrone.Mission.PropertyChanged += Mission_PropertyChanged;
            AttachedDrone.Mission.InstructionAreasUpdated += Mission_InstructionAreasUpdated;
            AttachedDrone.Mission.InstructionRouteUpdated += Mission_InstructionRouteUpdated;
            AttachedDrone.Mission.StartParametersChangedEvent += Mission_StartParametersChangedEvent;

            SelectedStartMode = (int)AttachedDrone.Mission.StartMode;

            if (AttachedDrone.Mission.Activated)
                MissionActivatedText = "Planner_DeactivateButton".GetLocalized();
            else
                MissionActivatedText = "Planner_ActivateButton".GetLocalized();
        }

        private void Mission_InstructionRouteUpdated(object sender, InstructionRouteUpdatedEventArgs e)
        {
            UpdatePlannerMapAreas(e.Instruction);
            UpdatePlannerMapPoints(e.Instruction);
            if (e.Instruction.FirstInstruction)
                CheckStartModeError();
        }

        private async void Mission_StartParametersChangedEvent(object sender, EventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var nextInstruction = AttachedDrone.Mission.GetNextInstruction();
                if (nextInstruction != null)
                    UpdatePlannerMapPoints(nextInstruction);
                CheckStartModeError();
            });
        }

        private async void Mission_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                RaisePropertyChanged(e.PropertyName);

                switch (e.PropertyName)
                {
                    case "Activated":
                        if (AttachedDrone.Mission.Activated)
                            MissionActivatedText = "Planner_DeactivateButton".GetLocalized();
                        else
                            MissionActivatedText = "Planner_ActivateButton".GetLocalized();
                        break;
                    case "TreatmentDuration":
                        TreatmentDuration = AttachedDrone.Mission.TreatmentDuration.ToString();
                        break;
                    case "SelectedPayload":
                        suppressPayloadCommand = true;
                        SelectedPayload = AttachedDrone.Mission.SelectedPayload;
                        break;
                    case "StartMode":
                        SelectedStartMode = (int)AttachedDrone.Mission.StartMode;
                        CheckStartModeError();
                        break;
                }
            });
        }

        private void Mission_InstructionAreasUpdated(object sender, InstructionAreasUpdatedEventArgs e)
        {
            UpdatePlannerMapAreas(e.Instructions);
            UpdatePlannerMapPoints(e.Instructions);
            CheckStartModeError();
        }

        private void CheckStartModeError()
        {
            var startPosition = AttachedDrone.Mission.GetStartCoordinate();

            if (AttachedDrone.Mission.StartMode == StartTreatmentParameters.Mode.Flythrough && startPosition != null)
            {
                var nextInstruction = AttachedDrone.Mission.GetNextInstruction();

                // 7.5 metres is the hardcoded buffer for triggering entry in the drone 
                if (nextInstruction != null && nextInstruction.HasValidTreatmentRoute() &&
                    !nextInstruction.TreatmentRoute.DoesRoutePassCoordinate(startPosition, 7.5f))
                {
                    StartModeError = true;

                    if (!startModeErrorNotificationSent & !Window.Current.Visible)
                    {
                        // Construct the content
                        var content = new ToastContentBuilder()
                            .AddText("Planner_FlythroughErrorNotification_Title".GetLocalized())
                            .AddText(string.Format("Planner_FlythroughErrorNotification_Content".GetLocalized(), AttachedDrone.Name))
                            .GetToastContent();

                        // Create the notification
                        var notif = new ToastNotification(content.GetXml());

                        // And show it!
                        ToastNotificationManager.CreateToastNotifier().Show(notif);
                        startModeErrorNotificationSent = true;
                    }

                    return;
                }
            }

            StartModeError = false;
            startModeErrorNotificationSent = false;
        }

        protected override void DroneUnattaching()
        {
            AttachedDrone.Mission.InstructionAreasUpdated -= Mission_InstructionAreasUpdated;
            AttachedDrone.Mission.PropertyChanged -= Mission_PropertyChanged;
            AttachedDrone.Mission.StartParametersChangedEvent -= Mission_StartParametersChangedEvent;
        }

        private bool isTreatmentDurationValid(string durationString)
        {
            int parseOut;
            return durationString.Length == 0 || int.TryParse(durationString, NumberStyles.Integer, CultureInfo.InvariantCulture, out parseOut);
        }

        // --- Mission Commands

        public RelayCommand<ComboBox> PayloadSelectionCommand => new RelayCommand<ComboBox>((box) => payloadSelectionCommand(box));
        private void payloadSelectionCommand(ComboBox box)
        {
            if (!suppressPayloadCommand)
            {
                //AttachedDrone.SendCommand("set_payload -index " + box.SelectedIndex.ToString());
                suppressPayloadCommand = false;
            }
        }

        public RelayCommand DurationChangedCommand => new RelayCommand(() => durationChangedCommand());
        private void durationChangedCommand()
        {
            if (isTreatmentDurationValid(TreatmentDuration))
                AttachedDrone.SendCommand("set_duration -duration " + TreatmentDuration.ToString());
        }

        public RelayCommand UploadCommand => new RelayCommand(() => uploadCommand());
        private void uploadCommand()
        {
            AttachedDrone.UploadMission();
        }

        public RelayCommand ActivateCommand => new RelayCommand(() => activateCommand());
        private void activateCommand()
        {
            if (AttachedDrone.Mission.Activated)
                AttachedDrone.SendCommand("deactivate_mission");
            else
                AttachedDrone.SendCommand("activate_mission");
        }

        public RelayCommand ResetCommand => new RelayCommand(() => resetCommand());
        private void resetCommand()
        {
            AttachedDrone.Mission.ResetProgress();
        }

        public RelayCommand<ComboBoxItem> StartModeSelectionCommand => new RelayCommand<ComboBoxItem>((args) => startModeSelectionCommand(args));
        private void startModeSelectionCommand(ComboBoxItem item)
        {
            var selectedMode = (StartTreatmentParameters.Mode)item.Tag;
            AttachedDrone.Mission.StartMode = selectedMode;
        }

        public RelayCommand<TreatmentInstruction> ReorderInstructionUpCommand => new RelayCommand<TreatmentInstruction>((args) => reorderInstructionCommand(args, -1));
        public RelayCommand<TreatmentInstruction> ReorderInstructionDownCommand => new RelayCommand<TreatmentInstruction>((args) => reorderInstructionCommand(args, 1));
        private void reorderInstructionCommand(TreatmentInstruction instruction, int change)
        {
            AttachedDrone.Mission.ReorderInstruction(instruction, AttachedDrone.Mission.TreatmentInstructions.IndexOf(instruction) + change);
        }

        // -- Map Functions

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
            var lastIndex = MapLayers.Count();
            var layersToAdd = (index + 1) - MapLayers.Count();

            if (layersToAdd <= 0)
                return;

            // Add map layers until it reaches the index
            for (int i = 0; i < layersToAdd; i++)
                MapLayers.Add(new MapElementsLayer());
        }

        private void UpdatePlannerMapAreas()
        {
            UpdatePlannerMapAreas(AttachedDrone.Mission.TreatmentInstructions);
        }

        private void UpdatePlannerMapAreas(ITreatmentInstruction instruction)
        {
            UpdatePlannerMapAreas(new List<ITreatmentInstruction> { instruction });
        }

        private void UpdatePlannerMapAreas(IEnumerable<ITreatmentInstruction> instructions)
        {
            foreach (ITreatmentInstruction instruction in instructions)
            {
                if (instruction == null)
                    continue;

                var layerIndex = instruction.ID * 2;

                // Add the layer or clear elements at the existing layer
                if (MapLayers.ElementAtOrDefault(layerIndex) == null)
                    AddMapLayersUntilIndex(layerIndex);
                else
                    ((MapElementsLayer)MapLayers[layerIndex]).MapElements.Clear();

                if (!instruction.Enabled)
                    continue;

                Color colour = (Color)(new InstructionNumberToColour().Convert(instruction.ID, typeof(Color), null, null));

                if (instruction.FirstInstruction && instruction.IsTreatmentRouteValid())
                {
                    MapPolyline polyline = new MapPolyline();
                    polyline.Path = CoordsToGeopath(instruction.TreatmentRoute.GetBasicCoordinates());
                    polyline.ZIndex = 2;
                    polyline.StrokeColor = colour;
                    polyline.StrokeThickness = 2;
                    polyline.StrokeDashed = true;
                    ((MapElementsLayer)MapLayers[layerIndex]).MapElements.Add(polyline);
                }

                MapPolygon polygon = new MapPolygon();
                polygon.Path = CoordsToGeopath(instruction.TreatmentPolygon.GetBasicCoordinates());
                polygon.ZIndex = 1;
                polygon.StrokeColor = colour;
                polygon.StrokeThickness = 3;
                polygon.StrokeDashed = false;
                colour.A = 100;
                polygon.FillColor = colour;
                ((MapElementsLayer)MapLayers[layerIndex]).MapElements.Add(polygon);
            }
        }

        private void UpdatePlannerMapPoints()
        {
            UpdatePlannerMapPoints(AttachedDrone.Mission.TreatmentInstructions);
        }

        private void UpdatePlannerMapPoints(ITreatmentInstruction instruction)
        {
            UpdatePlannerMapPoints(new List<ITreatmentInstruction> { instruction });
        }

        private void UpdatePlannerMapPoints(IEnumerable<ITreatmentInstruction> instructions)
        {
            foreach (ITreatmentInstruction instruction in instructions)
            {
                var layerIndex = (instruction.ID * 2) + 1;

                // Add the layer or clear elements at the existing layer
                if (MapLayers.ElementAtOrDefault(layerIndex) == null)
                    AddMapLayersUntilIndex(layerIndex);
                else
                    ((MapElementsLayer)MapLayers[layerIndex]).MapElements.Clear();

                if (!instruction.Enabled)
                    continue;

                var layer = (MapElementsLayer)MapLayers[layerIndex];

                MapIcon startIcon = new MapIcon();

                if (instruction.FirstInstruction)
                {
                    // Add a MapIcon for the last position if there is a last position
                    if (AttachedDrone.Mission.LastPosition != null)
                    {
                        MapIcon lastPosIcon = new MapIcon();
                        lastPosIcon.Location = CoordToGeopoint(
                            AttachedDrone.Mission.LastPosition.X,
                            AttachedDrone.Mission.LastPosition.Y);
                        lastPosIcon.Image = FlagImage;
                        lastPosIcon.NormalizedAnchorPoint = new Windows.Foundation.Point(0.15, 1);
                        lastPosIcon.Title = "Planner_MapLastPositionLabel".GetLocalized();
                        lastPosIcon.ZIndex = -1;
                        layer.MapElements.Add(lastPosIcon);
                    }

                    // Add a MapIcon for each waypoint in the first instruction's route if in SelectedWaypoint mode
                    if (AttachedDrone.Mission.StartMode == StartTreatmentParameters.Mode.SelectedWaypoint)
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
                    var startCoord = AttachedDrone.Mission.GetStartCoordinate();
                    if (startCoord != null)
                    {
                        var startGeopoint = CoordToGeopoint(startCoord.X, startCoord.Y);
                        startIcon.Location = startGeopoint;

                        if (!mapCentred)
                        {
                            MapCentre = startGeopoint;
                            mapCentred = true;
                        }
                    }
                }
                else
                {
                    startIcon.Location = CoordToGeopoint(instruction.AreaEntryCoordinate.X, instruction.AreaEntryCoordinate.Y);
                }

                startIcon.Image = StartImage;
                startIcon.Title = instruction.Name;
                layer.MapElements.Add(startIcon);

                // Add a MapIcon for the exit point for each area
                MapIcon stopIcon = new MapIcon();
                stopIcon.Location = CoordToGeopoint(instruction.AreaExitCoordinate.X, instruction.AreaExitCoordinate.Y);
                stopIcon.Image = instruction.LastInstruction ? StopImage : NextImage;
                layer.MapElements.Add(stopIcon);
            }
        }

        public RelayCommand<MapElementClickEventArgs> MapElementClickedCommand => new RelayCommand<MapElementClickEventArgs>((args) => mapElementClickedCommand(args));
        private void mapElementClickedCommand(MapElementClickEventArgs args)
        {
            if (AttachedDrone.Mission.StartMode != StartTreatmentParameters.Mode.SelectedWaypoint)
                return;

            foreach (MapElement element in args.MapElements)
            {
                if (args.MapElements[0].Tag == null)
                    continue;

                AttachedDrone.Mission.SetSelectedStartWaypoint(args.MapElements[0].Tag as string);
                break;
            }
            
        }
    }
}
