﻿using System;
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

    public class InstructionSyncedPropertyUpdatedArgs
    {
        public int InstructionID { get; set; }
        public List<string> UpdatedParameters { get; set; }

        public InstructionSyncedPropertyUpdatedArgs(int instructionID, List<string> parameters)
        {
            InstructionID = instructionID;
            UpdatedParameters = parameters;
        }
    }

    public class Mission : INotifyPropertyChanged, IMission
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler<InstructionAreasUpdatedArgs> InstructionAreasUpdated;

        public event EventHandler<InstructionRouteUpdatedArgs> InstructionRouteUpdated;

        // Triggered when an instruction's entry/exit points update OR a start mode change causes an entry/exit update
        public event EventHandler<InstructionSyncedPropertyUpdatedArgs> InstructionSyncedPropertyUpdated;

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
                startParameters.UpdateParameters(GetNextInstruction(), LastPosition, false);
            }
        }

        // Used to suppress EnabledChanged events causing InstructionUpdated events to be sent out while updating instructions
        // A single InstructionUpdated event will already be sent out after updating instructions
        private bool updatingInstructions;

        // StartParameters is kept private because updating it incorrectly can cause unexpected behaviour
        private StartTreatmentParameters startParameters;

        public ObservableCollection<ITreatmentInstruction> TreatmentInstructions { get; set; }

        public Mission()
        {
            TreatmentInstructions = new ObservableCollection<ITreatmentInstruction>();

            MissionRetriever.AreaScanPolygonsUpdated += MissionRetriever_AreaScanPolygonsUpdated;
            startParameters = new StartTreatmentParameters();
            startParameters.SelectedModeChangedEvent += StartParameters_SelectedModeChangedEvent;
            startParameters.StartParametersChanged += StartParameters_StartParametersChanged;

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

        private void StartParameters_StartParametersChanged(object sender, StartParametersChangedArgs e)
        {
            InstructionSyncedPropertyUpdated?.Invoke(this, new InstructionSyncedPropertyUpdatedArgs(GetNextInstruction().ID, e.ParameterNames));
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

            startParameters.UpdateParameters(GetNextInstruction(), LastPosition, justReturned);
        }

        public void SetInstructionAreaStatus(int instructionID, MissionRoute.Types.Status status)
        {
            var instruction = TreatmentInstructions.FirstOrDefault(i => i.ID == instructionID);
            if (instruction == null)
                return;

            instruction.AreaStatus = status;
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

            UpdateInstructionOrder();

            InstructionAreasUpdated?.Invoke(this, new InstructionAreasUpdatedArgs() { Instructions = TreatmentInstructions.ToList() });
        }

        public void ReorderInstruction(ITreatmentInstruction instruction, int newPosition)
        {
            var displacedInstruction = TreatmentInstructions.ElementAtOrDefault(newPosition);

            TreatmentInstructions.Remove(instruction);
            TreatmentInstructions.Insert(newPosition, instruction);

            UpdateInstructionOrder();

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
                return instruction.AreaEntryExitCoordinates.Item1;
        }

        public Coordinate GetStartCoordinate()
        {
            return startParameters.StartCoordinate;
        }

        public Coordinate GetStopCoordinate(int instructionID)
        {
            var instruction = TreatmentInstructions.FirstOrDefault(i => i.ID == instructionID);
            if (instruction == null)
                return null;

            return instruction.AreaEntryExitCoordinates.Item2;
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

        public long GetLastAreaModificationTime(int instructionID)
        {
            var instruction = TreatmentInstructions.FirstOrDefault(i => i.ID == instructionID);
            if (instruction == null)
                return 0;

            return instruction.TreatmentPolygon.LastModificationTime;
        }

        public ITreatmentInstruction GetInstructionByID(int instructionID)
        {
            return TreatmentInstructions.FirstOrDefault(i => i.ID == instructionID);
        }

        public ACEEnums.TurnType GetStartingTurnType(int instructionID)
        {
            var instruction = TreatmentInstructions.FirstOrDefault(i => i.ID == instructionID);
            if (instruction == null)
                return ACEEnums.TurnType.NotSpecified;

            // Assuming all area entries that are not the first instruction will be flythrough
            if (instruction.FirstInstruction)
                return startParameters.StartingTurnType;
            else
                return ACEEnums.TurnType.FlyThrough;
        }

        public MissionRoute.Types.Status GetAreaStatus(int instructionID)
        {
            var instruction = TreatmentInstructions.FirstOrDefault(i => i.ID == instructionID);
            if (instruction == null)
                return MissionRoute.Types.Status.NotStarted;

            return instruction.AreaStatus;
        }

        private void UpdateInstructionOrder()
        {
            int missionOrder = 1;
            int listPosition = 1;
            var lastInstructionID = TreatmentInstructions.LastOrDefault(i => i.Enabled && i.AreaStatus != MissionRoute.Types.Status.Finished)?.ID;
            var firstInstructionID = TreatmentInstructions.FirstOrDefault(i => i.Enabled && i.AreaStatus != MissionRoute.Types.Status.Finished)?.ID;

            ITreatmentInstruction previousFirstInstruction = null;
            ITreatmentInstruction newFirstInstruction = null;

            foreach (ITreatmentInstruction instruction in TreatmentInstructions)
            {
                // Tell the instruction what it's current placement in the list is
                if (instruction.Enabled)
                {
                    if (instruction.FirstInstruction)
                        previousFirstInstruction = instruction;

                    instruction.SetOrder(
                        missionOrder,
                        instruction.ID == firstInstructionID,
                        instruction.ID == lastInstructionID,
                        listPosition == 1,
                        listPosition == TreatmentInstructions.Count);
                    missionOrder++;

                    if (instruction.FirstInstruction)
                        newFirstInstruction = instruction;
                }
                else
                {
                    instruction.SetOrder(null, false, false, listPosition == 1, listPosition == TreatmentInstructions.Count);
                }
                listPosition++;
            }

            if (previousFirstInstruction == null && newFirstInstruction == null)
                return;

            if (previousFirstInstruction != newFirstInstruction)
            {
                startParameters.UpdateParameters(newFirstInstruction, LastPosition, false);
                // If the first instruction has changed, we need to notify that the former first instruction has changed parameters
                // This is because the first instruction has these parameters overriden by the StartParameters
                if (previousFirstInstruction != null)
                {
                    var updatedParams = new List<string>() { "AreaEntryExitCoordinates", "StartingTurnType" };
                    InstructionSyncedPropertyUpdated?.Invoke(this, new InstructionSyncedPropertyUpdatedArgs(previousFirstInstruction.ID, updatedParams));
                }
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
                startParameters.UpdateParameters(GetNextInstruction(), LastPosition, false);

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
        }

        // Only considers enabled instructions that are not finished - this should always be the FirstInstruction if the instruction order is up to date
        public ITreatmentInstruction GetNextInstruction()
        {
            foreach (ITreatmentInstruction instruction in TreatmentInstructions.OrderBy(i => i.Order))
            {
                if (!instruction.Enabled)
                    continue;

                if (instruction.AreaStatus == MissionRoute.Types.Status.NotStarted || instruction.AreaStatus == MissionRoute.Types.Status.InProgress)
                    return instruction;
            }

            return null;
        }

        public ITreatmentInstruction GetLastInstruction()
        {
            foreach (ITreatmentInstruction instruction in TreatmentInstructions.OrderByDescending(i => i.Order))
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
                newInstruction.PropertyChanged += Instruction_PropertyChanged;
                newInstruction.SyncedPropertyChanged += Instruction_SyncedPropertyChanged;
                updatedInstructions.Instructions.Add(newInstruction);
                TreatmentInstructions.Add(newInstruction);
            }

            UpdateInstructionOrder();

            if (updatedInstructions.Instructions.Count > 0)
                InstructionAreasUpdated?.Invoke(this, updatedInstructions);

            updatingInstructions = false;
        }

        private void Instruction_SyncedPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var propertyList = new List<string>() { e.PropertyName };
            InstructionSyncedPropertyUpdated?.Invoke(this, new InstructionSyncedPropertyUpdatedArgs((sender as ITreatmentInstruction).ID, propertyList ));
        }

        private void Instruction_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // If we allow this while updating instructions then it will update the start parameters for each instruction added / modified(potentially)
            // When instructions are updated it will update the parameters just once at the end
            if (updatingInstructions)
                return;

            var instruction = sender as ITreatmentInstruction;

            switch (e.PropertyName)
            {
                case "Enabled":
                    UpdateInstructionOrder();
                    UpdateCanUpload();
                    break;
                case "TreatmentRoute":
                    if (instruction.FirstInstruction)
                        startParameters.UpdateParameters(instruction, LastPosition, false);
                    InstructionRouteUpdated?.Invoke(this, new InstructionRouteUpdatedArgs { Instruction = instruction });
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
