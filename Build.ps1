# 杀旧进程 + 编译新版（含 -bg/-svc 后台支持与三模式自动运行 UI）
$ErrorActionPreference = 'Continue'

$old = Get-Process -Name 'WiFiAutoRefresh' -ErrorAction SilentlyContinue
if ($old) {
    foreach ($p in $old) {
        Write-Output ("kill old PID " + $p.Id)
        Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Milliseconds 600
} else {
    Write-Output 'no old process'
}

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$src = 'C:\Scripts\WiFiRefresh\WiFiAutoRefresh.cs'
$out = 'C:\Scripts\WiFiRefresh\WiFiAutoRefresh.exe'
$ico = 'C:\Scripts\WiFiRefresh\WiFiAutoRefresh.ico'
$cscArgs = @(
    '/target:winexe',
    ('/out:' + $out),
    $src,
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    '/r:System.ServiceProcess.dll',
    ('/win32icon:' + $ico),
    '/codepage:65001'
)
& $csc $cscArgs
Write-Output ('COMPILE_EXIT=' + $LASTEXITCODE)
if (Test-Path $out) {
    $f = Get-Item $out
    Write-Output ('EXE bytes=' + $f.Length + ' time=' + $f.LastWriteTime.ToString('HH:mm:ss'))
}
