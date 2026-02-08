// ---------------------------------------------------------
// FILE: .\src\NavigationIntegrationSystem.Infrastructure\TO_BE_DELETED\INTEGRATION_GUIDE.md
// ---------------------------------------------------------

# NIS Integration Guide

This document outlines the steps required to integrate the standalone **NavigationIntegrationSystem (NIS)** into the larger **VIC Solution**.

---

## 🛑 Phase 1: File Deletions (Legacy & Shims)

The following files were created to simulate the infrastructure of the main solution. They must be **deleted** or **replaced** with references to the existing production projects.

### 1. Infrastructure Shims (Delete)
* `src\NavigationIntegrationSystem.Infrastructure\TO_BE_DELETED\BinaryFileRecorderEnhanced.cs` -> Use `Infrastructure.FileManagement.DataRecording`
* `src\NavigationIntegrationSystem.Infrastructure\TO_BE_DELETED\DataRecordHeader.cs` -> Use `Infrastructure.FileManagement.DataRecording`
* `src\NavigationIntegrationSystem.Infrastructure\TO_BE_DELETED\WGS84Data.cs` -> Use `Infrastructure.DataStructures`
* `src\NavigationIntegrationSystem.Infrastructure\TO_BE_DELETED\NEDData.cs` -> Use `Infrastructure.DataStructures`
* `src\NavigationIntegrationSystem.Infrastructure\TO_BE_DELETED\EulerData.cs` -> Use `Infrastructure.Navigation.EulerCalculations`
* `src\NavigationIntegrationSystem.Infrastructure\TO_BE_DELETED\EulerAngles.cs` -> Use `Infrastructure.Navigation.EulerCalculations`
* `src\NavigationIntegrationSystem.Infrastructure\TO_BE_DELETED\Singleton.cs` -> Use `Infrastructure.Templates`
* `src\NavigationIntegrationSystem.Infrastructure\TO_BE_DELETED\Log4Net.cs` -> Use Main App's Logging Adapter

### 2. Temporary Services (Delete)
* `src\NavigationIntegrationSystem.UI\Services\Recording\CsvTestingService.cs` -> **DELETE completely.** This was for standalone verification only.
    * *Action:* Remove all references to `CsvTestingService` from `IntegrationSnapshotService.cs`.

---

## 📂 Phase 2: External Moves (RecordDecoderPro)

The following files define the binary contract and decoding logic. They must be moved to the **RecordDecoderPro** project (or a shared library referenced by it).

### Files to Move
1.  `IntegratedInsOutputItem.cs` (Move to `RecordDecoderPro.ItemTemplates`)
2.  `IntegratedInsOutput_Data.cs` (Move to Shared Infrastructure)
3.  `IntegratedInsOutput_CommFrame.cs` (Move to Shared Infrastructure)
4.  `IntegratedInsOutputStatusFlags.cs` (Move to Shared Infrastructure)
5.  `IntegratedValueTriplet.cs` (Move to Shared Infrastructure)

### Registration
In the `RecordDecoderPro` project (likely in `ItemFactory.cs` or `RecordTypeManager.cs`), register the new item:

```csharp
case DataRecordType.IntegratedInsOutputRawData: // 50
    return new IntegratedInsOutputItem();
```

---

## 🛠️ Phase 3: Code Modifications & Wiring

### 1. Namespace Refactoring
Perform a global Find & Replace to align namespaces with the solution structure.
* **Find:** `NavigationIntegrationSystem.Infrastructure`
* **Replace:** `[MainSolutionName].Infrastructure`

### 2. Dependency Injection (ServiceCollection)
The standalone app uses `HostBuilderFactory`. In the main application, create an extension method to register NIS.

**Create `NisServiceExtensions.cs`:**
```csharp
public static class NisServiceExtensions
{
    public static IServiceCollection AddNavigationIntegrationSystem(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Configuration
        services.Configure<NisSettings>(configuration.GetSection("Nis"));
        
        // 2. Devices Domain
        services.AddSingleton<InsDeviceRegistry>();
        services.AddSingleton<IInsDeviceRegistry>(sp => sp.GetRequiredService<InsDeviceRegistry>());
        services.AddSingleton<IInsDeviceInstanceProvider>(sp => sp.GetRequiredService<InsDeviceRegistry>());
        services.AddSingleton<DeviceCatalogService>();
        
        // 3. Device Modules
        services.AddSingleton<IInsDeviceModule, Vn310DeviceModule>();
        services.AddSingleton<IInsDeviceModule, Tmaps100XDeviceModule>();
        services.AddSingleton<IInsDeviceModule, ManualDeviceModule>();
        services.AddSingleton<DevicesModuleBootstrapper>(); // Ensure this is resolved at startup!

        // 4. Recording
        // Ensure "NisRecordingService" uses the REAL "BinaryFileRecorderEnhanced" singleton from the main app
        services.AddSingleton<IRecordingService, NisRecordingService>(); 
        services.AddSingleton<IntegrationSnapshotService>(); 

        // 5. ViewModels
        services.AddSingleton<IntegrationViewModel>();
        services.AddSingleton<DevicesViewModel>();
        services.AddSingleton<MainViewModel>(); // Or merge logic into main Shell ViewModel
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<LogsViewModel>();

        // 6. UI Services
        services.AddSingleton<IDialogService, DialogService>(); // Or use main app's DialogService

        return services;
    }
}
```

### 3. Startup Logic (Crucial)
In the main application's startup routine (`App.xaml.cs` or `Program.cs`), you **MUST** resolve these two services to initialize the system:

```csharp
// 1. Register Device Types
container.GetRequiredService<DevicesModuleBootstrapper>(); 

// 2. Start Recording Listeners
container.GetRequiredService<IntegrationSnapshotService>(); 
```

### 4. Path Management
In `NisRecordingService.cs` and `DevicesConfigService.cs`:
* **Current:** Uses `AppDomain.CurrentDomain.BaseDirectory`.
* **Change:** Inject the main application's `IPathProvider` or `ILogPaths` to ensure files are saved to the correct data drive (e.g., `D:\Recordings`).

---

## 🖥️ Phase 4: UI Integration

1.  **Pages:** Move `IntegrationPage.xaml`, `DevicesPage.xaml`, etc., into the main application's Views folder.
2.  **Navigation:** Add entries to the main `NavigationView` or Ribbon that navigate to `IntegrationPage`.
3.  **Recording Controls:**
    * Take the logic from `ShellHeaderControl.xaml` (Start/Stop button + Indicator).
    * Place it in the **Main Window TitleBar** or **Status Bar** of the parent application so it is always visible.
    * Bind it to the `MainViewModel.ToggleRecordingCommand`.

---

## ✅ Verification Checklist (Post-Merge)

1.  [ ] **Compilation:** No references to `TO_BE_DELETED` files.
2.  [ ] **Device ID:** Connect 2 devices of the same type. Verify they get ID 0 and 1 in the `.dat` file.
3.  [ ] **Binary Size:** Record 5 seconds. File should be > 0KB (approx 250KB).
4.  [ ] **Decoder:** Open the `.dat` file in `RecordDecoderPro`. Verify:
    * Status column is green/valid.
    * Calculated `VelocityTotal` matches the magnitude of N/E/D.
    * `Manual` device input appears correctly.