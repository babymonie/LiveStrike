# LiveStrike - Publishing Guide for ya

## Publishing Options

### 1. Self-Contained Executables (Recommended)
Creates standalone executables that don't require .NET runtime to be installed.

**64-bit (Most Common)**
```powershell
dotnet publish -p:PublishProfile=Win64-SelfContained
```
Output: `bin\Release\Publish\Win64\LiveStrike.exe`

**32-bit (Legacy Systems)**
```powershell
dotnet publish -p:PublishProfile=Win32-SelfContained
```
Output: `bin\Release\Publish\Win32\LiveStrike.exe`

### 2. Portable Version
Requires .NET 9.0 runtime to be installed on target machine.

```powershell
dotnet publish -p:PublishProfile=Portable
```
Output: `bin\Release\Publish\Portable\` (multiple files)

## Manual Publishing Commands

### For GitHub Releases (All Platforms)
```powershell
# 64-bit Self-Contained
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o "publish\win-x64"

# 32-bit Self-Contained  
dotnet publish -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o "publish\win-x86"

# ARM64 Self-Contained
dotnet publish -c Release -r win-arm64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o "publish\win-arm64"
```

## Automated Publishing

### GitHub Actions
The repository includes a GitHub Actions workflow that automatically:
- Builds and tests on every push/PR
- Creates releases on version tags
- Publishes to all supported platforms
- Generates installers and uninstallers

To trigger a release:
```powershell
git tag v1.0.0
git push origin v1.0.0
```

## Distribution

### Single Executable Distribution
The self-contained builds create a single `.exe` file that includes:
- The application
- .NET runtime
- Node.js server files
- All dependencies

### What Gets Included
- Main application executable
- Node.js server (`server/` folder)
- Required .NET libraries
- Native dependencies

### File Size
- 64-bit: ~70-80 MB
- 32-bit: ~65-75 MB
- ARM64: ~75-85 MB

## Installation Notes

### For End Users
1. Download the appropriate executable for your system
2. Run the `.exe` file - no installation required
3. The app will automatically:
   - Start the Node.js server
   - Create settings in `%LOCALAPPDATA%\LiveStrike\`
   - Add system tray icon

### Antivirus Considerations
Self-contained executables may trigger antivirus warnings. Consider:
- Code signing certificate (for professional distribution)
- Submitting to antivirus vendors for whitelisting
- Including checksums for verification

## Advanced Options

### Code Signing
For professional distribution, add to project file:
```xml
<PropertyGroup>
  <SignAssembly>true</SignAssembly>
  <AssemblyOriginatorKeyFile>path\to\certificate.pfx</AssemblyOriginatorKeyFile>
</PropertyGroup>
```

### MSIX Packaging
For Microsoft Store or enterprise distribution:
```powershell
dotnet publish -c Release -r win-x64 -p:PublishProfile=MSIX
```

## Troubleshooting

### Build Errors
- Ensure Node.js is installed for server file inclusion
- Close any running instances before building
- Use `dotnet clean` before publishing if needed

### Large File Sizes
- Use `PublishTrimmed=true` to reduce size (may break some features)
- Consider portable version for multiple deployments
- Compress final executables with UPX if needed

### Path Issues
- Server files are automatically included in build
- Settings are stored in user's AppData folder
- Temporary files use system temp directory