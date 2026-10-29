using Energy_Project.Models;
using Energy_Project.Services;
using Energy_Project.Services.Interfaces;
using Moq;
 
namespace SmartHomeTests
{
    /// <summary>
    /// Юніт-тести для <see cref="EnergyMonitorService"/>.
    /// </summary>
    public class EnergyMonitorServiceTests
    {
        private static EnergyMonitorService CreateService(
            out Mock<IDeviceRepository> mockDevices,
            out Mock<IEnergyPlanRepository> mockPlans,
            out Mock<INotificationService> mockNotify)
        {
            mockDevices = new Mock<IDeviceRepository>();
            mockPlans = new Mock<IEnergyPlanRepository>();
            mockNotify = new Mock<INotificationService>();
            return new EnergyMonitorService(mockDevices.Object, mockPlans.Object, mockNotify.Object);
        }
 
        private static Device CreateDevice(int id, bool isOn, double watts)
        {
            return new Device { Id = id, Name = $"Device{id}", PowerUsageWatts = watts, IsOn = isOn };
        }
 
        private static EnergyPlan CreatePlan(double limitKwh, DateTime? lastReset = null)
        {
            return new EnergyPlan { DailyLimitKwh = limitKwh, LastReset = lastReset ?? DateTime.Today };
        }
        
 
        /// <summary>
        /// Assert.Equal: сума увімкнених пристроїв (500 + 1500 Вт) = 2 кВт,
        /// вимкнений пристрій не враховується.
        /// </summary>
        [Fact]
        public void CalculateCurrentUsageKwh_MixedDevices_SumsOnlyActiveOnes()
        {
            //Arrange
            const double EXPECTED_KWH = 2.0;
            var service = CreateService(out var mockDevices, out _, out _);
            mockDevices.Setup(r => r.GetAll()).Returns(new List<Device>
            {
                CreateDevice(1, true, 500),
                CreateDevice(2, true, 1500),
                CreateDevice(3, false, 1000)
            });
 
            //Act
            var result = service.CalculateCurrentUsageKwh();
 
            //Assert
            Assert.Equal(EXPECTED_KWH, result, precision: 5);
        }
 
        /// <summary>
        /// Theory / InlineData + Assert.Equal: перерахунок Вт у кВт для різних значень.
        /// </summary>
        [Theory]
        [InlineData(0, 0.0)]
        [InlineData(100, 0.1)]
        [InlineData(500, 0.5)]
        [InlineData(1000, 1.0)]
        [InlineData(2500, 2.5)]
        public void CalculateCurrentUsageKwh_SingleDevice_ConvertsWattsToKilowatts(double watts, double expectedKwh)
        {
            //Arrange
            var service = CreateService(out var mockDevices, out _, out _);
            mockDevices.Setup(r => r.GetAll()).Returns(new List<Device> { CreateDevice(1, true, watts) });
 
            //Act
            var result = service.CalculateCurrentUsageKwh();
 
            //Assert
            Assert.Equal(expectedKwh, result, precision: 5);
        }
 
        /// <summary>
        /// Assert.Equal: якщо пристроїв немає, споживання дорівнює нулю.
        /// </summary>
        [Fact]
        public void CalculateCurrentUsageKwh_NoDevices_ReturnsZero()
        {
            //Arrange
            var service = CreateService(out var mockDevices, out _, out _);
            mockDevices.Setup(r => r.GetAll()).Returns(new List<Device>());
 
            //Act
            var result = service.CalculateCurrentUsageKwh();
 
            //Assert
            Assert.Equal(0.0, result);
        }
 
        /// <summary>
        /// Assert.NotEqual: вимкнені пристрої ігноруються, тому результат (1 кВт)
        /// не дорівнює сумі потужності всіх пристроїв (2 кВт).
        /// </summary>
        [Fact]
        public void CalculateCurrentUsageKwh_OffDevicesIgnored_ResultDiffersFromTotalOfAll()
        {
            //Arrange
            const double TOTAL_OF_ALL_KWH = 2.0;
            var service = CreateService(out var mockDevices, out _, out _);
            mockDevices.Setup(r => r.GetAll()).Returns(new List<Device>
            {
                CreateDevice(1, true, 1000),
                CreateDevice(2, false, 1000)
            });
 
            //Act
            var result = service.CalculateCurrentUsageKwh();
 
            //Assert
            Assert.NotEqual(TOTAL_OF_ALL_KWH, result);
        }
 
