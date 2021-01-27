using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Pbdrone;
using NetTopologySuite.Geometries;

namespace ACE_Mission_Control.Core.Models
{
    public class InstructionsUpdatedEventArgs
    {
        public List<TreatmentInstruction> Instructions { get; set; }

        public InstructionsUpdatedEventArgs() { Instructions = new List<TreatmentInstruction>(); }
    }

    public class StartModeChangedEventArgs
    {
        public StartTreatmentParameters.Modes NewMode { get; set; }
    }

    public class Mission : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler<StartModeChangedEventArgs> StartModeChanged;

        public event EventHandler<InstructionsUpdatedEventArgs> InstructionUpdated;

        private StartTreatmentParameters startParameters;
        public StartTreatmentParameters StartParameters 
        { 
            get => startParameters;
            private set
            {
                if (startParameters == value)
                    return;
                startParameters = value;
                NotifyPropertyChanged();
            }
        }

        private MissionStatus.Types.Stage stage;
        public MissionStatus.Types.Stage Stage
        {
            get => stage;
            private set
            {
                if (stage == value)
                    return;
                stage = value;
                NotifyPropertyChanged();
            }
        }

        private bool activated;
        public bool Activated
        {
            get => activated;
            private set
            {
                if (activated == value)
                    return;
                activated = value;
                NotifyPropertyChanged();
            }
        }

        private bool hasProgress;
        public bool HasProgress
        {
            get => hasProgress;
            private set
            {
                if (hasProgress == value)
                    return;
                hasProgress = value;
                NotifyPropertyChanged();
            }
        }

        private Coordinate lastPosition;
        public Coordinate LastPosition
        {
            get => lastPosition;
            private set
            {
                if (lastPosition == value)
                    return;
                lastPosition = value;
                NotifyPropertyChanged();
            }
        }

        private bool canBeReset;
        public bool CanBeReset
        {
            get => canBeReset;
            private set
            {
                if (canBeReset == value)
                    return;
                canBeReset = value;
                NotifyPropertyChanged();
            }
        }
        private void UpdateCanBeReset() { CanBeReset = HasProgress && CanBeModified; }

        private bool canBeModified;
        public bool CanBeModified
        {
            get => canBeModified;
            private set
            {
                if (canBeModified == value)
                    return;
                canBeModified = value;
                NotifyPropertyChanged();
            }
        }
        private void UpdateCanBeModified() { CanBeModified = onboardComputer.IsDirectorConnected && !Activated; }

        private bool canToggleActivation;
        public bool CanToggleActivation
        {
            get => canToggleActivation;
            private set
            {
                if (canToggleActivation == value)
                    return;
                canToggleActivation = value;
                NotifyPropertyChanged();
            }
        }
        private void UpdateCanToggleActivation() { CanToggleActivation = onboardComputer.IsDirectorConnected && drone.InterfaceState == InterfaceStatus.Types.State.Online; }

        private bool flyThroughMode;
        public bool FlyThroughMode
        {
            get => flyThroughMode;
            private set
            {
                if (flyThroughMode == value)
                    return;
                flyThroughMode = value;
                NotifyPropertyChanged();
            }
        }

        private int treatmentDuration;
        public int TreatmentDuration
        {
            get => treatmentDuration;
            set
            {
                if (treatmentDuration == value)
                    return;
                treatmentDuration = value;
                NotifyPropertyChanged();
            }
        }

        private int selectedPayload;
        public int SelectedPayload
        {
            get { return selectedPayload; }
            set
            {
                if (selectedPayload == value)
                    return;
                selectedPayload = value;
                NotifyPropertyChanged();
            }
        }

        private List<string> availablePayloads;
        public List<string> AvailablePayloads
        {
            get { return availablePayloads; }
            private set
            {
                if (availablePayloads == value)
                    return;
                availablePayloads = value;
                NotifyPropertyChanged();
            }
        }

        // Only store the ID of the coordinate because we should search and confirm it still exists everytime we need to use it
        private string boundStartCoordinateID;
        // Used to suppress EnabledChanged events causing InstructionUpdated events to be sent out while updating instructions
        // A single InstructionUpdated event will already be sent out after updating instructions
        private bool updatingInstructions;

