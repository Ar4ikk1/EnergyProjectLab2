using Energy_Project.Models;
using Energy_Project.Services;
using Energy_Project.Services.Interfaces;
using Moq;
 
namespace SmartHomeTests
{
    /// <summary>
    /// Юніт-тести для <see cref="DeviceService"/>.
    /// </summary>
    public class DeviceServiceTests
    {
        private static DeviceService CreateService(out Mock<IDeviceRepository> mockRepo)
        {
            mockRepo = new Mock<IDeviceRepository>();
            return new DeviceService(mockRepo.Object);
        }
 
        private static Device CreateDevice(int id, bool isOn, string name = "Lamp", double watts = 100)
        {
            return new Device { Id = id, Name = name, PowerUsageWatts = watts, IsOn = isOn };
        }

 
        /// <summary>
        /// Assert.True: увімкнення вимкненого пристрою повертає true.
        /// </summary>
        [Fact]
        public void ToggleDevice_TurnOnDevice_ReturnsTrue()
        {
            //Arrange
            var service = CreateService(out var mockRepo);
            mockRepo.Setup(r => r.GetById(1)).Returns(CreateDevice(1, false));
 
            //Act
            var result = service.ToggleDevice(1, true);
 
            //Assert
            Assert.True(result);
        }
 
        /// <summary>
        /// Assert.False: вимкнення увімкненого пристрою повертає false.
        /// </summary>
        [Fact]
        public void ToggleDevice_TurnOffDevice_ReturnsFalse()
        {
            //Arrange
            var service = CreateService(out var mockRepo);
            mockRepo.Setup(r => r.GetById(1)).Returns(CreateDevice(1, true));
 
            //Act
            var result = service.ToggleDevice(1, false);
 
            //Assert
            Assert.False(result);
        }
 
        /// <summary>
        /// Theory / InlineData + Assert.Equal: результат завжди дорівнює запитаному стану
        /// незалежно від початкового стану пристрою.
        /// </summary>
        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void ToggleDevice_AnyInitialState_ReturnsRequestedState(bool initialState, bool turnOn)
        {
            //Arrange
            var service = CreateService(out var mockRepo);
            mockRepo.Setup(r => r.GetById(5)).Returns(CreateDevice(5, initialState));
 
            //Act
            var result = service.ToggleDevice(5, turnOn);
 
            //Assert
            Assert.Equal(turnOn, result);
        }
 
        /// <summary>
        /// Assert.NotEqual: після зміни стану результат відрізняється від початкового.
        /// </summary>
        [Fact]
        public void ToggleDevice_ChangeState_ResultDiffersFromInitialState()
        {
            //Arrange
            const bool INITIAL_STATE = false;
            var service = CreateService(out var mockRepo);
            mockRepo.Setup(r => r.GetById(2)).Returns(CreateDevice(2, INITIAL_STATE));
 
            //Act
            var result = service.ToggleDevice(2, true);
 
            //Assert
            Assert.NotEqual(INITIAL_STATE, result);
        }
 
        /// <summary>
        /// Assert.Throws: якщо пристрій не знайдено, кидається <see cref="ArgumentException"/>.
        /// </summary>
        [Fact]
        public void ToggleDevice_UnknownDevice_ThrowsArgumentException()
        {
            //Arrange
            const string EXPECTED_MESSAGE = "Device not found";
            var service = CreateService(out var mockRepo);
            mockRepo.Setup(r => r.GetById(It.IsAny<int>())).Returns((Device?)null);
 
            //Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => service.ToggleDevice(999, true));
            Assert.Equal(EXPECTED_MESSAGE, ex.Message);
        }
 
        /// <summary>
        /// Verify(..., Times.Never) + It.IsAny: якщо пристрій не знайдено,
        /// Update у репозиторії не викликається.
        /// </summary>
        [Fact]
        public void ToggleDevice_UnknownDevice_NeverCallsUpdate()
        {
            //Arrange
            var service = CreateService(out var mockRepo);
            mockRepo.Setup(r => r.GetById(It.IsAny<int>())).Returns((Device?)null);
 
            //Act
            Assert.Throws<ArgumentException>(() => service.ToggleDevice(999, true));
 
            //Assert
            mockRepo.Verify(r => r.Update(It.IsAny<Device>()), Times.Never);
        }
 
        /// <summary>
        /// Verify() + Times.Once: для існуючого пристрою Update викликається один раз.
        /// </summary>
        [Fact]
        public void ToggleDevice_ExistingDevice_CallsRepositoryUpdateOnce()
        {
            //Arrange
            var service = CreateService(out var mockRepo);
            var device = CreateDevice(1, false);
            mockRepo.Setup(r => r.GetById(1)).Returns(device);
 
            //Act
            service.ToggleDevice(1, true);
 
            //Assert
            mockRepo.Verify(r => r.Update(device), Times.Once);
        }
 
        /// <summary>
        /// It.Is(predicate): в Update передається пристрій з Id = 3 та оновленим станом IsOn = true.
        /// </summary>
        [Fact]
        public void ToggleDevice_ExistingDevice_UpdatesDeviceWithNewState()
        {
            //Arrange
            var service = CreateService(out var mockRepo);
            mockRepo.Setup(r => r.GetById(3)).Returns(CreateDevice(3, false));
 
            //Act
            service.ToggleDevice(3, true);
 
            //Assert
            mockRepo.Verify(r => r.Update(It.Is<Device>(d => d.Id == 3 && d.IsOn)), Times.Once);
        }
 
