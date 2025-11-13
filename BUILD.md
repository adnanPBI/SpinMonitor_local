# SpinMonitor Desktop - Build Guide

## 🏗️ Building from Source

### Prerequisites

1. **Visual Studio 2022** (or later)
   - Download: https://visualstudio.microsoft.com/downloads/
   - Required workloads:
     - .NET desktop development
     - Windows desktop development with C++

2. **.NET 9.0 SDK**
   - Download: https://dotnet.microsoft.com/download/dotnet/9.0
   - Verify installation: `dotnet --version`

3. **Git** (optional, for cloning)
   - Download: https://git-scm.com/downloads

---

## 📦 Building with Visual Studio

### 1. Open Solution

1. Launch Visual Studio 2022
2. Open `SpinMonitor.sln`
3. Wait for NuGet packages to restore

### 2. Build Configuration

Select build configuration:
- **Debug**: For development and testing
- **Release**: For production deployment

### 3. Build

**Method 1: Visual Studio UI**
- Click **Build → Build Solution** (Ctrl+Shift+B)

**Method 2: Developer Command Prompt**
```cmd
msbuild SpinMonitor.sln /p:Configuration=Release /p:Platform=x64
```

### 4. Output Location

Built files will be in:
```
src\SpinMonitor.Desktop\bin\Release\net9.0-windows10.0.19041.0\win-x64\
```

---

## 🖥️ Building with .NET CLI

### 1. Restore Dependencies

```bash
dotnet restore
```

### 2. Build Debug

```bash
dotnet build --configuration Debug
```

### 3. Build Release

```bash
dotnet build --configuration Release
```

### 4. Run Application

```bash
dotnet run --project src/SpinMonitor.Desktop/SpinMonitor.Desktop.csproj
```

---

## 📦 Publishing for Distribution

### Single-File Executable (Recommended)

Create a self-contained, single-file executable:

```bash
dotnet publish src/SpinMonitor.Desktop/SpinMonitor.Desktop.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o publish
```

**Output:** `publish/SpinMonitor.Desktop.exe` (~80MB)

**Advantages:**
- Single executable file
- No .NET runtime required on target machine
- Easy distribution

---

### Framework-Dependent Deployment

Smaller executable that requires .NET 9.0 Runtime on target machine:

```bash
dotnet publish src/SpinMonitor.Desktop/SpinMonitor.Desktop.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o publish
```

**Output:** `publish/SpinMonitor.Desktop.exe` (~5MB)

**Advantages:**
- Much smaller file size
- Faster updates

**Disadvantages:**
- Requires .NET 9.0 Runtime on target machine

---

## 📋 Creating Installer (Optional)

### Using Inno Setup

1. **Install Inno Setup**
   - Download: https://jrsoftware.org/isdl.php

2. **Create Installer Script** (`installer.iss`):

```iss
[Setup]
AppName=SpinMonitor Desktop
AppVersion=1.0.0
DefaultDirName={autopf}\SpinMonitor
DefaultGroupName=SpinMonitor
OutputDir=installer
OutputBaseFilename=SpinMonitor-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: recursesubdirs

[Icons]
Name: "{group}\SpinMonitor"; Filename: "{app}\SpinMonitor.Desktop.exe"
Name: "{autodesktop}\SpinMonitor"; Filename: "{app}\SpinMonitor.Desktop.exe"

[Run]
Filename: "{app}\SpinMonitor.Desktop.exe"; Description: "Launch SpinMonitor"; Flags: nowait postinstall skipifsilent
```

3. **Compile Installer:**
   - Open `installer.iss` in Inno Setup
   - Click **Build → Compile**
   - Output: `installer/SpinMonitor-Setup.exe`

---

### Using WiX Toolset (MSI Installer)

1. **Install WiX Toolset**
   - Download: https://wixtoolset.org/releases/

2. **Add WiX Project to Solution**

