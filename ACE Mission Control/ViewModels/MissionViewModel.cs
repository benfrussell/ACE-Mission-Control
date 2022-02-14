using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;
using ACE_Mission_Control.Core.Models;
using GalaSoft.MvvmLight.Command;
using ACE_Mission_Control.Helpers;
using GalaSoft.MvvmLight.Messaging;
using System.ComponentModel;
using Windows.ApplicationModel.Core;
using System.Collections.ObjectModel;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System.Globalization;
using Windows.UI.Notifications;
using Windows.UI.Xaml.Controls.Maps;
using Windows.Devices.Geolocation;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Linq;
using Windows.Storage.Streams;
using Windows.UI;

namespace ACE_Mission_Control.ViewModels
{
    public class MissionViewModel : DroneViewModelBase
    {
        public enum ConnectStatus
        {
            NotConnected,
            Attempting,
            Connected
        }
        // --- Connection properties

        private ConnectStatus _chaperoneStatus;
        public ConnectStatus ChaperoneStatus
        {
            get { return _chaperoneStatus; }
            set
            {
                if (_chaperoneStatus == value)
                    return;
                _chaperoneStatus = value;
                RaisePropertyChanged();
            }
        }

        private ConnectStatus _directorStatus;
        public ConnectStatus DirectorStatus
        {
            get { return _directorStatus; }
            set
            {
                if (_directorStatus == value)
                    return;
                _directorStatus = value;
                RaisePropertyChanged();
            }
        }

        private string _hostname_text;
        public string Hostname_Text
        {
            get { return _hostname_text; }
            set
            {
                if (_hostname_text != value)
                {
                    UnsavedChanges = (AttachedDrone.OBCClient.Hostname != Hostname_Text);
                    _hostname_text = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool _unsaved_changes = false;
        public bool UnsavedChanges
        {
            get { return _unsaved_changes; }
            set
            {
                if (_unsaved_changes != value)
                {
                    _unsaved_changes = value;
                    RaisePropertyChanged();
                }
            }
        }

        private ObservableCollection<Tuple<string, string>> entriesFound;
        public ObservableCollection<Tuple<string, string>> EntriesFound
        {
            get { return entriesFound; }
            private set
            {
                if (value == entriesFound)
                    return;
                entriesFound = value;
                RaisePropertyChanged();
            }
        }

        private bool searching;
        public bool Searching
        {
            get { return searching; }
            private set
            {
                if (value == searching)
                    return;
                searching = value;
                RaisePropertyChanged();
            }
        }

        private int progress;
        public int Progress
        {
            get { return progress; }
            private set
            {
                if (value == progress)
                    return;
                progress = value;
                RaisePropertyChanged();
            }
        }

        private bool _droneConnectionOn;
        public bool DroneConnectionOn
        {
            get { return _droneConnectionOn; }
            set
            {
                if (_droneConnectionOn == value)
                    return;
                _droneConnectionOn = value;
                RaisePropertyChanged();
            }
        }

        // --- Planner properties

        private string _plannerStatus;
        public string PlannerStatus
        {
            get { return _plannerStatus; }
            set
            {
                if (_plannerStatus == value)
                    return;
                _plannerStatus = value;
                RaisePropertyChanged();
            }
        }

        private SolidColorBrush _plannerStatusColour;
        public SolidColorBrush PlannerStatusColour
        {
            get { return _plannerStatusColour; }
            set
            {
                if (_plannerStatusColour == value)
                    return;
                _plannerStatusColour = value;
                RaisePropertyChanged();
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

        private string _lockButtonText;
        public string LockButtonText
        {
            get { return _lockButtonText; }
            set
            {
                if (_lockButtonText == value)
                    return;
                _lockButtonText = value;
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

        private bool startModeErrorNotificationSent;
        private bool mapCentred;

        public MissionViewModel()
        {
            IPLookup.StaticPropertyChanged += IPLookup_StaticPropertyChanged;
            EntriesFound = IPLookup.EntriesFound;
            Searching = IPLookup.Searching;
            Progress = IPLookup.Progress;
            TreatmentDurationError = false;
            StartModeError = false;
            MapLayers = new ObservableCollection<MapLayer>();
        }

        private void IPLookup_StaticPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Searching":
                    Searching = IPLookup.Searching;
                    break;
                case "Progress":
                    Progress = IPLookup.Progress;
                    break;
            }
        }

        protected override void DroneAttached(bool firstTime)
        {
            Hostname_Text = AttachedDrone.OBCClient.Hostname;

            AttachedDrone.OBCClient.PropertyChanged += OBCClient_PropertyChanged;
            AttachedDrone.PropertyChanged += AttachedDrone_PropertyChanged;
            AttachedDrone.Mission.PropertyChanged += Mission_PropertyChanged;

            AttachedDrone.Mission.InstructionAreasUpdated += Mission_InstructionAreasUpdated;
            AttachedDrone.Mission.InstructionRouteUpdated += Mission_InstructionRouteUpdated;
            AttachedDrone.Mission.InstructionSyncedPropertyUpdated += Mission_InstructionSyncedPropertyUpdated;

            mapCentred = false;

            UpdatePlannerMapAreas();
            UpdatePlannerMapPoints();

            startModeErrorNotificationSent = false;
            CheckStartModeError();

            UpdateConnectionStatuses();
            UpdatePlannerStatus();
            UpdateLockButton();

            DroneConnectionOn = AttachedDrone.InterfaceState == Pbdrone.InterfaceStatus.Types.State.Attempting ||
                AttachedDrone.InterfaceState == Pbdrone.InterfaceStatus.Types.State.Online;

            SelectedStartMode = (int)AttachedDrone.Mission.StartMode;
        }

        private async void OBCClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (e.PropertyName == "IsDirectorConnected" || e.PropertyName == "IsChaperoneConnected" || e.PropertyName == "ConnectionInProgress")
                    UpdateConnectionStatuses();
            });
        }

        private async void Mission_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                RaisePropertyChanged(e.PropertyName);

                switch (e.PropertyName)
                {
                    case "Locked":
                        UpdateLockButton();
                        break;
                    case "TreatmentDuration":
                        TreatmentDuration = AttachedDrone.Mission.TreatmentDuration.ToString();
                        break;
                    case "StartMode":
                        SelectedStartMode = (int)AttachedDrone.Mission.StartMode;
                        CheckStartModeError();
                        break;
                    case "MissionSet":
                        UpdatePlannerStatus();
                        break;
                }
            });
        }

