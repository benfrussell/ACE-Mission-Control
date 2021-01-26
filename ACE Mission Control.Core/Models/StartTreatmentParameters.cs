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

        public StartTreatmentParameters()
        {
            SelectedMode = Modes.FirstEntry;
        }

        public void UpdateAvailableModes(TreatmentInstruction nextInstruction, bool missionHasProgress)
        {
            FirstEntryModeAvailable = nextInstruction == null || nextInstruction.HasValidTreatmentRoute();
            SelectedWaypointModeAvailable = nextInstruction == null || nextInstruction.HasValidTreatmentRoute();
            LastPositionWaypointModeAvailable = missionHasProgress;
            LastPositionContinuedModeAvailable = missionHasProgress;
        }
    }
}