        private Drone drone;
        private OnboardComputerClient onboardComputer;
        

        public ObservableCollection<TreatmentInstruction> TreatmentInstructions;

        public Mission(Drone _drone, OnboardComputerClient _onboardComputer)
        {
            MissionRetriever.AreaScanPolygonsUpdated += MissionData_AreaScanPolygonsUpdated;
            TreatmentInstructions = new ObservableCollection<TreatmentInstruction>();
            TreatmentInstructions.CollectionChanged += TreatmentInstructions_CollectionChanged;
            StartParameters = new StartTreatmentParameters();

            drone = _drone;
            drone.PropertyChanged += Drone_PropertyChanged;
            onboardComputer = _onboardComputer;
            onboardComputer.PropertyChanged += OnboardComputerClient_PropertyChanged;

            Activated = false;
            HasProgress = true;

            UpdateCanBeModified();
            UpdateCanBeReset();
            UpdateCanToggleActivation();

            // test
            LastPosition = new Coordinate(-1.32579946805, 0.791221070472);

            StartParameters.UpdateAvailableModes(null, HasProgress);
        }

        private void TreatmentInstructions_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && !updatingInstructions)
                UpdateStartCoordinate();
        }

        public void UpdateMissionStatus(MissionStatus newStatus)
        {
            Stage = newStatus.MissionStage;

            Activated = newStatus.Activated;
            UpdateCanBeModified();

            HasProgress = newStatus.InProgress;
            UpdateCanBeReset();

            // Update the treatment instruction statuses with any results that came from the mission status update
            foreach (TreatmentInstruction instruction in TreatmentInstructions)
            {
                var resultInStatus = newStatus.Results.FirstOrDefault(r => r.AreaID == instruction.TreatmentPolygon.Id);
                if (resultInStatus != null)
                    instruction.AreaStatus = resultInStatus.Status;
            }

            if (newStatus.LastLongitude != 0 || newStatus.LastLatitude != 0)
            {
                LastPosition = new Coordinate(
                  (newStatus.LastLongitude / 180) * Math.PI,
                  (newStatus.LastLatitude / 180) * Math.PI);

                if (GetNextInstruction().AreaStatus == AreaResult.Types.Status.InProgress)
                    SetStartTreatmentMode(StartTreatmentParameters.Modes.LastPositionContinued);
                else
                    SetStartTreatmentMode(StartTreatmentParameters.Modes.FirstEntry);
            }

            StartParameters.UpdateAvailableModes(GetNextInstruction(), HasProgress);
            UpdateStartCoordinate();
        }

        public void UpdateMissionConfig(MissionConfig newConfig)
        {
            FlyThroughMode = newConfig.FlyThroughMode;
            TreatmentDuration = newConfig.TreatmentDuration;
            AvailablePayloads = newConfig.AvailablePayloads.ToList();
            SelectedPayload = newConfig.SelectedPayload;
        }

        public void SetStartTreatmentMode(StartTreatmentParameters.Modes mode)
        {
            if (mode == StartParameters.SelectedMode)
                return;
            StartParameters.SelectedMode = mode;
            UpdateStartCoordinate();
            StartModeChanged?.Invoke(this, new StartModeChangedEventArgs() { NewMode = mode });
        }

        private void UpdateStartCoordinate()
        {
            var instruction = GetNextInstruction();

            if (instruction == null)
            {
                StartParameters.StartCoordinate = null;
                NotifyPropertyChanged("StartParameters");
                return;
            }
                
            switch (StartParameters.SelectedMode)
            {
                case StartTreatmentParameters.Modes.FirstEntry:
                    StartParameters.StartCoordinate = instruction.AreaEntryCoordinate;
                    StartParameters.StopAndTurn = false;
                    NotifyPropertyChanged("StartParameters");
                    break;
                case StartTreatmentParameters.Modes.SelectedWaypoint:
                    SetStartCoordToBoundWaypoint();
                    break;
                case StartTreatmentParameters.Modes.LastPositionWaypoint:
                    if (boundStartCoordinateID == null)
                    {
                        CreateWaypointAndSetStartCoord(instruction, LastPosition);
                    }
                    else
                    {
                        var boundIDCoord = instruction.TreatmentRoute.Waypoints.FirstOrDefault(p => p.ID == boundStartCoordinateID);
                        if (boundIDCoord == null)
                        {
                            CreateWaypointAndSetStartCoord(instruction, LastPosition);
                        }
                        else
                        {
                            if (WaypointRoute.IsCoordinateInArea(boundIDCoord, LastPosition, instruction.Swath))
                                StartParameters.StartCoordinate = boundIDCoord.Coordinate;
                            else
                                CreateWaypointAndSetStartCoord(instruction, LastPosition);
                        }
                    }
                    break;
                case StartTreatmentParameters.Modes.LastPositionContinued:
                    StartParameters.StartCoordinate = LastPosition;
                    StartParameters.StopAndTurn = true;
                    NotifyPropertyChanged("StartParameters");
                    break;
            }
        }

        private async void CreateWaypointAndSetStartCoord(TreatmentInstruction instruction, Coordinate position)
        {
            var preceedingWaypoint = instruction.TreatmentRoute.FindWaypointPreceedingCoordinate(position, instruction.Swath);
            if (preceedingWaypoint == null)
                return;
            var newWaypoint = await UGCSClient.InsertWaypointAlongRoute(instruction.TreatmentRoute.Id, preceedingWaypoint.ID, position.X, position.Y);
            boundStartCoordinateID = newWaypoint.ID;
            StartParameters.StartCoordinate = newWaypoint.Coordinate;
            StartParameters.StopAndTurn = newWaypoint.TurnType == "STOP_AND_TURN";
            NotifyPropertyChanged("StartParameters");
        }

        private void SetStartCoordToBoundWaypoint()
        {
            var waypoints = GetNextInstruction().TreatmentRoute.Waypoints;
            var boundCoord = waypoints.FirstOrDefault(p => p.ID == boundStartCoordinateID);

            if (boundCoord != null)
            {
                StartParameters.StartCoordinate = boundCoord.Coordinate;
                StartParameters.StopAndTurn = boundCoord.TurnType == "STOP_AND_TURN";
            }
            else
            {
                boundStartCoordinateID = waypoints.First().ID;
                StartParameters.StartCoordinate = waypoints.First().Coordinate;
                StartParameters.StopAndTurn = waypoints.First().TurnType == "STOP_AND_TURN";
            }

            NotifyPropertyChanged("StartParameters");
        }

        public void SetSelectedWaypoint(string waypointID)
        {
            boundStartCoordinateID = waypointID;
            UpdateStartCoordinate();
        }

        public string GetStartCoordinateString()
        {
            // Returns in radians - latitude then longitude
            string entryString = string.Format(
                "{0},{1}",
                StartParameters.StartCoordinate.X,
                StartParameters.StartCoordinate.Y);
            return entryString;
        }

        public TreatmentInstruction GetNextInstruction()
        {
            foreach (TreatmentInstruction instruction in TreatmentInstructions)
            {
                if (!instruction.Enabled)
                    continue;

                if (instruction.AreaStatus == AreaResult.Types.Status.NotStarted || instruction.AreaStatus == AreaResult.Types.Status.InProgress)
                    return instruction;
            }

            return null;
        }

        public List<TreatmentInstruction> GetRemainingInstructions()
        {
            return TreatmentInstructions.Where(i => i.Enabled && i.AreaStatus != Pbdrone.AreaResult.Types.Status.Finished).ToList();
        }

        private void OnboardComputerClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsDirectorConnected")
            {
                UpdateCanBeModified();
                UpdateCanToggleActivation();
            }
        }

        private void Drone_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "InterfaceState")
            {
                UpdateCanToggleActivation();
            }
        }

        private void MissionData_AreaScanPolygonsUpdated(object sender, AreaScanPolygonsUpdatedArgs e)
        {
            updatingInstructions = true;
            // Remove all instructions that have removed polygons
            foreach (int removedID in e.updates.RemovedRouteIDs)
            {
                var removedInstruction = TreatmentInstructions.FirstOrDefault(i => removedID == i.TreatmentPolygon.Id);
                if (removedInstruction != null)
                    TreatmentInstructions.Remove(removedInstruction);
            }

            InstructionsUpdatedEventArgs updatedInstructions = new InstructionsUpdatedEventArgs();

            foreach (TreatmentInstruction instruction in TreatmentInstructions)
            {
                var newTreatmentArea = e.updates.ModifiedRoutes.FirstOrDefault(r => r.Id == instruction.TreatmentPolygon.Id);
                if (newTreatmentArea != null)
                {
                    // Update treatment areas if they were modified
                    instruction.UpdateTreatmentArea(newTreatmentArea);
                    updatedInstructions.Instructions.Add(instruction);
                }
                else
                {
                    var routeUpdated = instruction.RevalidateTreatmentRoute();
                    if (routeUpdated)
                        updatedInstructions.Instructions.Add(instruction);
                }   
            }

            // Add new treatment areas as instructions
            foreach (AreaScanPolygon addedArea in e.updates.AddedRoutes)
            {
                var newInstruction = new TreatmentInstruction(addedArea);
                newInstruction.EnabledChangedEvent += NewInstruction_EnabledChangedEvent;
                TreatmentInstructions.Add(newInstruction);
            }
                

            StartParameters.UpdateAvailableModes(GetNextInstruction(), HasProgress);
            UpdateStartCoordinate();

            if (updatedInstructions.Instructions.Count > 0)
            {
                InstructionUpdated?.Invoke(this, updatedInstructions);
            }

            updatingInstructions = false;
        }

        private void NewInstruction_EnabledChangedEvent(object sender, EventArgs e)
        {
            if (updatingInstructions)
                return;
            UpdateStartCoordinate();
            InstructionUpdated?.Invoke(
                this,
                new InstructionsUpdatedEventArgs() { Instructions = new List<TreatmentInstruction> { sender as TreatmentInstruction } });
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Deprecated file import code

        //public async void AddRoutesFromFile(StorageFile file)
        //{
        //    foreach (AreaScanPolygon route in await CreateRoutesFromFile(file))
        //    {
        //        AreaScanRoutes.Add(route);
        //    }
        //}

        //public static async Task<MissionData> CreateMissionDataFromFile(StorageFile file)
        //{
        //    List<AreaScanPolygon> routes = await CreateRoutesFromFile(file);
        //    return new MissionData(new ObservableCollection<AreaScanPolygon>(routes));
        //}

        //public static async Task<List<AreaScanPolygon>> CreateRoutesFromFile(StorageFile file)
        //{
        //    string fileText = await FileIO.ReadTextAsync(file);
        //    JObject fileJson = JObject.Parse(fileText);
        //    List<AreaScanPolygon> routes = new List<AreaScanPolygon>();

        //    if (fileJson.ContainsKey("mission"))
        //    {
        //        var routeTokens = fileJson["mission"]["routes"].Children();
        //        foreach (JToken routeToken in routeTokens)
        //        {
        //            routes = routes.Concat(parseRoute(routeToken)).ToList();
        //        }
        //    }
        //    else if (fileJson.ContainsKey("route"))
        //    {
        //        var routeToken = fileJson["route"];
        //        routes = routes.Concat(parseRoute(routeToken)).ToList();
        //    }

        //    return routes;
        //}

        //private static List<AreaScanPolygon> parseRoute(JToken routeToken)
        //{
        //    List<AreaScanPolygon> routes = new List<AreaScanPolygon>();
        //    string routeName = routeToken["name"].ToObject<string>();

        //    foreach (JToken segmentToken in routeToken["segments"].Children())
        //    {
        //        if (segmentToken["type"].ToObject<string>() == "AreaScan")
        //        {
        //            var pointsToken = segmentToken["polygon"]["points"].Children();
        //            List<BasicGeoposition> routeArea = new List<BasicGeoposition>();

        //            foreach (JToken pointToken in pointsToken)
        //            {
        //                var geop = new BasicGeoposition();
        //                geop.Latitude = (180 / Math.PI) * pointToken["latitude"].ToObject<double>();
        //                geop.Longitude = (180 / Math.PI) * pointToken["longitude"].ToObject<double>();

        //                routeArea.Add(geop);
        //            }

        //            routes.Add(new AreaScanPolygon(routeName, new Geopath(routeArea)));
        //        }
        //    }

        //    return routes;
        //}
    }
}