3. **Build MSI**:
```bash
msbuild installer\SpinMonitor.Installer.wixproj /p:Configuration=Release
```

---

## 🔧 Build Troubleshooting

### Error: "SDK 'Microsoft.NET.Sdk' not found"

**Solution:** Install .NET 9.0 SDK
```bash
winget install Microsoft.DotNet.SDK.9
```

### Error: "SQLitePCLRaw native library not found"

**Solution:** Already handled by project file. Ensure clean rebuild:
```bash
dotnet clean
dotnet build
```

### Error: "FFmpeg not found"

**Solution:** FFmpeg is not included in source. Download separately:
```powershell
.\get-ffmpeg.ps1
```

### Error: "Cannot find MainWindow.xaml"

**Solution:** Ensure all files are included in project:
1. Right-click project in Solution Explorer
2. Click **Reload Project**

---

## 🧪 Testing Build

### 1. Unit Tests (None Currently)

To add unit tests:
```bash
dotnet new xunit -n SpinMonitor.Tests
```

### 2. Manual Testing Checklist

- [ ] Application launches without errors
- [ ] Settings window opens and saves
- [ ] FFmpeg validation works
- [ ] Stream monitoring starts/stops
- [ ] Detections appear in Live Now panel
- [ ] CSV files created in logs/
- [ ] MySQL logging works (if enabled)
- [ ] Application closes cleanly

---

## 📊 Build Configurations

### Debug Build

**Optimizations:** None
**Debugging:** Full symbols included
**Size:** Larger (~100MB)
**Performance:** Slower

**Use for:** Development and debugging

### Release Build

**Optimizations:** Full
**Debugging:** Minimal symbols
**Size:** Smaller (~80MB)
**Performance:** Faster

**Use for:** Production deployment

---

## 🚀 Continuous Integration (Optional)

### GitHub Actions Workflow

Create `.github/workflows/build.yml`:

```yaml
name: Build SpinMonitor

on: [push, pull_request]

jobs:
  build:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '9.0.x'

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --configuration Release

    - name: Publish
      run: dotnet publish src/SpinMonitor.Desktop/SpinMonitor.Desktop.csproj -c Release -o publish

    - name: Upload artifact
      uses: actions/upload-artifact@v3
      with:
        name: SpinMonitor-Windows
        path: publish/
```

---

## 📝 Version Management

### Update Version Number

Edit `SpinMonitor.Desktop.csproj`:

```xml
<PropertyGroup>
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
  <FileVersion>1.0.0.0</FileVersion>
  <Version>1.0.0</Version>
</PropertyGroup>
```

### Semantic Versioning

Follow semantic versioning: `MAJOR.MINOR.PATCH`

- **MAJOR**: Breaking changes
- **MINOR**: New features (backward compatible)
- **PATCH**: Bug fixes

---

## 🔐 Code Signing (Optional)

### Sign Executable with Certificate

```bash
signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com publish\SpinMonitor.Desktop.exe
```

**Benefits:**
- Removes "Unknown Publisher" warning
- Increases user trust
- Required for some enterprise deployments

---

## 📦 Distribution Checklist

Before distributing:

- [ ] Build in Release configuration
- [ ] Test on clean Windows 10/11 machine
- [ ] Include FFmpeg download script
- [ ] Include README.md
- [ ] Include sample appsettings.json
- [ ] Include sample streams.json
- [ ] Sign executable (optional)
- [ ] Create installer (optional)
- [ ] Test installer on clean machine

---

## 🆘 Getting Help

**Build Issues:**
1. Check Visual Studio Error List
2. Clean solution and rebuild
3. Verify .NET SDK installed
4. Check NuGet package restore

**Runtime Issues:**
1. Test in Debug configuration
2. Check logs/ folder
3. Verify FFmpeg installed
4. Check Windows Event Viewer

---

**Last Updated:** 2025-11-13
**Framework:** .NET 9.0
**Platform:** Windows 10/11 (x64)
