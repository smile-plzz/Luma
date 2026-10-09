param([Parameter(Mandatory=$true)][string]$Executable)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force artifacts/qa | Out-Null
$exe = (Resolve-Path $Executable).Path
$process = Start-Process $exe -PassThru
try {
    $window = $null
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Milliseconds 500
        $process.Refresh()
        if ($process.HasExited) { throw "Luma exited during startup: $($process.ExitCode)" }
        $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id)
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
        if ($window -and $window.Current.Name -eq 'Luma') { break }
    }
    if (!$window) { throw 'Luma did not create a desktop window.' }
    Start-Sleep -Seconds 3
    $nodes = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
    $names = @($nodes | ForEach-Object { $_.Current.Name })
    $names | Set-Content artifacts/qa/accessibility-tree.txt
    foreach ($name in @('All media','Search library','+ Add folder or drive','Settings')) {
        if ($names -notcontains $name) { throw "Required control not found: $name" }
    }
    $rectangle = $window.Current.BoundingRectangle
    $bitmap = [System.Drawing.Bitmap]::new([int]$rectangle.Width,[int]$rectangle.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen([int]$rectangle.X,[int]$rectangle.Y,0,0,$bitmap.Size)
    $bitmap.Save((Join-Path (Get-Location) 'artifacts/qa/startup.png'))
    $graphics.Dispose(); $bitmap.Dispose()
    'PASS: native window and required accessible controls are available.' | Set-Content artifacts/qa/result.txt
} catch {
    $log = Join-Path (Split-Path $exe) 'startup-error.txt'
    if (Test-Path $log) { Get-Content $log; Copy-Item $log artifacts/qa/startup-error.txt }
    Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddMinutes(-5)} -ErrorAction SilentlyContinue |
        Where-Object { $_.Message -match 'Luma|Microsoft.UI.Xaml' } | Select-Object -First 5 -ExpandProperty Message | Set-Content artifacts/qa/windows-events.txt
    throw
} finally {
    if (!$process.HasExited) { $process.CloseMainWindow() | Out-Null; if (!$process.WaitForExit(5000)) { $process.Kill() } }
}
