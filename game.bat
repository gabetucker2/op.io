@echo off
setlocal
cd /d "%~dp0"
powershell -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command ^
  "Start-Process -WindowStyle Hidden -FilePath 'dotnet' -ArgumentList 'run' -WorkingDirectory '%cd%'"
endlocal
exit /b
