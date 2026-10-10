param([Parameter(Mandatory=$true)][string]$Executable)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
New-Item -ItemType Directory -Force artifacts/qa | Out-Null
$exe = (Resolve-Path $Executable).Path
$sessionId=[Guid]::NewGuid().ToString('N')
$smokeRoot=Join-Path $env:TEMP "Luma-smoke-$sessionId"
$launchArgs=@('--smoke-test',"--smoke-session=$sessionId")
$process = Start-Process $exe -ArgumentList $launchArgs -PassThru
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
    function Find-Id([string]$Id) {
        $condition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,$Id)
        for ($j = 0; $j -lt 100; $j++) {
            $found=$window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$condition)
            if ($found) { return $found }
            Start-Sleep -Milliseconds 100
        }
        throw "Control did not become available: $Id"
    }
    function Select-Card([string]$Name) {
        $item=Wait-Control $Name; $pattern=$null
        while ($item -and !$item.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern,[ref]$pattern)) {
            $item=[System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($item)
        }
        if (!$item) { throw "Card is not selectable: $Name" }; $pattern.Select()
    }
    function Wait-Control([string]$Name) {
        for ($j = 0; $j -lt 120; $j++) { $found = Find-Control $Name; if ($found) { return $found }; Start-Sleep -Milliseconds 500 }
        throw "Control did not become available: $Name"
    }
    Wait-Control 'fixture000.bmp' | Out-Null
    Start-Sleep -Seconds 3
    $nodes = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
    $names = @($nodes | ForEach-Object { $_.Current.Name })
    $names | Set-Content artifacts/qa/accessibility-tree.txt
    foreach ($name in @('Library','Search library','+ Add folder or drive','Settings')) {
        if ($names -notcontains $name) { throw "Required control not found: $name" }
    }
    $galleryCondition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty,'MediaGrid')
    $gallery = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$galleryCondition)
    $scroll = $gallery.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
    for ($k=0; $k -lt 18; $k++) { $scroll.SetScrollPercent(-1,100); Start-Sleep -Milliseconds 400 }
    Wait-Control 'fixture259.bmp' | Out-Null
    $metricsPath=Join-Path $smokeRoot "gallery-metrics.json"
    if (Test-Path $metricsPath) {
        $metrics=Get-Content $metricsPath -Raw | ConvertFrom-Json
        if ($metrics.PeakDecoded -ge 240) { throw 'Decoded images grew to almost the entire fixture catalog.' }
        Copy-Item $metricsPath artifacts/qa/gallery-metrics.json
    } else { throw 'Gallery did not produce image-lifetime metrics.' }
    $scroll.SetScrollPercent(-1,0)
    Wait-Control 'fixture000.bmp'  | Out-Null
    $search = Wait-Control 'Search library'
    $search.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('fixture003')
    Start-Sleep -Seconds 2
    Wait-Control 'fixture003.bmp' | Out-Null
    if (Find-Control 'fixture000.bmp') { throw 'Search did not filter the library.' }
    $card = Wait-Control 'fixture003.bmp'
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
    Wait-Control 'fixture003.bmp' | Out-Null
    if (Find-Control 'fixture000.bmp') { throw 'Favorites filter included a non-favorite.' }
    (Wait-Control 'Library').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Seconds 2
    Wait-Control 'fixture000.bmp' | Out-Null
    # Create and use an actual virtual album through the desktop UI.
    (Wait-Control '+ Album').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 500
    (Find-Id 'DialogInput').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('Smoke album')
    (Wait-Control 'Save').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Seconds 1
    (Wait-Control 'Reset').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $search.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('fixture003')
    Start-Sleep -Seconds 1
    Select-Card 'fixture003.bmp'
    (Wait-Control 'Details').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    (Wait-Control 'Add to album…').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    (Wait-Control 'Add').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Seconds 1
    (Wait-Control 'Album filter').GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    (Wait-Control 'Smoke album (1)').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $search.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('')
    Start-Sleep -Seconds 1
    Wait-Control 'fixture003.bmp' | Out-Null
    if (Find-Control 'fixture000.bmp') { throw 'Album included a file that was not added.' }
    (Wait-Control 'Details').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    (Wait-Control 'Reset').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Wait-Control 'fixture000.bmp' | Out-Null
    # Select the registered fixture source; folder navigation uses saved catalog data.
    (Wait-Control 'Source filter').GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    Start-Sleep -Milliseconds 500
    $nodes = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
    $sourceItem = $nodes | Where-Object { $_.Current.Name -like '*Fixture originals' } | Select-Object -First 1
    if (!$sourceItem) { throw 'Fixture source choice was not found.' }
    $sourceItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $folder = Wait-Control 'Trips'
    $folderPattern = $null
    while ($folder -and !$folder.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern,[ref]$folderPattern)) {
        $folder = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($folder)
    }
    if (!$folder) { throw 'Cached folder cannot be selected.' }
    $folderPattern.Select()
    Start-Sleep -Seconds 2
    Wait-Control 'fixture007.bmp' | Out-Null
    if (Find-Control 'fixture000.bmp') { throw 'Folder navigation did not filter the library.' }
    (Wait-Control 'Up to parent folder').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Wait-Control 'fixture000.bmp' | Out-Null
    (Wait-Control 'Drive tools').GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    (Wait-Control 'Clear thumbnail cache').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    (Wait-Control 'Clear previews').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Wait-Control 'Saved preview cache cleared.' | Out-Null
    (Wait-Control 'Prepare offline previews').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    for ($j = 0; $j -lt 120; $j++) {
        $nodes = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
        $coverage = $nodes | Where-Object { $_.Current.Name -like '260 / 260 previews saved*' } | Select-Object -First 1
        if ($coverage) { break }; Start-Sleep -Milliseconds 500
    }
    if (!$coverage) { throw 'Offline preparation did not report full fixture coverage.' }
    $coverage.Current.Name | Set-Content artifacts/qa/offline-coverage.txt
    # Make only the isolated synthetic fixture source unavailable, then re-query the cached library.
    $fixtureRoot = Join-Path $smokeRoot "Fixture originals"
    if (!(Test-Path $fixtureRoot)) { throw 'Isolated smoke fixture root was not found.' }
    Move-Item $fixtureRoot ($fixtureRoot + ' disconnected')
    (Wait-Control 'Rescan sources').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Seconds 2
    Wait-Control 'fixture000.bmp' | Out-Null
    Wait-Control 'Trips' | Out-Null
    (Wait-Control 'Check offline coverage').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Seconds 1
    $nodes = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
    if (!($nodes | Where-Object { $_.Current.Name -like '260 / 260 previews saved*' })) { throw 'Preview coverage was lost when fixture source went offline.' }

    # Persist view choices, restart the same isolated library and verify native controls restore them.
    (Wait-Control 'Gallery view options').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    (Find-Id 'ThumbnailSlider').GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue(170)
    (Find-Id 'NamesToggle').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
    $process.CloseMainWindow() | Out-Null
    if (!$process.WaitForExit(10000)) { throw 'App did not close after completed work.' }
    $process=Start-Process $exe -ArgumentList $launchArgs -PassThru
    $window=$null
    for ($i=0;$i -lt 60;$i++) {
        Start-Sleep -Milliseconds 500
        $condition=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty,$process.Id)
        $window=[System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children,$condition)
        if($window -and $window.Current.Name -eq 'Luma') { break }
    }
    Wait-Control 'fixture000.bmp' | Out-Null
    (Wait-Control 'Gallery view options').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    if ((Find-Id 'ThumbnailSlider').GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).Current.Value -ne 170) { throw 'Thumbnail size did not survive restart.' }
    if ((Find-Id 'NamesToggle').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -ne [System.Windows.Automation.ToggleState]::Off) { throw 'Filename preference did not survive restart.' }
    [System.Windows.Forms.SendKeys]::SendWait('{ESC}')
    Copy-Item (Join-Path $smokeRoot 'settings.json') artifacts/qa/restored-settings.json
    Start-Sleep -Seconds 2
    $rectangle = $window.Current.BoundingRectangle
    $bitmap = [System.Drawing.Bitmap]::new([int]$rectangle.Width,[int]$rectangle.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen([int]$rectangle.X,[int]$rectangle.Y,0,0,$bitmap.Size)
    $bitmap.Save((Join-Path (Get-Location) 'artifacts/qa/startup.png'))
    $graphics.Dispose(); $bitmap.Dispose()
    'PASS: native window, 260-item continuous scrolling and image recycling, search, selection, favorites, albums, cached folders, offline preparation and view preference restart work through Windows UI Automation.' | Set-Content artifacts/qa/result.txt
} catch {
    if ($window) {
        $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition) | ForEach-Object { "$($_.Current.AutomationId) | $($_.Current.Name)" } | Set-Content artifacts/qa/failure-controls.txt
        $rect=$window.Current.BoundingRectangle
        if ($rect.Width -gt 0 -and $rect.Height -gt 0) {
            $bmp=[System.Drawing.Bitmap]::new([int]$rect.Width,[int]$rect.Height)
            $g=[System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen([int]$rect.X,[int]$rect.Y,0,0,$bmp.Size)
            $bmp.Save((Join-Path (Get-Location) 'artifacts/qa/failure.png')); $g.Dispose(); $bmp.Dispose()
        }
    }
    $log = Join-Path (Split-Path $exe) 'startup-error.txt'
    if (Test-Path $log) { Get-Content $log; Copy-Item $log artifacts/qa/startup-error.txt }
    Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddMinutes(-5)} -ErrorAction SilentlyContinue |
        Where-Object { $_.Message -match 'Luma|Microsoft.UI.Xaml' } | Select-Object -First 5 -ExpandProperty Message | Set-Content artifacts/qa/windows-events.txt
    throw
} finally {
    if (!$process.HasExited) { $process.CloseMainWindow() | Out-Null; if (!$process.WaitForExit(5000)) { $process.Kill() } }
}