        /// <summary>
        /// Assert.True / Assert.False: за наявності увімкнених пристроїв споживання додатне,
        /// а за їх відсутності не є додатним.
        /// </summary>
        [Fact]
        public void CalculateCurrentUsageKwh_ActiveVsInactive_PositiveOnlyWhenSomethingIsOn()
        {
            //Arrange
            var serviceOn = CreateService(out var devicesOn, out _, out _);
            devicesOn.Setup(r => r.GetAll()).Returns(new List<Device> { CreateDevice(1, true, 300) });
 
            var serviceOff = CreateService(out var devicesOff, out _, out _);
            devicesOff.Setup(r => r.GetAll()).Returns(new List<Device> { CreateDevice(1, false, 300) });
 
            //Act
            var usageOn = serviceOn.CalculateCurrentUsageKwh();
            var usageOff = serviceOff.CalculateCurrentUsageKwh();
 
            //Assert
            Assert.True(usageOn > 0);
            Assert.False(usageOff > 0);
        }
 
        /// <summary>
        /// Verify(..., Times.Exactly(n)): розрахунок звертається до репозиторію пристроїв рівно один раз.
        /// </summary>
        [Fact]
        public void CalculateCurrentUsageKwh_Always_CallsRepositoryGetAllExactlyOnce()
        {
            //Arrange
            var service = CreateService(out var mockDevices, out _, out _);
            mockDevices.Setup(r => r.GetAll()).Returns(new List<Device> { CreateDevice(1, true, 100) });
 
            //Act
            service.CalculateCurrentUsageKwh();
 
            //Assert
            mockDevices.Verify(r => r.GetAll(), Times.Exactly(1));
        }
        
 
        /// <summary>
        /// Verify() + It.Is(predicate): при перевищенні ліміту (3 кВт &gt; 2 кВт)
        /// надсилається одне сповіщення з текстом про перевантаження.
        /// </summary>
        [Fact]
        public void CheckForOverload_UsageAboveLimit_SendsAlertOnce()
        {
            //Arrange
            var service = CreateService(out var mockDevices, out var mockPlans, out var mockNotify);
            mockDevices.Setup(r => r.GetAll()).Returns(new List<Device> { CreateDevice(1, true, 3000) });
            mockPlans.Setup(r => r.GetCurrentPlan()).Returns(CreatePlan(2.0));
 
            //Act
            service.CheckForOverload();
 
            //Assert
            mockNotify.Verify(n => n.SendAlert(It.Is<string>(m => m.Contains("Overload"))), Times.Once);
        }
 
        /// <summary>
        /// Verify(..., Times.Never): якщо споживання нижче за ліміт, сповіщення не надсилається.
        /// </summary>
        [Fact]
        public void CheckForOverload_UsageBelowLimit_NeverSendsAlert()
        {
            //Arrange
            var service = CreateService(out var mockDevices, out var mockPlans, out var mockNotify);
            mockDevices.Setup(r => r.GetAll()).Returns(new List<Device> { CreateDevice(1, true, 1000) });
            mockPlans.Setup(r => r.GetCurrentPlan()).Returns(CreatePlan(5.0));
 
            //Act
            service.CheckForOverload();
 
            //Assert
            mockNotify.Verify(n => n.SendAlert(It.IsAny<string>()), Times.Never);
        }
 
        /// <summary>
        /// Verify(..., Times.Never): гранична умова — споживання дорівнює ліміту
        /// (перевірка строго "більше"), сповіщення немає.
        /// </summary>
        [Fact]
        public void CheckForOverload_UsageEqualsLimit_NeverSendsAlert()
        {
            //Arrange
            var service = CreateService(out var mockDevices, out var mockPlans, out var mockNotify);
            mockDevices.Setup(r => r.GetAll()).Returns(new List<Device> { CreateDevice(1, true, 2000) });
            mockPlans.Setup(r => r.GetCurrentPlan()).Returns(CreatePlan(2.0));
 
            //Act
            service.CheckForOverload();
 
            //Assert
            mockNotify.Verify(n => n.SendAlert(It.IsAny<string>()), Times.Never);
        }
 
