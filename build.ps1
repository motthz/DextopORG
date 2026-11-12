$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Force "$PSScriptRoot\bin" | Out-Null
& $compiler /nologo /codepage:65001 /target:winexe /optimize+ /out:"$PSScriptRoot\bin\DextopORG.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "$PSScriptRoot\src\Program.cs" "$PSScriptRoot\src\FocusView.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilazione fallita' }
if ($args -contains '-Test') {
    & $compiler /nologo /codepage:65001 /target:exe /out:"$PSScriptRoot\bin\LayoutTests.exe" /main:LayoutTests /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "$PSScriptRoot\src\Program.cs" "$PSScriptRoot\src\FocusView.cs" "$PSScriptRoot\tests\LayoutTests.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Compilazione test fallita' }
    & "$PSScriptRoot\bin\LayoutTests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Test falliti' }
}
