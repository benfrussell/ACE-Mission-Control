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
        public bool Reorder { get; set; }

        public InstructionsUpdatedEventArgs() 
        { 
            Instructions = new List<TreatmentInstruction>();
            Reorder = false;
        }
    }

    public class StartModeChangedEventArgs
    {
        public StartTreatmentParameters.Modes NewMode { get; set; }
    }

    public class Mission : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

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
        private void UpdateCanBeReset() { CanBeReset = HasProgress && CanBeModified && MissionSet; }

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
        private void UpdateCanBeModified() { CanBeModified = onboardComputer.IsDirectorConnected && !Activated && drone.Synchronized; }

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
        private void UpdateCanToggleActivation() { CanToggleActivation = onboardComputer.IsDirectorConnected && drone.InterfaceState == InterfaceStatus.Types.State.Online && MissionSet; }

        private bool missionSet;
        public bool MissionSet
        {
            get => missionSet;
            private set
            {
                if (missionSet == value)
                    return;
                missionSet = value;
                NotifyPropertyChanged();
            }
        }

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
        private string boundStartWaypointID;
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
            HasProgress = false;

            UpdateCanBeModified();
            UpdateCanBeReset();
            UpdateCanToggleActivation();

            // test
            // LastPosition = new Coordinate(-1.32579946805, 0.791221070472);

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

            var updatedInstructions = new InstructionsUpdatedEventArgs();
            // Update the treatment instruction statuses with any results that came from the mission status update
            foreach (TreatmentInstruction instruction in TreatmentInstructions)
            {
                var resultInStatus = newStatus.Results.FirstOrDefault(r => r.AreaID == instruction.TreatmentPolygon.Id);
                if (resultInStatus != null && instruction.AreaStatus != resultInStatus.Status)
                {
                    System.Diagnostics.Debug.WriteLine($"Updating instruction {instruction.Name} {resultInStatus.Status.ToString()}");
                    instruction.AreaStatus = resultInStatus.Status;
                    updatedInstructions.Instructions.Add(instruction);
                }
                    
            }

            if (newStatus.Results.Count() > 1)
            {
                // If the order of results we get from the mission status is different than what we have, reorder
                var resultIDs = newStatus.Results.Select(r => r.AreaID).ToList();
                var originalIDs = TreatmentInstructions.Select(i => i.TreatmentPolygon.Id);

                if (!originalIDs.SequenceEqual(resultIDs))
                    ReorderInstructionsByID(resultIDs);
            }
            else if (updatedInstructions.Instructions.Count > 0)
            {
                InstructionUpdated?.Invoke(this, updatedInstructions);
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
            else
            {
                // If we were on lastposition mode, but no last long or lat was provided, switch back to first entry
                if (StartParameters.SelectedMode == StartTreatmentParameters.Modes.LastPositionContinued)
                    SetStartTreatmentMode(StartTreatmentParameters.Modes.FirstEntry);
                LastPosition = null;
            }

            StartParameters.UpdateAvailableModes(GetNextInstruction(), HasProgress);
            UpdateStartCoordinate();
        }

        // Takes a complete or partial list of instruction IDs and orders our list to match it
        private void ReorderInstructionsByID(List<int> orderedIDs)
        {
            var originalIDs = TreatmentInstructions.Select(i => i.TreatmentPolygon.Id);
            // Add any new IDs that exist in the original list to the ordered list, so OrderBy can find every ID somewhere
            orderedIDs.AddRange(originalIDs.Except(orderedIDs));

            var reorderedInstructions = TreatmentInstructions.OrderBy(i => orderedIDs.IndexOf(i.TreatmentPolygon.Id)).ToList();
            TreatmentInstructions.Clear();
            foreach (TreatmentInstruction instruction in reorderedInstructions)
                TreatmentInstructions.Add(instruction);

            InstructionUpdated?.Invoke(this, new InstructionsUpdatedEventArgs() { Instructions = TreatmentInstructions.ToList(), Reorder = true });
        }

        public void UpdateMissionConfig(MissionConfig newConfig)
        {
            FlyThroughMode = newConfig.FlyThroughMode;
            System.Diagnostics.Debug.WriteLine($"Duration: {newConfig.TreatmentDuration}");
            TreatmentDuration = newConfig.TreatmentDuration;
            AvailablePayloads = newConfig.AvailablePayloads.ToList();
            SelectedPayload = newConfig.SelectedPayload;

            // A mission is set if the config has areas
            MissionSet = newConfig.Areas == null ? false : newConfig.Areas.Count > 0;
            UpdateCanBeReset();
            UpdateCanToggleActivation();
        }

        public void SetStartTreatmentMode(StartTreatmentParameters.Modes mode)
        {
            if (mode == StartParameters.SelectedMode)
                return;
            StartParameters.SelectedMode = mode;
            UpdateStartCoordinate();
        }

        private void UpdateStartCoordinate()
        {
            var instruction = GetNextInstruction();
            if (instruction == null || !instruction.IsTreatmentRouteValid())
                return;

            //if (instruction == null)
            //{
            //    StartParameters.StartCoordinate = null;
            //    NotifyPropertyChanged("StartParameters");
            //    return;
            //}
                
            switch (StartParameters.SelectedMode)
            {
                case StartTreatmentParameters.Modes.FirstEntry:
                    StartParameters.SetStartParameters(instruction.AreaEntryCoordinate, false);
                    break;
                case StartTreatmentParameters.Modes.SelectedWaypoint:
                    SetStartCoordToBoundWaypoint();
                    break;
                case StartTreatmentParameters.Modes.LastPositionWaypoint:
                    if (boundStartWaypointID == null)
                    {
                        CreateWaypointAndSetStartCoord(instruction, LastPosition);
                    }
                    else
                    {
                        var boundWaypoint = instruction.TreatmentRoute.Waypoints.FirstOrDefault(p => p.ID == boundStartWaypointID);
                        if (boundWaypoint == null)
                        {
                            CreateWaypointAndSetStartCoord(instruction, LastPosition);
                        }
                        else
                        {
                            if (WaypointRoute.IsCoordinateInArea(boundWaypoint, LastPosition, instruction.Swath))
                                StartParameters.SetStartParameters(boundWaypoint.Coordinate, boundWaypoint.TurnType == "STOP_AND_TURN");
                            else
                                CreateWaypointAndSetStartCoord(instruction, LastPosition);
                        }
                    }
                    break;
                case StartTreatmentParameters.Modes.LastPositionContinued:
                    StartParameters.SetStartParameters(LastPosition, true);
                    break;
            }
        }

        private async void CreateWaypointAndSetStartCoord(TreatmentInstruction instruction, Coordinate position)
        {
            var preceedingWaypoint = instruction.TreatmentRoute.FindWaypointPreceedingCoordinate(position, instruction.Swath);
            if (preceedingWaypoint == null)
                return;
            var newWaypoint = await UGCSClient.InsertWaypointAlongRoute(instruction.TreatmentRoute.Id, preceedingWaypoint.ID, position.X, position.Y);

            boundStartWaypointID = newWaypoint.ID;

            StartParameters.SetStartParameters(newWaypoint.Coordinate, newWaypoint.TurnType == "STOP_AND_TURN");
        }

        private void SetStartCoordToBoundWaypoint()
        {
            var waypoints = GetNextInstruction().TreatmentRoute.Waypoints;
            var boundCoord = waypoints.FirstOrDefault(p => p.ID == boundStartWaypointID);

            if (boundCoord != null)
            {
                StartParameters.SetStartParameters(boundCoord.Coordinate, boundCoord.TurnType == "STOP_AND_TURN");
            }
            else
            {
                boundStartWaypointID = waypoints.First().ID;
                StartParameters.SetStartParameters(waypoints.First().Coordinate, waypoints.First().TurnType == "STOP_AND_TURN");
            }
        }

        public void SetSelectedWaypoint(string waypointID)
        {
            boundStartWaypointID = waypointID;
            UpdateStartCoordinate();
        }

        public string GetStartCoordinateString()
        {
            // Returns in radians - latitude then longitude
            string entryString = string.Format(
                "{0},{1}",
                StartParameters.StartCoordinate.Y,
                StartParameters.StartCoordinate.X);
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
            else if (e.PropertyName == "Synchronized")
            {
                // Don't allow the user to affect the mission if the drone's state isn't syncronized to avoid trouble
                UpdateCanBeModified();
                UpdateCanBeReset();
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
                System.Diagnostics.Debug.WriteLine("added area ID is " + addedArea.Id);
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
