Set oShell = CreateObject("WScript.Shell")
Set oFSO   = CreateObject("Scripting.FileSystemObject")
Dim dir : dir = oFSO.GetParentFolderName(WScript.ScriptFullName)
oShell.Run "powershell -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command " & _
    """Start-Process -WindowStyle Hidden -FilePath 'dotnet' -ArgumentList 'run' -WorkingDirectory '" & dir & "'""", _
    0, False
