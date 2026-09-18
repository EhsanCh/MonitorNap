@echo off
setlocal enabledelayedexpansion

title MonitorNap Build Script
echo ======================================================
echo           Building MonitorNap Release Binary
echo ======================================================

:: 1. Resolve Windows directory dynamically
if not defined SystemRoot set SystemRoot=%SystemDrive%\Windows

:: 2. Locate .NET Framework C# compiler (prefer 64-bit, fallback to 32-bit or PATH)
set "CSC="
if exist "%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" (
    set "CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
) else if exist "%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe" (
    set "CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
) else (
    for /f "delims=" %%I in ('where csc.exe 2^>nul') do (
        set "CSC=%%I"
    )
)

:: 3. Validate compiler availability
if not defined CSC (
    echo [ERROR] Compatible .NET Framework C# compiler was not found.
    echo Please ensure .NET Framework 4.x is installed on this system.
    echo.
    if not defined CI pause
    exit /b 1
)

echo [INFO] Using compiler: %CSC%

:: 4. Ensure distribution output directory exists
if not exist "%~dp0dist" mkdir "%~dp0dist"

:: 5. Check for custom application icon
set "ICON_SWITCH="
if exist "%~dp0assets\app.ico" (
    set "ICON_SWITCH=/win32icon:"%~dp0assets\app.ico""
) else (
    echo [WARN] assets\app.ico not found. Compiling with default icon.
)

:: 6. Compile sources from src\ directory
echo [INFO] Compiling sources from src\ ...
"%CSC%" /target:winexe /optimize+ %ICON_SWITCH% /out:"%~dp0dist\MonitorNap.exe" "%~dp0src\*.cs"

if %errorlevel% equ 0 (
    echo.
    echo [SUCCESS] Binary successfully generated at:
    echo           %~dp0dist\MonitorNap.exe
) else (
    echo.
    echo [FAILED] Compilation encountered errors [Code: %errorlevel%]
    if not defined CI pause
    exit /b %errorlevel%
)

echo ======================================================
if not defined CI pause

