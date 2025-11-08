@echo off
echo LiveStrike - Publishing Script
echo ==============================

echo.
echo Cleaning previous builds...
dotnet clean OverlayWindow.csproj >nul 2>&1

echo.
echo Building Self-Contained 64-bit version...
dotnet publish OverlayWindow.csproj -p:PublishProfile=Win64-SelfContained

if %ERRORLEVEL% EQU 0 (
    echo ✓ 64-bit build successful
    for %%F in ("bin\Release\Publish\Win64\LiveStrike.exe") do echo   File size: %%~zF bytes
) else (
    echo ✗ 64-bit build failed
    goto :end
)

echo.
echo Building Self-Contained 32-bit version...
dotnet publish OverlayWindow.csproj -p:PublishProfile=Win32-SelfContained

if %ERRORLEVEL% EQU 0 (
    echo ✓ 32-bit build successful
    for %%F in ("bin\Release\Publish\Win32\LiveStrike.exe") do echo   File size: %%~zF bytes
) else (
    echo ✗ 32-bit build failed
    goto :end
)

echo.
echo Building Portable version...
dotnet publish OverlayWindow.csproj -p:PublishProfile=Portable

if %ERRORLEVEL% EQU 0 (
    echo ✓ Portable build successful
) else (
    echo ✗ Portable build failed
    goto :end
)

echo.
echo ========================================
echo All builds completed successfully YAY!
echo.
echo Output locations:
echo   64-bit: bin\Release\Publish\Win64\LiveStrike.exe
echo   32-bit: bin\Release\Publish\Win32\LiveStrike.exe
echo   Portable: bin\Release\Publish\Portable\
echo.
echo The self-contained versions include everything needed to run.
echo The portable version requires .NET 9.0 runtime to be installed.
echo ========================================

:end
pause