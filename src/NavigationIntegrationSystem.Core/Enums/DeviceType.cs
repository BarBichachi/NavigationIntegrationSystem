namespace NavigationIntegrationSystem.Core.Enums;

// Defines supported INS device types in the application
public enum DeviceType
{
    VN310,
    Tmaps100X
}

// Adding new device
// 1) Add -> DeviceType.NewIns
// 2) Create -> NewInsInsDevice : InsDeviceBase
// 3) Create -> NewInsDeviceModule : IInsDeviceModule
// 4) In HostBuilderFactory -> services.AddSingleton<IInsDeviceModule, NewInsDeviceModule>()