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
        [Fact]
        public void ResetProgress_WithMissionProgress_ClearsInstructionAreaStatus()
        {
            // Arrange

            // Set up mission with mock treatment instruction
            var sut = new Mission();
            sut.Unlock();

            var mockInstruction = new Mock<ITreatmentInstruction>();
            mockInstruction.SetupProperty(i => i.AreaStatus);
            mockInstruction.Object.AreaStatus = MissionRoute.Types.Status.InProgress;
            sut.TreatmentInstructions.Add(mockInstruction.Object);

            // Act
            sut.ResetProgress();

            // Assert
            Assert.Equal(MissionRoute.Types.Status.NotStarted, sut.TreatmentInstructions.First().AreaStatus);
        }
    }
}
