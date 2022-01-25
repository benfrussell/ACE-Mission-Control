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
    public class InstructionAreasUpdatedArgs
    {
        public List<ITreatmentInstruction> Instructions { get; set; }

        public InstructionAreasUpdatedArgs() 
        { 
            Instructions = new List<ITreatmentInstruction>();
        }
    }

    public class InstructionRouteUpdatedArgs
    {
        public ITreatmentInstruction Instruction { get; set; }

        public InstructionRouteUpdatedArgs() { }
    }

    public class EntryExitPointsUpdatedArgs
    {
        public List<int> UpdatedInstructionIDs { get; set; }
    }

    public class Mission : INotifyPropertyChanged, IMission
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler<InstructionAreasUpdatedArgs> InstructionAreasUpdated;

        public event EventHandler<InstructionRouteUpdatedArgs> InstructionRouteUpdated;

        // Triggered when an instruction's entry/exit points update OR a start mode change causes an entry/exit update
        public event EventHandler<EventArgs> StartStopPointsUpdated;

        public event EventHandler<EventArgs> ProgressReset;

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

        public bool MissionHasProgress
        {
            get => TreatmentInstructions.Any(i => i.AreaStatus != MissionRoute.Types.Status.NotStarted);
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

        private bool locked;
        public bool Locked
        {
            get => locked;
            private set
            {
                if (locked == value)
                    return;
                locked = value;
                NotifyPropertyChanged();
            }
        }

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
                    StartStopPointsUpdated?.Invoke(this, new EventArgs());
            }
        }

        public bool StopAndTurnStartMode { get => startParameters.StopAndTurn; }

        // Used to suppress EnabledChanged events causing InstructionUpdated events to be sent out while updating instructions
        // A single InstructionUpdated event will already be sent out after updating instructions
        private bool updatingInstructions;

        // StartParameters is kept private because updating it incorrectly can cause unexpected behaviour
        private StartTreatmentParameters startParameters;

        public ObservableCollection<ITreatmentInstruction> TreatmentInstructions { get; set; }

        public Mission()
        {
            MissionRetriever.AreaScanPolygonsUpdated += MissionRetriever_AreaScanPolygonsUpdated;
            TreatmentInstructions = new ObservableCollection<ITreatmentInstruction>();
            startParameters = new StartTreatmentParameters();
            startParameters.SelectedModeChangedEvent += StartParameters_SelectedModeChangedEvent;

            Activated = false;
            Lock();
        }

        // Lock or unlock user changes to the mission
        public void Lock()
        {
            Locked = true;
            CanBeModified = false;
            UpdateCanUpload();
            UpdateCanBeReset();
            UpdateCanToggleActivation();
        }

        public void Unlock()
        {
            Locked = false;
            CanBeModified = !Activated;
            UpdateCanUpload();
            UpdateCanBeReset();
            UpdateCanToggleActivation();
        }

        private void UpdateCanUpload()
        {
            CanUpload =
                CanBeModified &&
                TreatmentInstructions.Any(i => i.Enabled && i.CurrentUploadStatus != TreatmentInstruction.UploadStatus.Uploaded);
        }

        private void UpdateCanBeReset()
        {
            CanBeReset = CanBeModified && MissionHasProgress;
        }

        private void UpdateCanToggleActivation()
        {
            CanToggleActivation = MissionSet;
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
            CanBeModified = !Locked & !Activated;

            UpdateCanBeReset();
            UpdateCanUpload();

            if (newStatus.LastLongitude != 0 && newStatus.LastLatitude != 0)
            {
                LastPosition = new Coordinate(
                  (newStatus.LastLongitude / 180) * Math.PI,
                  (newStatus.LastLatitude / 180) * Math.PI);
                NotifyPropertyChanged("MissionHasProgress");
            }
            else if (!MissionHasProgress)
            {
                LastPosition = null;
            }

            bool changes = startParameters.UpdateParameters(GetNextInstruction(), LastPosition, justReturned);
            if (changes || LastPosition != null)
                StartStopPointsUpdated?.Invoke(this, new EventArgs());
        }

        public void SetInstructionAreaStatus(int instructionID, MissionRoute.Types.Status status)
        {
            var instruction = TreatmentInstructions.FirstOrDefault(i => i.ID == instructionID);
            if (instruction == null)
                return;


            if (instruction.AreaStatus != status)
            {
                instruction.AreaStatus = status;
                // This uses the status result to determine if the instruction came from a previous upload
                // This does really make sense here anymore, it came from the original UpdateMissionStatus method
                if (instruction.CurrentUploadStatus == TreatmentInstruction.UploadStatus.NotUploaded)
                    instruction.CurrentUploadStatus = TreatmentInstruction.UploadStatus.PreviousUpload;
            }
        }

        // Takes a complete or partial list of instruction IDs and orders our list to match it
        public void ReorderInstructionsByID(List<int> orderedIDs)
        {
            var originalIDs = TreatmentInstructions.Select(i => i.ID);

            // Intersect the lists so the ordered IDs only contains IDs that we have
            orderedIDs = orderedIDs.Intersect(originalIDs).ToList();

            // If the ordered IDs are already in the same sequence, then skip
            if (originalIDs.SequenceEqual(orderedIDs))
                return;

            // Add any new IDs that exist in the original list to the ordered list, so OrderBy can find every ID somewhere
            orderedIDs.AddRange(originalIDs.Except(orderedIDs));

            var reorderedInstructions = TreatmentInstructions.OrderBy(i => orderedIDs.IndexOf(i.ID)).ToList();
            TreatmentInstructions.Clear();
            foreach (ITreatmentInstruction instruction in reorderedInstructions)
                TreatmentInstructions.Add(instruction);

            UpdateInstructionOrderValues();

            InstructionAreasUpdated?.Invoke(this, new InstructionAreasUpdatedArgs() { Instructions = TreatmentInstructions.ToList() });
        }

        public void ReorderInstruction(ITreatmentInstruction instruction, int newPosition)
        {
            var displacedInstruction = TreatmentInstructions.ElementAtOrDefault(newPosition);

            TreatmentInstructions.Remove(instruction);
            TreatmentInstructions.Insert(newPosition, instruction);

            UpdateInstructionOrderValues();

            bool changes = startParameters.UpdateParameters(GetNextInstruction(), LastPosition, false);
            if (changes)
                StartStopPointsUpdated?.Invoke(this, new EventArgs());

            var updatedInstructions = new List<ITreatmentInstruction> { instruction };
            if (displacedInstruction != null)
                updatedInstructions.Add(displacedInstruction);

            InstructionAreasUpdated?.Invoke(this, new InstructionAreasUpdatedArgs { Instructions = updatedInstructions });
        }

        public Coordinate GetStartCoordinate(int instructionID)
        {
            var instruction = TreatmentInstructions.FirstOrDefault(i => i.ID == instructionID);
            if (instruction == null)
                return null;

            if (instruction.FirstInstruction)
                return startParameters.StartCoordinate;
            else
                return instruction.AreaEntryCoordinate;
        }

        public Coordinate GetStopCoordinate(int instructionID)
        {
            var instruction = TreatmentInstructions.FirstOrDefault(i => i.ID == instructionID);
            if (instruction == null)
                return null;

            return instruction.AreaExitCoordinate;
        }

        public string GetStartCoordinateString(int instructionID)
        {
            var coordinate = GetStartCoordinate(instructionID);
            if (coordinate == null)
                return null;

            // Returns in radians - latitude,longitude
            string startString = string.Format(
                "{0},{1}",
                coordinate.Y,
                coordinate.X);
            return startString;
        }

        public string GetStopCoordinateString(int instructionID)
        {
            var coordinate = GetStopCoordinate(instructionID);
            if (coordinate == null)
                return null;

            // Returns in radians - latitude,longitude
            string stopString = string.Format(
                "{0},{1}",
                coordinate.Y,
                coordinate.X);
            return stopString;
        }

        public long GetLastPropertyModificationTime(int instructionID)
        {
            var instruction = TreatmentInstructions.FirstOrDefault(i => i.ID == instructionID);
            if (instruction == null)
                return 0;

            // For the first instruction, the start parameter modification time takes priority if it's more recent
            if (instruction.FirstInstruction && startParameters.LastStartPropertyModification > instruction.LastSyncedPropertyModification)
                return startParameters.LastStartPropertyModification;
            else
                return instruction.LastSyncedPropertyModification;
        }

        private void UpdateInstructionOrderValues()
        {
            int missionOrder = 1;
            int listPosition = 1;
            var lastInstructionID = TreatmentInstructions.LastOrDefault(i => i.Enabled && i.AreaStatus != MissionRoute.Types.Status.Finished)?.ID;

            foreach (ITreatmentInstruction instruction in TreatmentInstructions)
            {
                // Tell the instruction what it's current placement in the list is
                if (instruction.Enabled)
                {
                    instruction.SetOrder(
                        missionOrder,
                        missionOrder == 1,
                        instruction.ID == lastInstructionID,
                        listPosition == 1,
                        listPosition == TreatmentInstructions.Count);
                    missionOrder++;
                }
                else
                {
                    instruction.SetOrder(null, false, false, listPosition == 1, listPosition == TreatmentInstructions.Count);
                }
                listPosition++;
            }
        }

        public void ResetStatus()
        {
            Activated = false;
            CanBeModified = false;
            Stage = MissionStatus.Types.Stage.NotActivated;
        }

        public void ResetProgress()
        {
            UpdateCanBeReset();
            if (CanBeReset)
            {
                foreach (ITreatmentInstruction instruction in TreatmentInstructions)
                {
                    if (instruction.AreaStatus != MissionRoute.Types.Status.NotStarted)
                    {
                        instruction.AreaStatus = MissionRoute.Types.Status.NotStarted;
                    }
                }

                LastPosition = null;
                bool changes = startParameters.UpdateParameters(GetNextInstruction(), LastPosition, false);
                if (changes)
                    StartStopPointsUpdated?.Invoke(this, new EventArgs());

                UpdateCanBeReset();
                NotifyPropertyChanged("MissionHasProgress");

                ProgressReset?.Invoke(this, new EventArgs());
            }
        }

        public void SetSelectedStartWaypoint(string waypointID)
        {
            if (waypointID == startParameters.BoundStartWaypointID)
                return;
            startParameters.SetSelectedWaypoint(waypointID, GetNextInstruction());
            StartStopPointsUpdated?.Invoke(this, new EventArgs());
        }

        public ITreatmentInstruction GetNextInstruction()
        {
            foreach (ITreatmentInstruction instruction in TreatmentInstructions)
            {
                if (!instruction.Enabled)
                    continue;

                if (instruction.AreaStatus == MissionRoute.Types.Status.NotStarted || instruction.AreaStatus == MissionRoute.Types.Status.InProgress)
                    return instruction;
            }

            return null;
        }

        public List<ITreatmentInstruction> GetRemainingInstructions()
        {
            return TreatmentInstructions.Where(i => i.Enabled && i.AreaStatus != MissionRoute.Types.Status.Finished).ToList();
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

        private void MissionRetriever_AreaScanPolygonsUpdated(object sender, AreaScanPolygonsUpdatedArgs e)
        {
            updatingInstructions = true;
            // Remove all instructions that have removed polygons
            foreach (int removedID in e.Updates.RemovedRouteIDs)
            {
                var removedInstruction = TreatmentInstructions.FirstOrDefault(i => removedID == i.ID);
                if (removedInstruction != null)
                    TreatmentInstructions.Remove(removedInstruction);
            }

            InstructionAreasUpdatedArgs updatedInstructions = new InstructionAreasUpdatedArgs();

            foreach (ITreatmentInstruction instruction in TreatmentInstructions)
            {
                var instructionWithModifiedArea = e.Updates.ModifiedRoutes.FirstOrDefault(r => r.Id == instruction.TreatmentPolygon.Id);
                if (instructionWithModifiedArea != null)
                {
                    // Update treatment areas if they were modified
                    instruction.UpdateTreatmentArea(instructionWithModifiedArea);
                    UpdateCanUpload();
                    updatedInstructions.Instructions.Add(instruction);
                }
                // If this instruction does not matche any area that was modified, then there would not have been any change to it...
                // So there should be no need to revalidate the treatment route here
                //else
                //{
                //    var routeUpdated = instruction.RevalidateTreatmentRoute();
                //    if (routeUpdated)
                //        updatedInstructions.Instructions.Add(instruction);
                //}
            }

            // Add new treatment areas as instructions
            foreach (AreaScanPolygon addedArea in e.Updates.AddedRoutes)
            {
                var newInstruction = new TreatmentInstruction(addedArea);
                newInstruction.PropertyChanged += NewInstruction_PropertyChanged;
                updatedInstructions.Instructions.Add(newInstruction);
                TreatmentInstructions.Add(newInstruction);
            }

            UpdateInstructionOrderValues();

            bool changes = startParameters.UpdateParameters(GetNextInstruction(), LastPosition, false);
            if (changes)
                StartStopPointsUpdated?.Invoke(this, new EventArgs());

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

            var instruction = sender as ITreatmentInstruction;

            switch (e.PropertyName)
            {
                case "Enabled":
                    // If the instruction was the first or last instruction, this call will trigger two extra property updates because of the first/last instruction properties
                    // It could be more smartly handled by creating a "smart instruction list" which puts the related property updates out in one go
                    UpdateInstructionOrderValues();
                    UpdateCanUpload();
                    InstructionAreasUpdated?.Invoke(this, new InstructionAreasUpdatedArgs { Instructions = new List<ITreatmentInstruction> { instruction } });
                    break;
                case "TreatmentRoute":
                    InstructionRouteUpdated?.Invoke(this, new InstructionRouteUpdatedArgs { Instruction = instruction });
                    break;
                case "FirstInstruction":
                    if (instruction.FirstInstruction)
                    {
                        bool changes = startParameters.UpdateParameters(GetNextInstruction(), LastPosition, false);
                        if (changes)
                            StartStopPointsUpdated?.Invoke(this, new EventArgs());
                    }

                    InstructionAreasUpdated?.Invoke(this, new InstructionAreasUpdatedArgs { Instructions = new List<ITreatmentInstruction> { instruction } });
                    break;
                case "LastInstruction":
                    InstructionAreasUpdated?.Invoke(this, new InstructionAreasUpdatedArgs { Instructions = new List<ITreatmentInstruction> { instruction } });
                    break;
                default:
                    break;
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
