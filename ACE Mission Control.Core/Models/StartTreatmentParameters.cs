using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Text;
using Pbdrone;
using System.Linq;

namespace ACE_Mission_Control.Core.Models
{
    public class StartParametersChangedArgs
    {
        public List<string> ParameterNames { get; set; }
    }

    public class StartTreatmentParameters
    {
        public enum Mode
        {
            FirstEntry = 0,
            SelectedWaypoint = 1,
            InsertWaypoint = 2,
            ContinueMission = 3,
            Flythrough = 4,
            NotUsed = 5
        }

        public event EventHandler<EventArgs> SelectedModeChangedEvent;

        public event EventHandler<StartParametersChangedArgs> StartParametersChanged;

        private Mode selectedMode;
        public Mode SelectedMode
        {
            get => selectedMode;
            set
            {
                if (value == selectedMode)
                    return;
                selectedMode = value;
                SelectedModeChangedEvent?.Invoke(this, new EventArgs());
            }
        }

        private Mode defaultNoProgressMode;
        public Mode DefaultNoProgressMode { get => defaultNoProgressMode; set => defaultNoProgressMode = value; }

        private Mode defaultProgressMode;
        public Mode DefaultProgressMode { get => defaultProgressMode; set => defaultProgressMode = value; }

        private Coordinate startCoordinate;
        public Coordinate StartCoordinate
        {
            get => startCoordinate;
            protected set
            {
                startCoordinate = value;
            }
        }

        private Waypoint.TurnType startingTurnType;
        public Waypoint.TurnType StartingTurnType
        {
            get => startingTurnType;
            protected set
            {                
                startingTurnType = value;
            }
        }

        private long lastStartPropertyModification;
        public long LastStartPropertyModification
        {
            get => lastStartPropertyModification;
            protected set
            {
                if (lastStartPropertyModification == value)
                    return;
                lastStartPropertyModification = value;
            }
        }

        // Only store the ID of the coordinate because we should search and confirm it still exists everytime we need to use it
        private string BoundWaypointID;
        private int? BoundRouteID;

        public StartTreatmentParameters()
        {
            DefaultNoProgressMode = Mode.FirstEntry;
            DefaultProgressMode = Mode.Flythrough;

            SelectedMode = DefaultNoProgressMode;
            StartingTurnType = Waypoint.TurnType.FlyThrough;

            LastStartPropertyModification = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            MissionRetriever.StaticPropertyChanged += MissionRetriever_StaticPropertyChanged;
        }

        // If we have a bound start waypoint we need to be checking to see if there have been changes to it
        // Specifically we need to watch the turn type
        private void MissionRetriever_StaticPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (BoundRouteID == null || BoundWaypointID == null || SelectedMode != Mode.SelectedWaypoint)
                return;

            if (e.PropertyName == "WaypointRoutes")
            {
                var boundRoute = MissionRetriever.WaypointRoutes.FirstOrDefault(r => r.Id == BoundRouteID);
                if (boundRoute != null)
                {
                    var boundWaypoint = boundRoute.Waypoints.FirstOrDefault(w => w.ID == BoundWaypointID);

                    if (boundWaypoint != null)
                        System.Diagnostics.Debug.WriteLine($"Bound wp turn changed to {boundWaypoint.Turn}");

                    if (boundWaypoint != null && boundWaypoint.Turn != StartingTurnType)
                    {
                        StartingTurnType = boundWaypoint.Turn;
                        LastStartPropertyModification = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                        var changedParametersList = new List<string> { "StartingTurnType" };
                        StartParametersChanged?.Invoke(this, new StartParametersChangedArgs { ParameterNames = changedParametersList });
                    }
                }
            }
        }

