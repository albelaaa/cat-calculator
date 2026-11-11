$ErrorActionPreference = 'Stop'

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The Windows .NET Framework C# compiler was not found. Install the Windows .NET Framework developer tools, then retry.'
}

$source = Join-Path $projectDir 'CatCalculator.cs'
$manifest = Join-Path $projectDir 'app.manifest'
$exe = Join-Path $projectDir 'CATCalculator.exe'

& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 `
    "/out:$exe" "/win32manifest:$manifest" `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll $source

if ($LASTEXITCODE -ne 0) {
    throw "C# compilation failed with exit code $LASTEXITCODE."
}

Write-Host "Built $exe"
