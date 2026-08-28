$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$app = Join-Path $root 'src\SDM.App'
$failures = [System.Collections.Generic.List[string]]::new()

function Assert-Text {
    param(
        [string]$Name,
        [string]$Path,
        [string]$Pattern,
        [switch]$Absent
    )

    $content = [IO.File]::ReadAllText($Path)
    $matched = [regex]::IsMatch($content, $Pattern, [Text.RegularExpressions.RegexOptions]::Singleline)
    $passed = if ($Absent) { -not $matched } else { $matched }
    if ($passed) {
        Write-Output "PASS $Name"
    }
    else {
        $failures.Add($Name)
        Write-Output "FAIL $Name"
    }
}

Assert-Text 'native-window-drag' (Join-Path $app 'MainWindow.xaml') '<WindowChrome' -Absent
Assert-Text 'search-is-direct-input' (Join-Path $app 'MainWindow.xaml') 'x:Name="SearchBox"[\s\S]*?Text="\{Binding Search, UpdateSourceTrigger=PropertyChanged\}"'
Assert-Text 'textbox-readable-minheight' (Join-Path $app 'Themes\Controls.xaml') '<Style TargetType="TextBox">[\s\S]*?<Setter Property="MinHeight" Value="40"/>'
Assert-Text 'textbox-content-host' (Join-Path $app 'Themes\Controls.xaml') 'x:Name="PART_ContentHost"'
Assert-Text 'settings-no-empty-scrollbar' (Join-Path $app 'Views\SettingsWindow.xaml') '<ScrollViewer' -Absent
Assert-Text 'sniffer-explicit-cell-colors' (Join-Path $app 'Views\VideoSnifferWindow.xaml') 'GridViewColumn.CellTemplate[\s\S]*?Foreground="\{StaticResource TextBrush\}"'
Assert-Text 'browser-executable-discovery' (Join-Path $app 'Services\BrowserIntegration.cs') 'FindBrowserExecutables\(exe\)'
Assert-Text 'list-filename-light-text' (Join-Path $app 'MainWindow.xaml') 'Text="\{Binding FileName\}"[\s\S]*?Foreground="\{StaticResource TextBrush\}"'
Assert-Text 'listbox-light-foreground' (Join-Path $app 'Themes\Controls.xaml') '<Style TargetType="ListBox">[\s\S]*?<Setter Property="Foreground" Value="\{StaticResource TextBrush\}"/>'
Assert-Text 'add-url-scrollbar' (Join-Path $app 'Views\AddUrlWindow.xaml') '<ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto"'
Assert-Text 'chrome-page-argumentlist' (Join-Path $app 'Services\BrowserIntegration.cs') 'TryStartBrowser\(executable, \["--new-window", url\]\)'
Assert-Text 'chrome-skip-store-alias' (Join-Path $app 'Services\BrowserIntegration.cs') 'WindowsApps'
Assert-Text 'install-skips-explorer' (Join-Path $app 'Views\BrowserSetupWindow.xaml.cs') 'OpenFolder' -Absent
Assert-Text 'chrome-drag-card' (Join-Path $app 'Views\BrowserSetupWindow.xaml') 'x:Name="ChromeDragCard"'
Assert-Text 'extension-filedrop-drag' (Join-Path $app 'Views\BrowserSetupWindow.xaml.cs') 'SetFileDropList'

if ($failures.Count -gt 0) {
    Write-Output "RESULT=FAIL COUNT=$($failures.Count)"
    exit 1
}

Write-Output 'RESULT=PASS COUNT=15'