        // Returns True for any changes, False for no changes
        public bool UpdateParameters(ITreatmentInstruction nextInstruction, Coordinate lastPosition, bool justReturned)
        {
            bool inProgress = nextInstruction != null && lastPosition != null && nextInstruction.AreaStatus == MissionRoute.Types.Status.InProgress;

            UpdateMode(inProgress, justReturned);

            if (nextInstruction == null)
                return false;

            var originalStart = StartCoordinate?.Copy();
            var originalTurnMode = StartingTurnType;

            // Update the parameters based on mode

            switch (SelectedMode)
            {
                case Mode.FirstEntry:
                    StartCoordinate = nextInstruction.AreaEntryExitCoordinates.Item1;
                    StartingTurnType = Waypoint.TurnType.FlyThrough;
                    break;
                case Mode.SelectedWaypoint:
                    SetStartCoordToBoundWaypoint(nextInstruction);
                    break;
                case Mode.InsertWaypoint:
                    if (BoundWaypointID == null)
                    {
                        CreateWaypointAndSetStartCoord(nextInstruction, lastPosition);
                    }
                    else
                    {
                        var boundWaypoint = nextInstruction.TreatmentRoute.Waypoints.FirstOrDefault(p => p.ID == BoundWaypointID);
                        if (boundWaypoint == null)
                        {
                            CreateWaypointAndSetStartCoord(nextInstruction, lastPosition);
                        }
                        else
                        {
                            if (WaypointRoute.IsCoordinateInArea(boundWaypoint, lastPosition, nextInstruction.Swath))
                            {
                                StartCoordinate = boundWaypoint.Coordinate;
                                StartingTurnType = boundWaypoint.Turn;
                            } 
                            else
                            {
                                CreateWaypointAndSetStartCoord(nextInstruction, lastPosition);
                            }
                        }
                    }
                    break;
                case Mode.ContinueMission:
                    StartCoordinate = lastPosition;
                    StartingTurnType = Waypoint.TurnType.StopAndTurn;
                    break;
                case Mode.Flythrough:
                    if (nextInstruction.TreatmentPolygon.IntersectsCoordinate(lastPosition))
                    {
                        StartCoordinate = lastPosition;
                    }
                    else
                    {
                        StartCoordinate = nextInstruction.TreatmentRoute.CalcIntersectAfterCoordinate(lastPosition, 7.5f, nextInstruction.TreatmentPolygon);
                        if (StartCoordinate == null)
                            StartCoordinate = nextInstruction.AreaEntryExitCoordinates.Item1;
                    }
                    StartingTurnType = Waypoint.TurnType.FlyThrough;
                    break;
            }

            bool anyChanges =
                StartCoordinate == null ||
                (originalStart == null || originalStart.X != StartCoordinate.X || originalStart.Y != StartCoordinate.Y) ||
                originalTurnMode != StartingTurnType;

            if (anyChanges)
            {
                LastStartPropertyModification = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                // Just say all start parameters changed - no harm caused
                var changedParametersList = new List<string> { "AreaEntryExitCoordinates", "StartingTurnType" };
                StartParametersChanged?.Invoke(this, new StartParametersChangedArgs { ParameterNames = changedParametersList });
            }
                

            return anyChanges;
        }

        public void SetSelectedWaypoint(string waypointID, ITreatmentInstruction nextInstruction)
        {
            if (waypointID == BoundWaypointID)
                return;

            BoundWaypointID = waypointID;
            BoundRouteID = nextInstruction.TreatmentRoute.Id;
            if (SelectedMode == Mode.SelectedWaypoint)
                UpdateParameters(nextInstruction, null, false);
        }

        private bool DoesModeRequireProgress(Mode mode)
        {
            return mode == Mode.InsertWaypoint || mode == Mode.ContinueMission || mode == Mode.Flythrough;
        }

        // Applies any valid default and ensures the current mode is valid
        private void UpdateMode(bool inProgress, bool justReturned)
        {
            if (inProgress)
            {
                if (justReturned && DefaultProgressMode != Mode.NotUsed)
                    SelectedMode = DefaultProgressMode;
            }
            else
            {
                if (DoesModeRequireProgress(SelectedMode))
                {
                    if (DefaultNoProgressMode != Mode.NotUsed)
                        SelectedMode = DefaultNoProgressMode;
                    else
                        SelectedMode = Mode.FirstEntry;
                }
            }
        }

        private async void CreateWaypointAndSetStartCoord(ITreatmentInstruction instruction, Coordinate position)
        {
            var waypointPair = instruction.TreatmentRoute.FindWaypointPairAroundCoordinate(position, instruction.Swath);
            if (waypointPair == null)
                return;
            var newWaypoint = await UGCSClient.InsertWaypointAlongRoute(instruction.TreatmentRoute.Id, waypointPair.Item1.ID, position.X, position.Y);

            BoundWaypointID = newWaypoint.ID;
            BoundRouteID = instruction.TreatmentRoute.Id;

            StartCoordinate = newWaypoint.Coordinate; 
            StartingTurnType = newWaypoint.Turn;
        }

        private void SetStartCoordToBoundWaypoint(ITreatmentInstruction instruction)
        {
            var waypoints = instruction.TreatmentRoute.Waypoints;
            var boundCoord = waypoints.FirstOrDefault(p => p.ID == BoundWaypointID);

            if (boundCoord != null)
            {
                StartCoordinate = boundCoord.Coordinate;
                StartingTurnType = waypoints.First().Turn;
            }
            else
            {
                BoundRouteID = instruction.TreatmentRoute.Id;
                BoundWaypointID = waypoints.First().ID;
                StartCoordinate = waypoints.First().Coordinate;
                StartingTurnType = waypoints.First().Turn;
            }
        }
    }
}
