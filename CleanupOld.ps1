# Cleanup old WiFi tool files in C:\Scripts (keep WiFiRefresh only)
$dir = 'C:\Scripts'

# 1. Check and delete scheduled task pointing to old EXE
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = 'schtasks.exe'
$psi.Arguments = '/query /tn WiFiAutoRefresh /fo list /v'
$psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
try {
  $p = [System.Diagnostics.Process]::Start($psi)
  $p.WaitForExit()
  $out = $p.StandardOutput.ReadToEnd()
  if ($p.ExitCode -eq 0) {
    Write-Host '--- TASK BEFORE ---'
    $out -split "`r?`n" | Where-Object { $_ -match 'TaskToRun|Status' } | ForEach-Object { Write-Host $_ }
    $delPsi = New-Object System.Diagnostics.ProcessStartInfo
    $delPsi.FileName = 'schtasks.exe'
    $delPsi.Arguments = '/delete /tn WiFiAutoRefresh /f'
    $delPsi.UseShellExecute = $false
    $delPsi.RedirectStandardOutput = $true
    $delPsi.CreateNoWindow = $true
    $dp = [System.Diagnostics.Process]::Start($delPsi)
    $dp.WaitForExit()
    Write-Host ("TASK DELETED exit=" + $dp.ExitCode)
  } else {
    Write-Host 'no existing task'
  }
} catch {
  Write-Host ("task query err: " + $_)
}

# 2. Remove all top-level items in C:\Scripts except WiFiRefresh
Get-ChildItem $dir -Force | ForEach-Object {
  if ($_.Name -eq 'WiFiRefresh') { return }
  if ($_.PSIsContainer) {
    Write-Host ("REMOVE-DIR " + $_.FullName)
    Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
  } else {
    Write-Host ("REMOVE-FILE " + $_.Name)
    Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
  }
}

# 3. Kill old EXE if still running
Get-Process WiFiAutoRefresh -ErrorAction SilentlyContinue | Stop-Process -Force

# 4. Show result
Write-Host '--- AFTER CLEANUP ---'
Get-ChildItem $dir -Force | Format-Table Name,Length,LastWriteTime -AutoSize
