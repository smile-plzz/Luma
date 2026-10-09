param([Parameter(Mandatory=$true)][string]$Executable)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force artifacts/qa | Out-Null
$exe = (Resolve-Path $Executable).Path
$process = Start-Process $exe -ArgumentList "--smoke-test" -PassThru
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
    function Find-Control([string]$Name) {
        $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$Name)
        return $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
    }
    function Wait-Control([string]$Name) {
        for ($j = 0; $j -lt 60; $j++) { $found = Find-Control $Name; if ($found) { return $found }; Start-Sleep -Milliseconds 500 }
        throw "Control did not become available: $Name"
    }
    Wait-Control 'fixture00.bmp' | Out-Null
    Start-Sleep -Seconds 3
    $nodes = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
    $names = @($nodes | ForEach-Object { $_.Current.Name })
    $names | Set-Content artifacts/qa/accessibility-tree.txt
    foreach ($name in @('All media','Search library','+ Add folder or drive','Settings')) {
        if ($names -notcontains $name) { throw "Required control not found: $name" }
    }
    $search = Wait-Control 'Search library'
    $search.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('fixture03')
    Start-Sleep -Seconds 2
    Wait-Control 'fixture03.bmp' | Out-Null
    if (Find-Control 'fixture00.bmp') { throw 'Search did not filter the library.' }
    $card = Wait-Control 'fixture03.bmp'
    # The named border is inside the selectable GridViewItem; walk to its selection pattern.
    $selectable = $card
    $selectionPattern = $null
    while ($selectable -and !$selectable.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern,[ref]$selectionPattern)) {
        $selectable = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($selectable)
    }
    if (!$selectable) { throw 'Media item is not selectable through accessibility.' }
    $selectionPattern.Select()
    (Wait-Control '☆ Favorite').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Seconds 2
    $search.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('')
    Start-Sleep -Seconds 2
    (Wait-Control 'Favorites').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Seconds 2
    Wait-Control 'fixture03.bmp' | Out-Null
    if (Find-Control 'fixture00.bmp') { throw 'Favorites filter included a non-favorite.' }
    (Wait-Control 'All media').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Seconds 2
    Wait-Control 'fixture00.bmp' | Out-Null
    $rectangle = $window.Current.BoundingRectangle
    $bitmap = [System.Drawing.Bitmap]::new([int]$rectangle.Width,[int]$rectangle.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen([int]$rectangle.X,[int]$rectangle.Y,0,0,$bitmap.Size)
    $bitmap.Save((Join-Path (Get-Location) 'artifacts/qa/startup.png'))
    $graphics.Dispose(); $bitmap.Dispose()
    'PASS: native window, indexed fixture grid, search, selection and favorites work through Windows UI Automation.' | Set-Content artifacts/qa/result.txt
} catch {
    $log = Join-Path (Split-Path $exe) 'startup-error.txt'
    if (Test-Path $log) { Get-Content $log; Copy-Item $log artifacts/qa/startup-error.txt }
    Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddMinutes(-5)} -ErrorAction SilentlyContinue |
        Where-Object { $_.Message -match 'Luma|Microsoft.UI.Xaml' } | Select-Object -First 5 -ExpandProperty Message | Set-Content artifacts/qa/windows-events.txt
    throw
} finally {
    if (!$process.HasExited) { $process.CloseMainWindow() | Out-Null; if (!$process.WaitForExit(5000)) { $process.Kill() } }
}