        /// <summary>
        /// Theory / InlineData + Verify(..., Times.Exactly(n)): кількість сповіщень
        /// залежить від співвідношення споживання та ліміту.
        /// </summary>
        [Theory]
        [InlineData(3000, 2.0, 1)]
        [InlineData(2001, 2.0, 1)]
        [InlineData(2000, 2.0, 0)]
        [InlineData(500, 2.0, 0)]
        [InlineData(0, 0.0, 0)]
        [InlineData(1, 0.0, 1)]
        public void CheckForOverload_VariousUsageAndLimit_SendsExpectedNumberOfAlerts(
            double watts, double limitKwh, int expectedAlerts)
        {
            //Arrange
            var service = CreateService(out var mockDevices, out var mockPlans, out var mockNotify);
            mockDevices.Setup(r => r.GetAll()).Returns(new List<Device> { CreateDevice(1, true, watts) });
            mockPlans.Setup(r => r.GetCurrentPlan()).Returns(CreatePlan(limitKwh));
 
            //Act
            service.CheckForOverload();
 
            //Assert
            mockNotify.Verify(n => n.SendAlert(It.IsAny<string>()), Times.Exactly(expectedAlerts));
        }
 
        /// <summary>
        /// Verify(..., Times.AtLeastOnce): перевірка перевантаження отримує і план, і список пристроїв.
        /// </summary>
        [Fact]
        public void CheckForOverload_Always_ReadsPlanAndDevices()
        {
            //Arrange
            var service = CreateService(out var mockDevices, out var mockPlans, out _);
            mockDevices.Setup(r => r.GetAll()).Returns(new List<Device> { CreateDevice(1, true, 100) });
            mockPlans.Setup(r => r.GetCurrentPlan()).Returns(CreatePlan(10));
 
            //Act
            service.CheckForOverload();
 
            //Assert
            mockPlans.Verify(r => r.GetCurrentPlan(), Times.AtLeastOnce);
            mockDevices.Verify(r => r.GetAll(), Times.AtLeastOnce);
        }
 
        /// <summary>
        /// Verify(..., Times.Never): перевірка перевантаження не змінює план.
        /// </summary>
        [Fact]
        public void CheckForOverload_Overloaded_NeverUpdatesPlan()
        {
            //Arrange
            var service = CreateService(out var mockDevices, out var mockPlans, out _);
            mockDevices.Setup(r => r.GetAll()).Returns(new List<Device> { CreateDevice(1, true, 5000) });
            mockPlans.Setup(r => r.GetCurrentPlan()).Returns(CreatePlan(1.0));
 
            //Act
            service.CheckForOverload();
 
            //Assert
            mockPlans.Verify(r => r.UpdatePlan(It.IsAny<EnergyPlan>()), Times.Never);
        }
        
 
        /// <summary>
        /// Verify() + It.Is(predicate): у UpdatePlan передається план з новим лімітом.
        /// </summary>
        [Fact]
        public void UpdateEnergyLimit_ValidLimit_UpdatesPlanWithNewLimit()
        {
            //Arrange
            const double NEW_LIMIT = 8.5;
            var service = CreateService(out _, out var mockPlans, out _);
            mockPlans.Setup(r => r.GetCurrentPlan()).Returns(CreatePlan(5.0));
 
            //Act
            service.UpdateEnergyLimit(NEW_LIMIT);
 
            //Assert
            mockPlans.Verify(r => r.UpdatePlan(It.Is<EnergyPlan>(p => p.DailyLimitKwh == NEW_LIMIT)), Times.Once);
        }
 
        /// <summary>
        /// Assert.NotNull + Assert.Equal + Assert.NotEqual: збережений план не null,
        /// має новий ліміт, що відрізняється від старого.
        /// </summary>
        [Fact]
        public void UpdateEnergyLimit_ValidLimit_SavedPlanHasNewLimit()
        {
            //Arrange
            const double OLD_LIMIT = 5.0;
            const double NEW_LIMIT = 12.0;
            var service = CreateService(out _, out var mockPlans, out _);
            mockPlans.Setup(r => r.GetCurrentPlan()).Returns(CreatePlan(OLD_LIMIT));
 
            EnergyPlan? savedPlan = null;
            mockPlans.Setup(r => r.UpdatePlan(It.IsAny<EnergyPlan>()))
                     .Callback<EnergyPlan>(p => savedPlan = p);
 
            //Act
            service.UpdateEnergyLimit(NEW_LIMIT);
 
            //Assert
            Assert.NotNull(savedPlan);
            Assert.Equal(NEW_LIMIT, savedPlan!.DailyLimitKwh);
            Assert.NotEqual(OLD_LIMIT, savedPlan.DailyLimitKwh);
        }
 
