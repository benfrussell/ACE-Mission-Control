using System;
using Xunit;
using Moq;
using ACE_Mission_Control.Core.Models;

namespace ACE_Mission_Control_Tests
{
    public class MissionTests
    {
        [Fact]
        public void MissionCanBeResetAfterConfigUpdate()
        {
            // Arrange
            var mockDrone = new Mock<IDrone>();
            mockDrone.SetupGet(d => d.InterfaceState).Returns(Pbdrone.InterfaceStatus.Types.State.Online);
            mockDrone.SetupGet(d => d.Synchronization).Returns(Drone.SyncState.Synchronized);

            var mockOBC = new Mock<IOnboardComputerClient>();
            mockOBC.SetupGet(d => d.IsDirectorConnected).Returns(true);

            Pbdrone.MissionStatus status = new Pbdrone.MissionStatus();
            status.Activated = false;
            status.InProgress = true;

            Pbdrone.MissionConfig config = new Pbdrone.MissionConfig();
            config.Areas.Add(0);

            var sut = new Mission(mockDrone.Object, mockOBC.Object);

            sut.UpdateMissionStatus(status);

            // Act
            sut.UpdateMissionConfig(config);

            // Assert
            Assert.True(sut.CanBeReset);

            // For CanBeReset to be TRUE
            // Mission MUST be modifiable
            // - Drone MUST report mission is NOT activated
            // - Drone MUST be sychronized
            // - Director MUST be connected
            // Mission MUST have progress OR
            // Drone MUST report having progress (Mission Status) AND report having area IDs (Mission Config)
        }
    }
}
