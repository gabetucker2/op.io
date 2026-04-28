@echo off
setlocal
pushd "%~dp0..\.."
for /d %%D in ("Tools\TerrainDevInterface\bin\Run\*") do rd /s /q "%%D" 2>nul
set TERRAIN_RUN_ID=%RANDOM%%RANDOM%
set TERRAIN_OUTPUT=bin\Run\%TERRAIN_RUN_ID%\
set TERRAIN_EXE=Tools\TerrainDevInterface\%TERRAIN_OUTPUT%TerrainDevInterface.exe
dotnet build "Tools\TerrainDevInterface\TerrainDevInterface.csproj" --nologo --verbosity quiet -p:OutputPath="%TERRAIN_OUTPUT%"
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
