Get-Process -Name WiFiAutoRefresh -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        Stop-Process -Id $_.Id -Force -ErrorAction Stop
        Write-Host "killed PID $($_.Id)"
    } catch {
        Write-Host "fail PID $($_.Id): $($_.Exception.Message)"
    }
}
Start-Sleep -Seconds 1
$remaining = Get-Process -Name WiFiAutoRefresh -ErrorAction SilentlyContinue
if ($remaining) { Write-Host "still running"; $remaining | ForEach-Object { Write-Host $_.Id } } else { Write-Host "all killed" }