        private async void AttachedDrone_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                switch (e.PropertyName)
                {
                    case "InterfaceState":
                        DroneConnectionOn = AttachedDrone.InterfaceState == Pbdrone.InterfaceStatus.Types.State.Attempting || AttachedDrone.InterfaceState == Pbdrone.InterfaceStatus.Types.State.Online;
                        break;
                }
            });
        }

        private void Mission_InstructionRouteUpdated(object sender, InstructionRouteUpdatedArgs e)
        {
            UpdatePlannerMapAreas(e.Instruction);
            UpdatePlannerMapPoints(e.Instruction);
            if (e.Instruction.FirstInstruction)
                CheckStartModeError();
        }

        private async void Mission_InstructionSyncedPropertyUpdated(object sender, InstructionSyncedPropertyUpdatedArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var instruction = AttachedDrone.Mission.GetInstructionByID(e.InstructionID);
                if (instruction == null)
                    return;

                if (e.UpdatedParameters.Contains("AreaEntryExitCoordinates"))
                {
                    UpdatePlannerMapPoints(instruction);
                    CheckStartModeError();
                }
                else if (e.UpdatedParameters.Contains("Enabled"))
                {
                    UpdatePlannerMapAreas(instruction);
                    UpdatePlannerMapPoints(instruction);
                    // If there's a new first instruction after this change, which WASN'T this instruction, we need to draw it's route on the map now
                    if (!instruction.FirstInstruction)
                    {
                        var nextInstruction = AttachedDrone.Mission.GetNextInstruction();
                        if (nextInstruction == null)
                            return;

                        UpdatePlannerMapAreas(nextInstruction);
                        UpdatePlannerMapPoints(nextInstruction);
                    }

                    // Same deal for the last instruction, we draw it's points differently
                    if (!instruction.LastInstruction)
                    {
                        var lastInstruction = AttachedDrone.Mission.GetLastInstruction();
                        if (lastInstruction != null)
                            UpdatePlannerMapPoints(AttachedDrone.Mission.GetLastInstruction());
                    }
                }
            });
        }

        private void Mission_InstructionAreasUpdated(object sender, InstructionAreasUpdatedArgs e)
        {
            UpdatePlannerMapAreas(e.Instructions);
            UpdatePlannerMapPoints(e.Instructions);
            CheckStartModeError();
        }

        protected override void DroneUnattaching()
        {
            AttachedDrone.OBCClient.PropertyChanged -= OBCClient_PropertyChanged;
            AttachedDrone.PropertyChanged -= AttachedDrone_PropertyChanged;
            AttachedDrone.Mission.PropertyChanged -= Mission_PropertyChanged;
            AttachedDrone.Mission.InstructionAreasUpdated -= Mission_InstructionAreasUpdated;
            AttachedDrone.Mission.InstructionRouteUpdated -= Mission_InstructionRouteUpdated;
            AttachedDrone.Mission.InstructionSyncedPropertyUpdated -= Mission_InstructionSyncedPropertyUpdated;
        }

        private void UpdateLockButton()
        {
            if (AttachedDrone.Mission.Locked)
                LockButtonText = "Planner_UnlockButton".GetLocalized();
            else
                LockButtonText = "Planner_LockButton".GetLocalized();
        }

        private void UpdateConnectionStatuses()
        {
            if (AttachedDrone.OBCClient.IsDirectorConnected)
                DirectorStatus = ConnectStatus.Connected;
            else if (AttachedDrone.OBCClient.ConnectionInProgress)
                DirectorStatus = ConnectStatus.Attempting;
            else
                DirectorStatus = ConnectStatus.NotConnected;

            if (AttachedDrone.OBCClient.IsChaperoneConnected)
                ChaperoneStatus = ConnectStatus.Connected;
            else if (AttachedDrone.OBCClient.ConnectionInProgress)
                ChaperoneStatus = ConnectStatus.Attempting;
            else
                ChaperoneStatus = ConnectStatus.NotConnected;
        }

        private void UpdatePlannerStatus()
        {
            if (AttachedDrone.Mission.MissionSet)
            {
                PlannerStatus = "Mission_PlannerMissionSet".GetLocalized();
                PlannerStatusColour = new SolidColorBrush(Colors.ForestGreen);
            }
            else
            {
                PlannerStatus = "Mission_PlannerMissionNotSet".GetLocalized();
                PlannerStatusColour = new SolidColorBrush(Colors.Yellow);
            }
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

        // --- Connection commands

        public RelayCommand ConnectOBCCommand => new RelayCommand(() => connectOBCCommand());
        private void connectOBCCommand()
        {
            if (AttachedDrone.OBCClient.AutoTryingConnections)
            {
                AttachedDrone.OBCClient.StopTryingConnections();
                AttachedDrone.OBCClient.Disconnect();
                AttachedDrone.Mission.ResetStatus();
            }
            else
            {
                AttachedDrone.OBCClient.StartTryingConnections();
            }
                
        }

        public RelayCommand<ToggleSwitch> ConnectDroneCommand => new RelayCommand<ToggleSwitch>((toggle) => connectDroneCommand(toggle));
        private void connectDroneCommand(ToggleSwitch toggle = null)
        {
            bool connectDrone = toggle == null ? DroneConnectionOn : toggle.IsOn;
            if (connectDrone)
            {
                if (AttachedDrone.OBCClient.IsDirectorConnected && AttachedDrone.InterfaceState == Pbdrone.InterfaceStatus.Types.State.Offline)
                    AttachedDrone.SendCommand("start_interface");
            }
            else
            {
                if (AttachedDrone.OBCClient.IsDirectorConnected &&
                    (AttachedDrone.InterfaceState == Pbdrone.InterfaceStatus.Types.State.Attempting ||
                    AttachedDrone.InterfaceState == Pbdrone.InterfaceStatus.Types.State.Online))
                    AttachedDrone.SendCommand("stop_interface");
            }
        }

        public RelayCommand ApplyChangesCommand => new RelayCommand(() => applyButtonClicked());
        private void applyButtonClicked()
        {
            AttachedDrone.OBCClient.Configure(Hostname_Text);
            UnsavedChanges = false;
        }

        public RelayCommand ResetChangesCommand => new RelayCommand(() => resetButtonClicked());
        private void resetButtonClicked()
        {
            Hostname_Text = AttachedDrone.OBCClient.Hostname;
        }

        public RelayCommand StartSearch => new RelayCommand(() => IPLookup.LookupIPs("gdg-pi"));

        public RelayCommand<ListView> SearchResultClickedCommand => new RelayCommand<ListView>((v) => searchResultClicked(v));
        private void searchResultClicked(ListView list)
        {
            if (list.SelectedItem == null)
                return;
            var item = list.SelectedItem as Tuple<string, string>;
            Hostname_Text = item.Item2;
            applyButtonClicked();
        }

        // --- Planner commands

        public RelayCommand<TreatmentInstruction> ReorderInstructionUpCommand => new RelayCommand<TreatmentInstruction>((args) => reorderInstructionCommand(args, -1));
        public RelayCommand<TreatmentInstruction> ReorderInstructionDownCommand => new RelayCommand<TreatmentInstruction>((args) => reorderInstructionCommand(args, 1));
        private void reorderInstructionCommand(TreatmentInstruction instruction, int change)
        {
            AttachedDrone.Mission.ReorderInstruction(instruction, AttachedDrone.Mission.TreatmentInstructions.IndexOf(instruction) + change);
        }

        public RelayCommand<TreatmentInstruction> WaypointRouteChangedCommand => new RelayCommand<TreatmentInstruction>((args) => waypointRouteChangedCommand(args));
        private void waypointRouteChangedCommand(TreatmentInstruction args)
        {
            if (args == null)
                return;

            // When moving items in the ObservableCollection of TreatmentInstructions that is bound to the ListView that holds these Comboboxes....
            // ... it seems UWP internally adds/readds the TreatmentInstruction item
            // When doing this readding, it sets the ComboBox's SelectedItem to null for some reason (the SelectionChanged event shows that the selected item gets removed by an internal trigger)
            // So we need to ask the TreatmentInstruction to resend a NotifyPropertyChanged for it's TreatmentRoute property, thus setting the SelectedItem back
            // This is all very stupid!
            if (!args.Renotifying)
                args.RenotifyTreatmentRoute();
        }

        public RelayCommand DurationChangedCommand => new RelayCommand(() => durationChangedCommand());
        private void durationChangedCommand()
        {
            if (isTreatmentDurationValid(TreatmentDuration))
                AttachedDrone.SendCommand("set_duration -duration " + TreatmentDuration.ToString());
        }

        private bool isTreatmentDurationValid(string durationString)
        {
            int parseOut;
            return durationString.Length == 0 || int.TryParse(durationString, NumberStyles.Integer, CultureInfo.InvariantCulture, out parseOut);
        }

        public RelayCommand<ComboBoxItem> StartModeSelectionCommand => new RelayCommand<ComboBoxItem>((args) => startModeSelectionCommand(args));
        private void startModeSelectionCommand(ComboBoxItem item)
        {
            var selectedMode = (StartTreatmentParameters.Mode)item.Tag;
            AttachedDrone.Mission.StartMode = selectedMode;
        }

        public RelayCommand ResetCommand => new RelayCommand(() => resetCommand());
        private void resetCommand()
        {
            AttachedDrone.Mission.ResetProgress();
        }

        public RelayCommand LockCommand => new RelayCommand(() => lockCommand());
        private void lockCommand()
        {
            AttachedDrone.ToggleLock();
        }

        // --- Control commands

        public RelayCommand RestartOBCCommand => new RelayCommand(() => restartOBCCommand());
        private void restartOBCCommand()
        {
            AttachedDrone.SendCommand("start_director");
        }

        public RelayCommand TestPayloadCommand => new RelayCommand(() => testPayloadCommand());
        private void testPayloadCommand()
        {
            AttachedDrone.SendCommand("test_payload");
        }

        public RelayCommand TestInterfaceCommand => new RelayCommand(() => testInterfaceCommand());
        private void testInterfaceCommand()
        {
            AttachedDrone.SendCommand("test_interface");
        }

        public RelayCommand StopPayloadCommand => new RelayCommand(() => stopPayloadCommand());
        private void stopPayloadCommand()
        {
            AttachedDrone.SendCommand("force_stop_payload");
        }

        public RelayCommand SynchronizeCommand => new RelayCommand(() => synchronizeCommand());
        private void synchronizeCommand()
        {
            AttachedDrone.Synchronize(true);
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

                Color colour = (Color)new InstructionNumberToColour().Convert(instruction.ID, typeof(Color), null, null);

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
                colour.A = 60;
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
                    var startCoord = AttachedDrone.Mission.GetStartCoordinate(instruction.ID);
                    startIcon.Location = CoordToGeopoint(startCoord.X, startCoord.Y);
                }

                startIcon.Image = StartImage;
                startIcon.Title = instruction.Name;
                layer.MapElements.Add(startIcon);

                // Add a MapIcon for the exit point for each area
                MapIcon stopIcon = new MapIcon();
                var stopCoord = AttachedDrone.Mission.GetStopCoordinate(instruction.ID);
                stopIcon.Location = CoordToGeopoint(stopCoord.X, stopCoord.Y);
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
