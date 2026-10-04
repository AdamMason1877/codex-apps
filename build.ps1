param([string]$OutputName = 'Codex Apps.exe')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
# An original vector-style icon: a mint upward chevron above a small app grid.
$taskBitmap = New-Object System.Drawing.Bitmap 64,64
$taskGraphics = [System.Drawing.Graphics]::FromImage($taskBitmap)
$taskGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$taskGraphics.Clear([System.Drawing.Color]::FromArgb(20,28,39))
$taskPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(89,209,170)),6
$taskGraphics.DrawLines($taskPen, [System.Drawing.Point[]]@((New-Object System.Drawing.Point 17,27),(New-Object System.Drawing.Point 32,12),(New-Object System.Drawing.Point 47,27)))
$taskBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(239,244,249))
foreach ($taskX in @(16,36)) { foreach ($taskY in @(35,49)) { $taskGraphics.FillRectangle($taskBrush,$taskX,$taskY,12,8) } }
$taskIcon = [System.Drawing.Icon]::FromHandle($taskBitmap.GetHicon())
$taskStream = [System.IO.File]::Create((Join-Path $PSScriptRoot 'CodexApps.ico'))
$taskIcon.Save($taskStream)
$taskStream.Dispose(); $taskIcon.Dispose(); $taskBrush.Dispose(); $taskPen.Dispose(); $taskGraphics.Dispose(); $taskBitmap.Dispose()
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $taskCompiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$PSScriptRoot\$OutputName" "/win32icon:$PSScriptRoot\CodexApps.ico" "/win32manifest:$PSScriptRoot\app.manifest" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll "/reference:$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationClient.dll" "/reference:$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationTypes.dll" "/reference:$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" (Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object FullName)
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output "Built $PSScriptRoot\$OutputName"
