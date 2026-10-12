@echo off
rem Project Hymn build check (Slice 12). Close the Unity editor first, then double-click this file.
rem 1) EditMode tests  2) rebuild every scene + Windows build  3) smoke run of the build (every scene for a few seconds).
rem Results land in Logs\BuildCheck\ (summary.txt, tests.xml, build-report.txt, smoke-report.txt and the full logs).
setlocal
set "PROJ=%~dp0.."
for %%I in ("%PROJ%") do set "PROJ=%%~fI"
set "OUT=%PROJ%\Logs\BuildCheck"
if not exist "%OUT%" mkdir "%OUT%"
del /q "%OUT%\*.txt" "%OUT%\*.xml" 2>nul

for /f "tokens=2" %%v in ('findstr /b "m_EditorVersion:" "%PROJ%\ProjectSettings\ProjectVersion.txt"') do set "VER=%%v"
set "UNITY=%ProgramFiles%\Unity\Hub\Editor\%VER%\Editor\Unity.exe"
if not exist "%UNITY%" (
  echo Unity %VER% not found at "%UNITY%".> "%OUT%\summary.txt"
  type "%OUT%\summary.txt"
  pause
  exit /b 2
)

echo running> "%OUT%\status.txt"
echo [1/3] EditMode tests...
"%UNITY%" -batchmode -nographics -projectPath "%PROJ%" -runTests -testPlatform EditMode -testResults "%OUT%\tests.xml" -logFile "%OUT%\tests.log"
set "TESTS=%ERRORLEVEL%"

echo [2/3] Rebuild scenes + Windows build...
"%UNITY%" -batchmode -projectPath "%PROJ%" -executeMethod ProjectHymn.EditorTools.BuildCheck.Run -logFile "%OUT%\build.log"
set "BUILD=%ERRORLEVEL%"

set "SMOKE=skipped"
if exist "%PROJ%\Builds\Windows\ProjectHymn.exe" (
  echo [3/3] Smoke run of the build...
  rem Windowed, not batch mode, so the shaders and audio really run; it closes itself after every scene.
  start "" /wait "%PROJ%\Builds\Windows\ProjectHymn.exe" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -hymnSmoke -hymnSmokeOut "%OUT%\smoke-report.txt" -logFile "%OUT%\smoke.log"
  call set "SMOKE=%%ERRORLEVEL%%"
)

> "%OUT%\summary.txt" (
  echo Project Hymn build check
  echo tests exit code: %TESTS%   (0 = all passed)
  echo build exit code: %BUILD%   (0 = clean rebuild and build)
  echo smoke exit code: %SMOKE%   (0 = no errors in any scene)
)
echo done> "%OUT%\status.txt"
type "%OUT%\summary.txt"
if exist "%OUT%\build-report.txt" type "%OUT%\build-report.txt"
if exist "%OUT%\smoke-report.txt" type "%OUT%\smoke-report.txt"
echo.
echo Full logs: %OUT%
pause
