using NetTopologySuite.Geometries;
using Pbdrone;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ACE_Mission_Control.Core.Models
{
    public interface IMission : INotifyPropertyChanged
    {
        bool Activated { get; }
        bool CanBeModified { get; }
        bool CanBeReset { get; }
        bool CanToggleActivation { get; }
        bool CanUpload { get; }
        Coordinate LastPosition { get; }
        bool Locked { get; }
        bool MissionHasProgress { get; }
        bool MissionSet { get; }
        MissionStatus.Types.Stage Stage { get; }
        StartTreatmentParameters.Mode StartMode { get; set; }
        int TreatmentDuration { get; set; }
        ObservableCollection<ITreatmentInstruction> TreatmentInstructions { get; set; }

        event EventHandler<InstructionAreasUpdatedArgs> InstructionAreasUpdated;
        event EventHandler<InstructionRouteUpdatedArgs> InstructionRouteUpdated;
        event EventHandler<EventArgs> ProgressReset;
        event EventHandler<InstructionSyncedPropertyUpdatedArgs> InstructionSyncedPropertyUpdated;

        ITreatmentInstruction GetNextInstruction();
        List<ITreatmentInstruction> GetRemainingInstructions();
        Coordinate GetStartCoordinate(int instructionID);
        string GetStartCoordinateString(int instructionID);
        Coordinate GetStopCoordinate(int instructionID);
        string GetStopCoordinateString(int instructionID);
        long GetLastPropertyModificationTime(int instructionID);
        long GetLastAreaModificationTime(int instructionID);
        ITreatmentInstruction GetInstructionByID(int instructionID);
        ACEEnums.TurnType GetStartingTurnType(int instructionID);
        MissionRoute.Types.Status GetAreaStatus(int instructionID);
        void Lock();
        void SetInstructionAreaStatus(int instructionID, MissionRoute.Types.Status status);
        void ReorderInstructionsByID(List<int> orderedIDs);
        void ReorderInstruction(ITreatmentInstruction instruction, int newPosition);
        void ResetProgress();
        void ResetStatus();
        void SetInstructionUploaded(int id);
        void SetSelectedStartWaypoint(string waypointID);
        void Unlock();
        void UpdateMissionStatus(MissionStatus newStatus);
    }
}