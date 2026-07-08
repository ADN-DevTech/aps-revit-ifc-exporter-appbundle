# CLAUDE.md — aps-revit-ifc-exporter-appbundle

## Project Summary and Target

This is a headless Revit Add-in for exporting IFC from RVT files. The project primarily ports C# classes from the [Autodesk/revit-ifc](https://github.com/Autodesk/revit-ifc) repository to enable IFC export functionality. Supporting new Revit versions requires porting code from that upstream repository, potentially including `IFCExportConfiguration.cs` and `IFCExportConfigurationsMap.cs`.

## New Revit Version Support Strategy

### Code Porting Strategy

Code ported from Autodesk/revit-ifc will reside in the `RevitIfcExporter/IFC` folder. Since Revit may change or deprecate APIs across versions, the project uses preprocessor directives (`#if`, `#else`, `#endif`) for conditional compilation to avoid compiling errors and introducing wrong Revit API.

### Folder Structure

Each Revit version receives its own project folder (e.g., `RevitIfcExporter2027`). The project file references Revit installation locations and includes a `PostBuild` script copying the bundle contents to the Revit addins directory. Configuration files like `PackageContents.xml` and `.addin` files document the add-in and specify supported Revit versions.

**Upstream reference:** classes `IFCExportConfiguration` and `IFCExportConfigurationsMap` are ported from tag `IFC_v26.4.0` of the revit-ifc repo and kept in sync manually.

### Adding a New Revit Version

