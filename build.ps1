param([switch]$Test, [switch]$Package)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Microsoft .NET Framework 4.x compiler was not found.' }
$outputDirectory = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$sourceDirectory = Join-Path $PSScriptRoot 'src'
$sources = @(Get-ChildItem -LiteralPath $sourceDirectory -Filter '*.cs' | ForEach-Object { $_.FullName })
$executable = Join-Path $outputDirectory 'PowerPlanSwitcher.exe'
& $compiler /nologo /utf8output /target:winexe /platform:anycpu /optimize+ "/out:$executable" "/win32manifest:$sourceDirectory\app.manifest" "/win32icon:$sourceDirectory\app.ico" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll @sources
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
Copy-Item -LiteralPath (Join-Path $sourceDirectory 'app.config') -Destination ($executable + '.config') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $outputDirectory 'README.md') -Force
Write-Host "Built: $executable"
if ($Test) {
    $testDirectory = Join-Path $PSScriptRoot 'artifacts'
    New-Item -ItemType Directory -Path $testDirectory -Force | Out-Null
    $testExecutable = Join-Path $testDirectory 'PowerPlanSwitcher.Tests.exe'
    & $compiler /nologo /utf8output /target:exe /optimize+ "/out:$testExecutable" /reference:System.dll /reference:System.Core.dll (Join-Path $sourceDirectory 'PowerPlans.cs') (Join-Path $PSScriptRoot 'tests\ServiceTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & $testExecutable
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    $uiTestExecutable = Join-Path $testDirectory 'PowerPlanSwitcher.UiSmoke.exe'
    & $compiler /nologo /utf8output /target:exe /main:UiSmoke /optimize+ "/out:$uiTestExecutable" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll @sources (Join-Path $PSScriptRoot 'tests\UiSmoke.cs')
    if ($LASTEXITCODE -ne 0) { throw 'UI smoke test build failed.' }
    Copy-Item -LiteralPath (Join-Path $sourceDirectory 'app.config') -Destination ($uiTestExecutable + '.config') -Force
    & $uiTestExecutable
    if ($LASTEXITCODE -ne 0) { throw 'UI smoke test failed.' }
    $emojiTestExecutable = Join-Path $testDirectory 'PowerPlanSwitcher.EmojiTests.exe'
    & $compiler /nologo /utf8output /target:exe /main:EmojiTests /optimize+ "/out:$emojiTestExecutable" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll @sources (Join-Path $PSScriptRoot 'tests\EmojiTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Emoji test build failed.' }
    Copy-Item -LiteralPath (Join-Path $sourceDirectory 'app.config') -Destination ($emojiTestExecutable + '.config') -Force
    & $emojiTestExecutable
    if ($LASTEXITCODE -ne 0) { throw 'Emoji tests failed.' }
}
if ($Package) {
    $packagePath = Join-Path $PSScriptRoot 'PowerPlanSwitcher.zip'
    Compress-Archive -LiteralPath $executable,($executable + '.config'),(Join-Path $outputDirectory 'README.md') -DestinationPath $packagePath -Force
    Write-Host "Packaged: $packagePath"
}
