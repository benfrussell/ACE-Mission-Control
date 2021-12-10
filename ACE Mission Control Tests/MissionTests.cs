using System;
using Xunit;
using Moq;
using ACE_Mission_Control.Core.Models;
using Pbdrone;
using System.Linq;

namespace ACE_Mission_Control_Tests
{
    public class MissionTests
    {
        [Theory]
        // Drone status activated tests
        [InlineData(false, 0, AreaResult.Types.Status.NotStarted, 0, AreaResult.Types.Status.InProgress, true)]
        [InlineData(true, 0, AreaResult.Types.Status.NotStarted, 0, AreaResult.Types.Status.InProgress, false)]
        // Drone matching instruction status test
        [InlineData(false, 0, AreaResult.Types.Status.NotStarted, 0, AreaResult.Types.Status.Finished, true)]
        // Drone mismatching instruction status tests
        [InlineData(false, 0, AreaResult.Types.Status.NotStarted, 1, AreaResult.Types.Status.NotStarted, false)]
        [InlineData(false, 0, AreaResult.Types.Status.NotStarted, 1, AreaResult.Types.Status.InProgress, false)]
        [InlineData(false, 0, AreaResult.Types.Status.InProgress, 1, AreaResult.Types.Status.NotStarted, true)]
        [InlineData(false, 0, AreaResult.Types.Status.Finished, 1, AreaResult.Types.Status.NotStarted, true)]
        public void MissionCanBeReset_AfterStatusUpdate(
            bool droneActivated, 
            int mcInstructionID,  
            AreaResult.Types.Status mcInstructionStatus,
            int droneInstructionID,
            AreaResult.Types.Status droneInstructionStatus,
            bool expected)
        {
            // Arrange

            // Set up an unlocked mission with mock treatment instruction
            var sut = new Mission();
            sut.Unlock();

            var mockInstruction = new Mock<ITreatmentInstruction>();
            mockInstruction.SetupGet(i => i.ID).Returns(mcInstructionID);
            mockInstruction.SetupProperty(i => i.AreaStatus);
            mockInstruction.Object.AreaStatus = mcInstructionStatus;
            sut.TreatmentInstructions.Add(mockInstruction.Object);

            // Set up status update
            MissionStatus status = new MissionStatus();
            status.Activated = droneActivated;
            AreaResult result = new AreaResult();
            result.AreaID = droneInstructionID;
            result.Status = droneInstructionStatus;
            status.Results.Add(result);

            // Act
            sut.UpdateMissionStatus(status);

            // Assert
            Assert.Equal(expected, sut.CanBeReset);

            // For CanBeReset to be TRUE
            // Mission MUST be unlocked
            // Mission MUST be modifiable
            // - Drone MUST report mission is NOT activated (Mission Status)
            // Mission MUST have treatments in progress (Mission Status)
        }

        [Fact]
        public void ResetProgress_WithMissionProgress_ClearsInstructionAreaStatus()
        {
            // Arrange

            // Set up mission with mock treatment instruction
            var sut = new Mission();
            sut.Unlock();

            var mockInstruction = new Mock<ITreatmentInstruction>();
            mockInstruction.SetupProperty(i => i.AreaStatus);
            mockInstruction.Object.AreaStatus = AreaResult.Types.Status.InProgress;
            sut.TreatmentInstructions.Add(mockInstruction.Object);

            // Act
            sut.ResetProgress();

            // Assert
            Assert.Equal(AreaResult.Types.Status.NotStarted, sut.TreatmentInstructions.First().AreaStatus);
        }
    }
}
