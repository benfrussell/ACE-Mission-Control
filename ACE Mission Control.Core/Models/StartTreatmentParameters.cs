using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Text;
using Pbdrone;
using System.Linq;

namespace ACE_Mission_Control.Core.Models
{
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

        private bool stopAndTurn;
        public bool StopAndTurn
        {
            get => stopAndTurn;
            protected set
            {
                stopAndTurn = value;
            }
        }

        // Only store the ID of the coordinate because we should search and confirm it still exists everytime we need to use it
        public string BoundStartWaypointID;

        public StartTreatmentParameters()
        {
            DefaultNoProgressMode = Mode.FirstEntry;
            DefaultProgressMode = Mode.Flythrough;

            SelectedMode = DefaultNoProgressMode;
            StopAndTurn = false;
        }

        // Returns True for any changes, False for no changes
        public bool UpdateParameters(TreatmentInstruction nextInstruction, Coordinate lastPosition, bool justReturned)
        {
            bool inProgress = nextInstruction != null && lastPosition != null && nextInstruction.AreaStatus == AreaResult.Types.Status.InProgress;

            UpdateMode(inProgress, justReturned);

            if (nextInstruction == null)
                return false;

            var originalStart = StartCoordinate?.Copy();
            var originalTurnMode = StopAndTurn;

            // Update the parameters based on mode

            switch (SelectedMode)
            {
                case Mode.FirstEntry:
                    StartCoordinate = nextInstruction.AreaEntryCoordinate;
                    StopAndTurn = false;
                    break;
                case Mode.SelectedWaypoint:
                    SetStartCoordToBoundWaypoint(nextInstruction);
                    break;
                case Mode.InsertWaypoint:
                    if (BoundStartWaypointID == null)
                    {
                        CreateWaypointAndSetStartCoord(nextInstruction, lastPosition);
                    }
                    else
                    {
                        var boundWaypoint = nextInstruction.TreatmentRoute.Waypoints.FirstOrDefault(p => p.ID == BoundStartWaypointID);
                        if (boundWaypoint == null)
                        {
                            CreateWaypointAndSetStartCoord(nextInstruction, lastPosition);
                        }
                        else
                        {
                            if (WaypointRoute.IsCoordinateInArea(boundWaypoint, lastPosition, nextInstruction.Swath))
                            {
                                StartCoordinate = boundWaypoint.Coordinate;
                                StopAndTurn = boundWaypoint.TurnType == "STOP_AND_TURN";
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
                    StopAndTurn = true;
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
                            StartCoordinate = nextInstruction.AreaEntryCoordinate;
                    }
                    StopAndTurn = false;
                    break;
            }

            bool anyChanges =
                StartCoordinate == null ||
                (originalStart == null || originalStart.X != StartCoordinate.X || originalStart.Y != StartCoordinate.Y) ||
                originalTurnMode != StopAndTurn;

            return anyChanges;
        }

        // Returns True for any changes, False for no changes
        public void SetSelectedWaypoint(string waypointID, TreatmentInstruction nextInstruction)
        {
            BoundStartWaypointID = waypointID;
            SetStartCoordToBoundWaypoint(nextInstruction);
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

        private async void CreateWaypointAndSetStartCoord(TreatmentInstruction instruction, Coordinate position)
        {
            var waypointPair = instruction.TreatmentRoute.FindWaypointPairAroundCoordinate(position, instruction.Swath);
            if (waypointPair == null)
                return;
            var newWaypoint = await UGCSClient.InsertWaypointAlongRoute(instruction.TreatmentRoute.Id, waypointPair.Item1.ID, position.X, position.Y);

            BoundStartWaypointID = newWaypoint.ID;

            StartCoordinate = newWaypoint.Coordinate; 
            StopAndTurn = newWaypoint.TurnType == "STOP_AND_TURN";
        }

        private void SetStartCoordToBoundWaypoint(TreatmentInstruction instruction)
        {
            var waypoints = instruction.TreatmentRoute.Waypoints;
            var boundCoord = waypoints.FirstOrDefault(p => p.ID == BoundStartWaypointID);

            if (boundCoord != null)
            {
                StartCoordinate = boundCoord.Coordinate;
                StopAndTurn = boundCoord.TurnType == "STOP_AND_TURN";
            }
            else
            {
                BoundStartWaypointID = waypoints.First().ID;
                StartCoordinate = waypoints.First().Coordinate;
                StopAndTurn = waypoints.First().TurnType == "STOP_AND_TURN";
            }
        }
    }
}
