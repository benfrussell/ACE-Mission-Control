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

    public class Mission : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler<InstructionsUpdatedEventArgs> InstructionUpdated;

        public StartTreatmentParameters StartParameters;

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

        private Drone drone;
        private OnboardComputerClient onboardComputer;

        public ObservableCollection<TreatmentInstruction> TreatmentInstructions;

        public Mission(Drone _drone, OnboardComputerClient _onboardComputer)
        {
            MissionRetriever.AreaScanPolygonsUpdated += MissionData_AreaScanPolygonsUpdated;
            TreatmentInstructions = new ObservableCollection<TreatmentInstruction>();
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
            StartParameters.UpdateAvailableModes(null, HasProgress);
        }

        public void UpdateMissionStatus(MissionStatus newStatus)
        {
            Stage = newStatus.MissionStage;

            Activated = newStatus.Activated;
            UpdateCanBeModified();

            HasProgress = newStatus.InProgress;
            UpdateCanBeReset();

            LastPosition = new Coordinate(
                (newStatus.LastLongitude / 180) * Math.PI,
                (newStatus.LastLatitude / 180) * Math.PI);

            // Update the treatment instruction statuses with any results that came from the mission status update
            foreach (TreatmentInstruction instruction in TreatmentInstructions)
            {
                var resultInStatus = newStatus.Results.FirstOrDefault(r => r.AreaID == instruction.TreatmentPolygon.Id);
                if (resultInStatus != null)
                    instruction.AreaStatus = resultInStatus.Status;
            }

            StartParameters.UpdateAvailableModes(GetNextInstruction(), HasProgress);
        }

        public void UpdateMissionConfig(MissionConfig newConfig)
        {
            FlyThroughMode = newConfig.FlyThroughMode;
            TreatmentDuration = newConfig.TreatmentDuration;
            AvailablePayloads = newConfig.AvailablePayloads.ToList();
            SelectedPayload = newConfig.SelectedPayload;
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
                TreatmentInstructions.Add(new TreatmentInstruction(addedArea));

            if (updatedInstructions.Instructions.Count > 0)
            {
                StartParameters.UpdateAvailableModes(GetNextInstruction(), HasProgress);
                InstructionUpdated?.Invoke(this, updatedInstructions);
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
