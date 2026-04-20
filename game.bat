@echo off
setlocal
cd /d "%~dp0"

set "GAME_EXE=%cd%\bin\Debug\net8.0-windows\op.io.exe"
if exist "%GAME_EXE%" (
  start "" "%GAME_EXE%"
  endlocal
  exit /b 0
)

powershell -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command ^
  "Start-Process -WindowStyle Hidden -FilePath 'dotnet' -ArgumentList 'run' -WorkingDirectory '%cd%'"
endlocal
exit /b
