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
    public class InstructionAreasUpdatedEventArgs
    {
        public List<TreatmentInstruction> Instructions { get; set; }

        public InstructionAreasUpdatedEventArgs() 
        { 
            Instructions = new List<TreatmentInstruction>();
        }
    }

    public class StartModeChangedEventArgs
    {
        public StartTreatmentParameters.Mode NewMode { get; set; }
    }

    public class Mission : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler<InstructionAreasUpdatedEventArgs> InstructionAreasUpdated;

        public event EventHandler<EventArgs> StartParametersChangedEvent;

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

        private bool droneHasProgress;
        public bool DroneHasProgress
        {
            get => droneHasProgress;
            private set
            {
                if (droneHasProgress == value)
                    return;
                droneHasProgress = value;
                NotifyPropertyChanged();
            }
        }

        public bool MissionControlHasProgress
        {
            get => TreatmentInstructions.Any(i => i.AreaStatus != AreaResult.Types.Status.NotStarted);
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
        private void UpdateCanBeReset() { CanBeReset = CanBeModified && (MissionControlHasProgress || (DroneHasProgress && MissionSet)); }

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

        private bool canUpload;
        public bool CanUpload
        {
            get => canUpload;
            private set
            {
                if (canUpload == value)
                    return;
                canUpload = value;
                NotifyPropertyChanged();
            }
        }
        private void UpdateCanUpload() 
        {
            CanUpload =
                CanBeModified &&
                TreatmentInstructions.Any(i => i.Enabled && i.CurrentUploadStatus != TreatmentInstruction.UploadStatus.Uploaded);
        }

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

        public StartTreatmentParameters.Mode StartMode 
        {
            get => startParameters.SelectedMode;
            set
            {
                if (startParameters.SelectedMode == value)
                    return;
                startParameters.SelectedMode = value;
                bool changes = startParameters.UpdateParameters(GetNextInstruction(), LastPosition, false);
                if (changes)
                    StartParametersChangedEvent?.Invoke(this, new EventArgs());
            }
        }

        public bool StopAndTurnStartMode { get => startParameters.StopAndTurn; }

        // Used to suppress EnabledChanged events causing InstructionUpdated events to be sent out while updating instructions
        // A single InstructionUpdated event will already be sent out after updating instructions
        private bool updatingInstructions;

        private Drone drone;
        private OnboardComputerClient onboardComputer;

        // StartParameters is kept private because updating it incorrectly can cause unexpected behaviour
        private StartTreatmentParameters startParameters;

        public ObservableCollection<TreatmentInstruction> TreatmentInstructions;

        public Mission(Drone _drone, OnboardComputerClient _onboardComputer)
        {
            MissionRetriever.AreaScanPolygonsUpdated += MissionData_AreaScanPolygonsUpdated;
            TreatmentInstructions = new ObservableCollection<TreatmentInstruction>();
            startParameters = new StartTreatmentParameters();
            startParameters.SelectedModeChangedEvent += StartParameters_SelectedModeChangedEvent;

            drone = _drone;
            drone.PropertyChanged += Drone_PropertyChanged;
            onboardComputer = _onboardComputer;
            onboardComputer.PropertyChanged += OnboardComputerClient_PropertyChanged;

            Activated = false;
            DroneHasProgress = false;

            UpdateCanBeModified();
            UpdateCanUpload();
            UpdateCanBeReset();
            UpdateCanToggleActivation();
        }

        private void StartParameters_SelectedModeChangedEvent(object sender, EventArgs e)
        {
            NotifyPropertyChanged("StartMode");
        }

        public void UpdateMissionStatus(MissionStatus newStatus)
        {
            bool justReturned = 
                (Stage != MissionStatus.Types.Stage.Returning && 
                Stage != MissionStatus.Types.Stage.Override) &&
                (newStatus.MissionStage == MissionStatus.Types.Stage.Returning || 
                newStatus.MissionStage == MissionStatus.Types.Stage.Override);

            Stage = newStatus.MissionStage;

            Activated = newStatus.Activated;
            UpdateCanBeModified();

            DroneHasProgress = newStatus.InProgress;
            UpdateCanBeReset();

            // Update the treatment instruction statuses with any results that came from the mission status update
            foreach (TreatmentInstruction instruction in TreatmentInstructions)
            {
                var resultInStatus = newStatus.Results.FirstOrDefault(r => r.AreaID == instruction.ID);
                if (resultInStatus != null && instruction.AreaStatus != resultInStatus.Status)
                    instruction.AreaStatus = resultInStatus.Status;
                else if (resultInStatus == null)
                    instruction.CurrentUploadStatus = TreatmentInstruction.UploadStatus.NotUploaded;
            }

            UpdateCanUpload();

            if (newStatus.Results.Count() > 1)
            {
                // If the order of results we get from the mission status is different than what we have, reorder
                var resultIDs = newStatus.Results.Select(r => r.AreaID).ToList();
                var originalIDs = TreatmentInstructions.Select(i => i.ID);

                if (!originalIDs.SequenceEqual(resultIDs))
                    ReorderInstructionsByID(resultIDs);
            }

            if (DroneHasProgress && newStatus.LastLongitude != 0 && newStatus.LastLatitude != 0)
            {
                LastPosition = new Coordinate(
                  (newStatus.LastLongitude / 180) * Math.PI,
                  (newStatus.LastLatitude / 180) * Math.PI);
            }
            else if (!MissionControlHasProgress)
            {
                LastPosition = null;
            }

            bool changes = startParameters.UpdateParameters(GetNextInstruction(), LastPosition, justReturned);
            if (changes)
                StartParametersChangedEvent?.Invoke(this, new EventArgs());
        }

        // Takes a complete or partial list of instruction IDs and orders our list to match it
        private void ReorderInstructionsByID(List<int> orderedIDs)
        {
            var originalIDs = TreatmentInstructions.Select(i => i.ID);
            // Add any new IDs that exist in the original list to the ordered list, so OrderBy can find every ID somewhere
            orderedIDs.AddRange(originalIDs.Except(orderedIDs));

            var reorderedInstructions = TreatmentInstructions.OrderBy(i => orderedIDs.IndexOf(i.ID)).ToList();
            TreatmentInstructions.Clear();
            foreach (TreatmentInstruction instruction in reorderedInstructions)
                TreatmentInstructions.Add(instruction);

            UpdateInstructionOrderValues();

            InstructionAreasUpdated?.Invoke(this, new InstructionAreasUpdatedEventArgs() { Instructions = TreatmentInstructions.ToList() });
        }

        public void ReorderInstruction(TreatmentInstruction instruction, int newPosition)
        {
            var displacedInstruction = TreatmentInstructions.ElementAtOrDefault(newPosition);

            TreatmentInstructions.Remove(instruction);
            TreatmentInstructions.Insert(newPosition, instruction);

            UpdateInstructionOrderValues();

            bool changes = startParameters.UpdateParameters(GetNextInstruction(), LastPosition, false);
            if (changes)
                StartParametersChangedEvent?.Invoke(this, new EventArgs());

            var updatedInstructions = new List<TreatmentInstruction> { instruction };
            if (displacedInstruction != null)
                updatedInstructions.Add(displacedInstruction);

            InstructionAreasUpdated?.Invoke(this, new InstructionAreasUpdatedEventArgs { Instructions = updatedInstructions });
        }

        private void UpdateInstructionOrderValues()
        {
            int order = 1;
            int position = 1;
            var lastInstructionID = TreatmentInstructions.Last(i => i.Enabled && i.AreaStatus != AreaResult.Types.Status.Finished).ID;

            foreach (TreatmentInstruction instruction in TreatmentInstructions)
            {
                if (instruction.Enabled)
                {
                    instruction.SetOrder(
                        order, 
                        order == 1, 
                        instruction.ID == lastInstructionID, 
                        position == 1, 
                        position == TreatmentInstructions.Count);
                    order++;
                }
                else
                {
                    instruction.SetOrder(null, false, false, position == 1, position == TreatmentInstructions.Count);
                }
                position++;
            }
        }

        public void UpdateMissionConfig(MissionConfig newConfig)
        {
            FlyThroughMode = newConfig.FlyThroughMode;
            TreatmentDuration = newConfig.TreatmentDuration;
            AvailablePayloads = newConfig.AvailablePayloads.ToList();
            SelectedPayload = newConfig.SelectedPayload;

            // A mission is set if the config has areas
            MissionSet = newConfig.Areas == null ? false : newConfig.Areas.Count > 0;
            UpdateCanBeReset();
            UpdateCanToggleActivation();
        }

        public void ResetProgress()
        {
            if (MissionControlHasProgress)
            {
                foreach (TreatmentInstruction instruction in TreatmentInstructions)
                {
                    if (instruction.AreaStatus != AreaResult.Types.Status.NotStarted)
                    {
                        instruction.AreaStatus = AreaResult.Types.Status.NotStarted;
                    }
                }

                LastPosition = null;
                bool changes = startParameters.UpdateParameters(GetNextInstruction(), LastPosition, false);
                if (changes)
                    StartParametersChangedEvent?.Invoke(this, new EventArgs());
            }

            if (DroneHasProgress && CanBeModified && MissionSet)
            {
                drone.SendCommand("reset_mission");
            }
            else
            {
                // If we're able to set a reset_mission command, then CanBeReset will be updated after the command response is received
                // If not, update right away
                UpdateCanBeReset();
            }
        }

        public void SetSelectedStartWaypoint(string waypointID)
        {
            if (waypointID == startParameters.BoundStartWaypointID)
                return;
            startParameters.SetSelectedWaypoint(waypointID, GetNextInstruction());
            StartParametersChangedEvent?.Invoke(this, new EventArgs());
        }

        public Coordinate GetStartCoordinate()
        {
            return startParameters.StartCoordinate;
        }

        public string GetStartCoordinateString()
        {
            // Returns in radians - latitude then longitude
            string entryString = string.Format(
                "{0},{1}",
                startParameters.StartCoordinate.Y,
                startParameters.StartCoordinate.X);
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

        public void SetInstructionUploaded(int id)
        {
            var instruction = TreatmentInstructions.FirstOrDefault(i => i.ID == id);
            if (instruction != null)
            {
                instruction.CurrentUploadStatus = TreatmentInstruction.UploadStatus.Uploaded;
                UpdateCanUpload();
            }
        }

        private void OnboardComputerClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsDirectorConnected")
            {
                UpdateCanBeModified();
                UpdateCanUpload();
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
                UpdateCanUpload();
                UpdateCanBeReset();
            }
        }

        private void MissionData_AreaScanPolygonsUpdated(object sender, AreaScanPolygonsUpdatedArgs e)
        {
            updatingInstructions = true;
            // Remove all instructions that have removed polygons
            foreach (int removedID in e.updates.RemovedRouteIDs)
            {
                var removedInstruction = TreatmentInstructions.FirstOrDefault(i => removedID == i.ID);
                if (removedInstruction != null)
                    TreatmentInstructions.Remove(removedInstruction);
            }

            InstructionAreasUpdatedEventArgs updatedInstructions = new InstructionAreasUpdatedEventArgs();

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
                newInstruction.PropertyChanged += NewInstruction_PropertyChanged;
                updatedInstructions.Instructions.Add(newInstruction);
                TreatmentInstructions.Add(newInstruction);
            }

            UpdateInstructionOrderValues();

            bool changes = startParameters.UpdateParameters(GetNextInstruction(), LastPosition, false);
            if (changes)
                StartParametersChangedEvent?.Invoke(this, new EventArgs());

            if (updatedInstructions.Instructions.Count > 0)
            {
                InstructionAreasUpdated?.Invoke(this, updatedInstructions);
            }

            updatingInstructions = false;
        }

        private void NewInstruction_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // If we allow this while updating instructions then it will update the start parameters for each instruction added / modified(potentially)
            // When instructions are updated it will update the parameters just once at the end
            if (updatingInstructions)
                return;

            if (e.PropertyName == "Enabled")
            {
                UpdateInstructionOrderValues();

                bool changes = startParameters.UpdateParameters(GetNextInstruction(), LastPosition, false);
                if (changes)
                    StartParametersChangedEvent?.Invoke(this, new EventArgs());

                var updatedInstruction = new List<TreatmentInstruction> { sender as TreatmentInstruction };
                InstructionAreasUpdated?.Invoke(this, new InstructionAreasUpdatedEventArgs { Instructions = updatedInstruction });
            }
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