        /// <summary>
        /// Assert.Equal: зміна ліміту не зачіпає дату останнього скидання (LastReset).
        /// </summary>
        [Fact]
        public void UpdateEnergyLimit_ValidLimit_PreservesLastReset()
        {
            //Arrange
            var lastReset = new DateTime(2026, 10, 1, 8, 0, 0);
            var service = CreateService(out _, out var mockPlans, out _);
            var plan = CreatePlan(5.0, lastReset);
            mockPlans.Setup(r => r.GetCurrentPlan()).Returns(plan);
 
            //Act
            service.UpdateEnergyLimit(7.0);
 
            //Assert
            Assert.Equal(lastReset, plan.LastReset);
        }
 
        /// <summary>
        /// Theory / InlineData + It.Is(predicate): оновлення працює для різних значень ліміту.
        /// </summary>
        [Theory]
        [InlineData(0.0)]
        [InlineData(1.5)]
        [InlineData(10)]
        [InlineData(100.25)]
        public void UpdateEnergyLimit_VariousLimits_SavesCorrectLimit(double newLimit)
        {
            //Arrange
            var service = CreateService(out _, out var mockPlans, out _);
            mockPlans.Setup(r => r.GetCurrentPlan()).Returns(CreatePlan(3.0));
 
            //Act
            service.UpdateEnergyLimit(newLimit);
 
            //Assert
            mockPlans.Verify(r => r.UpdatePlan(It.Is<EnergyPlan>(p => p.DailyLimitKwh == newLimit)), Times.Once);
        }
 
        /// <summary>
        /// Verify(..., Times.Exactly(n)): за один виклик план читається і зберігається по одному разу.
        /// </summary>
        [Fact]
        public void UpdateEnergyLimit_Always_ReadsAndSavesPlanExactlyOnce()
        {
            //Arrange
            var service = CreateService(out _, out var mockPlans, out _);
            mockPlans.Setup(r => r.GetCurrentPlan()).Returns(CreatePlan(4.0));
 
            //Act
            service.UpdateEnergyLimit(6.0);
 
            //Assert
            mockPlans.Verify(r => r.GetCurrentPlan(), Times.Exactly(1));
            mockPlans.Verify(r => r.UpdatePlan(It.IsAny<EnergyPlan>()), Times.Exactly(1));
        }
 
        /// <summary>
        /// Verify(..., Times.AtMost(n)) + Times.Never: зміна ліміту не надсилає сповіщень
        /// і не опитує репозиторій пристроїв.
        /// </summary>
        [Fact]
        public void UpdateEnergyLimit_Always_DoesNotNotifyAndDoesNotReadDevices()
        {
            //Arrange
            var service = CreateService(out var mockDevices, out var mockPlans, out var mockNotify);
            mockPlans.Setup(r => r.GetCurrentPlan()).Returns(CreatePlan(4.0));
 
            //Act
            service.UpdateEnergyLimit(6.0);
 
            //Assert
            mockNotify.Verify(n => n.SendAlert(It.IsAny<string>()), Times.AtMost(0));
            mockDevices.Verify(r => r.GetAll(), Times.Never);
        }
 
        /// <summary>
        /// Assert.Throws + Verify(..., Times.Never): якщо репозиторій повернув null-план,
        /// метод кидає <see cref="NullReferenceException"/> і нічого не зберігає.
        /// </summary>
        [Fact]
        public void UpdateEnergyLimit_PlanIsNull_ThrowsNullReferenceException()
        {
            //Arrange
            var service = CreateService(out _, out var mockPlans, out _);
            mockPlans.Setup(r => r.GetCurrentPlan()).Returns((EnergyPlan)null!);
 
            //Act & Assert
            Assert.Throws<NullReferenceException>(() => service.UpdateEnergyLimit(5.0));
            mockPlans.Verify(r => r.UpdatePlan(It.IsAny<EnergyPlan>()), Times.Never);
        }
    }
}