        /// <summary>
        /// Verify(..., Times.Exactly(n)): два перемикання дають рівно два виклики Update.
        /// </summary>
        [Fact]
        public void ToggleDevice_CalledTwice_CallsRepositoryUpdateExactlyTwice()
        {
            //Arrange
            var service = CreateService(out var mockRepo);
            mockRepo.Setup(r => r.GetById(1)).Returns(CreateDevice(1, false));
 
            //Act
            service.ToggleDevice(1, true);
            service.ToggleDevice(1, false);
 
            //Assert
            mockRepo.Verify(r => r.Update(It.IsAny<Device>()), Times.Exactly(2));
        }
 
        /// <summary>
        /// Verify(..., Times.AtMost(n)): за одне перемикання Update викликається не більше одного разу.
        /// </summary>
        [Fact]
        public void ToggleDevice_ExistingDevice_CallsUpdateAtMostOnce()
        {
            //Arrange
            var service = CreateService(out var mockRepo);
            mockRepo.Setup(r => r.GetById(1)).Returns(CreateDevice(1, true));
 
            //Act
            service.ToggleDevice(1, false);
 
            //Assert
            mockRepo.Verify(r => r.Update(It.IsAny<Device>()), Times.AtMost(1));
        }
        
 
        /// <summary>
        /// Assert.Contains / Assert.DoesNotContain: у результаті є лише увімкнені пристрої.
        /// </summary>
        [Fact]
        public void GetActiveDevices_MixedDevices_ReturnsOnlyActiveOnes()
        {
            //Arrange
            var service = CreateService(out var mockRepo);
            var lamp = CreateDevice(1, true, "Lamp");
            var tv = CreateDevice(2, false, "TV");
            var fridge = CreateDevice(3, true, "Fridge");
            mockRepo.Setup(r => r.GetAll()).Returns(new List<Device> { lamp, tv, fridge });
 
            //Act
            var result = service.GetActiveDevices().ToList();
 
            //Assert
            Assert.Contains(lamp, result);
            Assert.Contains(fridge, result);
            Assert.DoesNotContain(tv, result);
            Assert.Equal(2, result.Count);
        }
 
        /// <summary>
        /// Assert.Empty: якщо всі пристрої вимкнені, список активних порожній.
        /// </summary>
        [Fact]
        public void GetActiveDevices_AllDevicesOff_ReturnsEmptyCollection()
        {
            //Arrange
            var service = CreateService(out var mockRepo);
            mockRepo.Setup(r => r.GetAll()).Returns(new List<Device>
            {
                CreateDevice(1, false),
                CreateDevice(2, false)
            });
 
            //Act
            var result = service.GetActiveDevices();
 
            //Assert
            Assert.Empty(result);
        }
 
        /// <summary>
        /// Assert.Empty: якщо репозиторій порожній, список активних також порожній.
        /// </summary>
        [Fact]
        public void GetActiveDevices_RepositoryEmpty_ReturnsEmptyCollection()
        {
            //Arrange
            var service = CreateService(out var mockRepo);
            mockRepo.Setup(r => r.GetAll()).Returns(new List<Device>());
 
            //Act
            var result = service.GetActiveDevices();
 
            //Assert
            Assert.Empty(result);
        }
 
        /// <summary>
        /// Assert.NotNull / Assert.NotEmpty: якщо є увімкнений пристрій, результат не null і не порожній.
        /// </summary>
        [Fact]
        public void GetActiveDevices_OneDeviceOn_ReturnsNotEmptyCollection()
        {
            //Arrange
            var service = CreateService(out var mockRepo);
            mockRepo.Setup(r => r.GetAll()).Returns(new List<Device> { CreateDevice(1, true) });
 
            //Act
            var result = service.GetActiveDevices();
 
            //Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }
 
        /// <summary>
        /// Verify(..., Times.AtLeastOnce): для отримання активних пристроїв
        /// репозиторій опитується хоча б один раз.
        /// </summary>
        [Fact]
        public void GetActiveDevices_Always_CallsRepositoryGetAllAtLeastOnce()
        {
            //Arrange
            var service = CreateService(out var mockRepo);
            mockRepo.Setup(r => r.GetAll()).Returns(new List<Device>());
 
            //Act
            service.GetActiveDevices().ToList();
 
            //Assert
            mockRepo.Verify(r => r.GetAll(), Times.AtLeastOnce);
        }
 
        /// <summary>
        /// Verify(..., Times.Never): отримання активних пристроїв нічого не змінює в репозиторії.
        /// </summary>
        [Fact]
        public void GetActiveDevices_Always_NeverCallsUpdate()
        {
            //Arrange
            var service = CreateService(out var mockRepo);
            mockRepo.Setup(r => r.GetAll()).Returns(new List<Device> { CreateDevice(1, true) });
 
            //Act
            service.GetActiveDevices().ToList();
 
            //Assert
            mockRepo.Verify(r => r.Update(It.IsAny<Device>()), Times.Never);
        }
    }
}