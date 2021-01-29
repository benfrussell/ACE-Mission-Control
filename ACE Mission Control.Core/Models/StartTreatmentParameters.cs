using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Text;

namespace ACE_Mission_Control.Core.Models
{
    public class StartTreatmentParameters
    {
        public enum Modes
        {
            FirstEntry = 0,
            SelectedWaypoint = 1,
            LastPositionWaypoint = 2,
            LastPositionContinued = 3
        }

        public event EventHandler<EventArgs> StartParametersChangedEvent;

        public Modes SelectedMode;

        public int SelectedModeInt { get => (int)SelectedMode; }

        private bool firstEntryModeAvailable;
        public bool FirstEntryModeAvailable { get => firstEntryModeAvailable; private set => firstEntryModeAvailable = value; }

        private bool selectedWaypointModeAvailable;
        public bool SelectedWaypointModeAvailable { get => selectedWaypointModeAvailable; private set => selectedWaypointModeAvailable = value; }

        private bool lastPositionWaypointModeAvailable;
        public bool LastPositionWaypointModeAvailable { get => lastPositionWaypointModeAvailable; private set => lastPositionWaypointModeAvailable = value; }

        private bool lastPositionContinuedModeAvailable;
        public bool LastPositionContinuedModeAvailable { get => lastPositionContinuedModeAvailable; private set => lastPositionContinuedModeAvailable = value; }

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

        public StartTreatmentParameters()
        {
            SelectedMode = Modes.FirstEntry;
            StopAndTurn = false;
        }

        public void UpdateAvailableModes(TreatmentInstruction nextInstruction, bool missionHasProgress)
        {
            FirstEntryModeAvailable = nextInstruction == null || nextInstruction.HasValidTreatmentRoute();
            SelectedWaypointModeAvailable = nextInstruction == null || nextInstruction.HasValidTreatmentRoute();
            LastPositionWaypointModeAvailable = missionHasProgress;
            LastPositionContinuedModeAvailable = missionHasProgress;
        }

        public void SetStartParameters(Coordinate coordinate, bool stopAndTurnMode)
        {
            bool anyChanges = StartCoordinate == null || coordinate.X != StartCoordinate.X || coordinate.Y != StartCoordinate.Y || stopAndTurnMode != StopAndTurn;
            StartCoordinate = coordinate;
            StopAndTurn = stopAndTurn;

            if (anyChanges)
                StartParametersChangedEvent?.Invoke(this, new EventArgs());
        }
    }
}
