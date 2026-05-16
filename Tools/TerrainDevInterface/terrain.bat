@echo off
setlocal
set "TERRAIN_PROJECT_DIR=%~dp0"
for %%I in ("%TERRAIN_PROJECT_DIR%..\..") do set "TERRAIN_REPO_ROOT=%%~fI"
pushd "%TERRAIN_REPO_ROOT%"
for /d %%D in ("%TERRAIN_PROJECT_DIR%bin\Run\*") do rd /s /q "%%D" 2>nul
set TERRAIN_RUN_ID=%RANDOM%%RANDOM%
set "TERRAIN_OUTPUT=%TERRAIN_PROJECT_DIR%bin\Run\%TERRAIN_RUN_ID%\"
set "TERRAIN_EXE=%TERRAIN_OUTPUT%TerrainDevInterface.exe"
dotnet build "%TERRAIN_PROJECT_DIR%TerrainDevInterface.csproj" --nologo --verbosity quiet -p:OutputPath="%TERRAIN_OUTPUT%"
if errorlevel 1 (
    echo Failed to build TerrainDevInterface.
    pause
    popd
    exit /b 1
)
start "" "%TERRAIN_EXE%"
set TERRAIN_EXIT_CODE=%ERRORLEVEL%
popd
exit /b %TERRAIN_EXIT_CODE%