Based on the real-world 2027 port ([issue #1](https://github.com/yiskang/aps-revit-ifc-exporter-appbundle/issues/1)):

**1. Port upstream IFC changes first (blocks everything else)**
   - Diff `IFCExportConfiguration.cs` / `IFCExportConfigurationsMap.cs` / the enum-support files in `RevitIfcExporter/IFC/` against the latest relevant tag of [Autodesk/revit-ifc](https://github.com/Autodesk/revit-ifc) and port changes manually, gated with `#if SinceRVTXXXX`.
   - If upstream changed a method signature (e.g. added a parameter), don't just add the parameter — split the call site with `#if SinceRVTXXXX ... #else ... #endif` so older versions keep the old signature. Same pattern for new/renamed enum values.
   - When merging upstream's own conditional branches into ours, watch for **duplicate calls** (e.g. two `AddOption()` invocations firing because both an old and a new branch ended up unconditional) — restructure so exactly one path executes per version.
   - Update `MainApp.cs` call sites to match any signature split (cumulative `#if`, same as above).

**2. Bump the shared project (`RevitIfcExporter/RevitIfcExporter.csproj`)**
   - Append the new `SinceRVTXXXX` constant to both Debug and Release `DefineConstants` (cumulative — never remove older constants).
   - Bump `TargetFramework` if the new Revit version requires it.
   - Bump the `Autodesk.Forge.DesignAutomation.Revit` NuGet reference and all DLL hint paths to the new Revit year.

**3. Create the new per-version project**
   - Copy the most recent version folder (e.g. `RevitIfcExporter2026` → `RevitIfcExporter2027`), including `RevitIfcExporter.bundle/`.
   - Rename the `.csproj` to match and update: `DefineConstants` (cumulative), `TargetFramework`, hint paths, and the `PostBuild` target's Addins folder year (e.g. `AppData\Autodesk\REVIT\Addins\2027`).
   - Generate **fresh GUIDs** for `Properties/AssemblyInfo.cs` and the `AddInId` in `RevitIfcExporter.addin` — reusing the prior version's GUIDs causes Revit add-in registration conflicts.
   - Update `PackageContents.xml` series/version attributes (e.g. `R2026` → `R2027`) and the `.addin` file to declare the new supported Revit version.
   - Confirm all shared `RevitIfcExporter/IFC/*.cs` files are linked via `<Compile Include>` (copy-paste from the prior project's csproj, don't hand-retype the list).

**4. Register in the solution**
   - Add the new project to `RevitIfcExporter.sln` with a unique project GUID and Debug/Release `ProjectConfigurationPlatforms` entries.
   - Sanity-check that the *previous* version's project is also registered — it was missed in a prior version bump and only caught during the 2027 work.

**5. Verify**
   - `dotnet build` the new project, the previous version's project (regression), and the full `.sln` — all must succeed with zero errors.
   - Confirm `RevitIfcExporter.zip` is produced in the bundle's output directory.
   - Manually upload the bundle to APS Design Automation and run an export against a real RVT file with the new `Autodesk.Revit+XXXX` engine.

**6. Update docs**
   - Update `README.md`: supported-versions badge, `Autodesk.Revit+XXXX` engine string in examples, prerequisites.

---

## Repository layout

```
RevitIfcExporter/          ← shared source files (linked into all version projects)
  MainApp.cs               ← entry point: IExternalDBApplication, DoExport()
  InputParams.cs           ← JSON-deserialized params.json model
  IFC/
    IFCExportConfiguration.cs        ← ported from revit-ifc, version-gated via #if
    IFCExportConfigurationsMap.cs    ← ported from revit-ifc
    IFCExportConfigurationsMap.partial.cs
    IFCExportConfigurationsUtils.cs
    ... (enums, comparers, phase/version/facility types)

RevitIfcExporter2023/      ← Revit 2023 project (.NET Framework 4.8, packages.config)
RevitIfcExporter2024/      ← Revit 2024 project (.NET Framework 4.8, packages.config)
RevitIfcExporter2025/      ← Revit 2025 project (net8.0, PackageReference)
RevitIfcExporter2026/      ← Revit 2026 project (net8.0, PackageReference)
RevitIfcExporter2027/      ← Revit 2027 project (net10.0-windows, PackageReference)
  RevitIfcExporter2027.csproj
  RevitIfcExporter.bundle/
    PackageContents.xml
    Contents/
      RevitIfcExporter.addin
      RevitIfcExporter.dll   ← built output copied here by PostBuild
      DesignAutomationBridge.dll

RevitIfcExporter.sln
```

Each version-specific project **links** all shared source files instead of duplicating them:

```xml
<Compile Include="..\RevitIfcExporter\MainApp.cs" Link="MainApp.cs" />
```

---

## Preprocessor convention

Version-specific behaviour is gated with cumulative `#if` defines. Each project's `.csproj` defines all constants up to its version:

| Project | DefineConstants |
|---|---|
| 2023 | *(none — baseline, no SinceRVT defines)* |
| 2024 | `SinceRVT2023;SinceRVT2024` |
| 2025 | `SinceRVT2023;SinceRVT2024;SinceRVT2025` |
| 2026 | `SinceRVT2023;SinceRVT2024;SinceRVT2025;SinceRVT2026` |
| 2027 | `SinceRVT2023;SinceRVT2024;SinceRVT2025;SinceRVT2026;SinceRVT2027` |

When adding new version-gated code, always use the `#if SinceRVTXXXX` pattern, never `#if REVIT2024` or `#else` chains that would require editing older projects.

---

## Build

Requirements: Visual Studio 2022+, Windows. 7-Zip installed at `C:\Program Files\7-Zip\7z.exe` (hardcoded in PostBuild). Revit 2023–2027 installed locally (DLLs referenced by absolute hint paths). Note: 2023/2024 are legacy `.NET Framework 4.8` projects (`packages.config`); 2025–2027 are SDK-style (`PackageReference`).

Build each version-specific project to produce:
- `RevitIfcExporter.bundle/Contents/RevitIfcExporter.dll` (copied by PostBuild)
- `RevitIfcExporter.zip` (appbundle zip, created by 7-Zip in PostBuild)

There are no automated tests. Validation is done by running a DA workitem against a real Revit file.

---

## Runtime inputs (`params.json`)

| Field | Required | Description |
|---|---|---|
| `exportSettingName` | yes | Name of saved IFC export configuration in the RVT file |
| `useExportSettingFile` | no | If `true`, loads `userExportSettings.json` instead of saved config |
| `viewId` | no | Unique ID of a Revit view to scope the export |
| `onlyExportVisibleElementsInView` | no | Override to force visible-elements-only export |
| `userDefinedPropertySetsFilenameOverride` | no | Override filename for user-defined property sets |
| `userDefinedParameterMappingFilenameOverride` | no | Override filename for parameter mapping |

---

## Key implementation notes

- `MainApp.DoExport()` is the single export path. It loads `params.json`, resolves the export config, fixes dependency file paths to be relative (via `FixDependeciesPath`), then calls `doc.Export()` inside a transaction that is rolled back unless `StoreIFCGUID` is set.
- `IFCClassificationMgr.DeleteObsoleteSchemas(doc)` must be called before the export transaction.
- The `ActiveViewId` API changed in Revit 2024: before 2024 it takes `int` (`.IntegerValue`); from 2024 it takes `ElementId` directly. This is already gated with `#if SinceRVT2024` in `MainApp.cs:172`.
- All DA output is logged via `MainApp.LogTrace()` which writes to stdout (visible in DA workitem reports).
