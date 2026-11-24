@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "ROOT=%CD%"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC%" (
    echo [ERROR] .NET Framework C# compiler was not found.
    echo         Install .NET Framework 4.8 Developer Pack or build with Visual Studio.
    exit /b 1
)

if not exist "%ROOT%\lib\dnlib.dll" (
    echo [ERROR] Missing dependency: lib\dnlib.dll
    exit /b 1
)

if not exist "%ROOT%\dist" mkdir "%ROOT%\dist"
if not exist "%ROOT%\tests\bin" mkdir "%ROOT%\tests\bin"

echo [1/3] Building HOPE5000TitleFix.exe ...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 ^
    /out:"%ROOT%\dist\HOPE5000TitleFix.exe" ^
    /win32manifest:"%ROOT%\src\app.manifest" ^
    /resource:"%ROOT%\lib\dnlib.dll",dnlib.dll ^
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
    /r:"%ROOT%\lib\dnlib.dll" ^
    "%ROOT%\src\AssemblyInfo.cs" ^
    "%ROOT%\src\Program.cs" ^
    "%ROOT%\src\MainForm.cs" ^
    "%ROOT%\src\Patcher.cs" ^
    "%ROOT%\src\PathDetector.cs" ^
    "%ROOT%\src\FileSafety.cs"
if errorlevel 1 goto :failed

echo [2/3] Building smoke tests ...
"%CSC%" /nologo /target:exe /platform:anycpu /optimize+ /codepage:65001 ^
    /out:"%ROOT%\tests\bin\PatcherSmokeTests.exe" ^
    /r:System.dll /r:System.Core.dll /r:"%ROOT%\dist\HOPE5000TitleFix.exe" /r:"%ROOT%\lib\dnlib.dll" ^
    "%ROOT%\tests\PatcherSmokeTests.cs"
if errorlevel 1 goto :failed
copy /y "%ROOT%\dist\HOPE5000TitleFix.exe" "%ROOT%\tests\bin\HOPE5000TitleFix.exe" >nul
copy /y "%ROOT%\lib\dnlib.dll" "%ROOT%\tests\bin\dnlib.dll" >nul

echo [3/3] Build completed.
echo.
echo   dist\HOPE5000TitleFix.exe
echo   tests\bin\PatcherSmokeTests.exe
echo.
echo Run tests\bin\PatcherSmokeTests.exe with a copy of HOPE5000.exe.
exit /b 0

:failed
echo.
echo [ERROR] Build failed.
exit /b 1
