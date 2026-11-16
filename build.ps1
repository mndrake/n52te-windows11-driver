<#
.SYNOPSIS
  Builds Install-n52te.exe from the sources in src\.

.DESCRIPTION
  1. Checks src\editor\n52te Editor.ahk for syntax errors with AutoHotkey's /Validate.
     The editor ships as this script plus the standard AutoHotkey64.exe interpreter,
     not as a compiled exe (compiled AutoHotkey programs trip antivirus heuristics).
  2. Compiles src\remapper\Program.cs (n52te_interception.exe) with the
     .NET Framework 4.x C# compiler that ships with Windows.
  3. Compiles src\installer\Installer.cs, embedding the editor script, AutoHotkey64.exe
     and its license, the remapper and the files in src\installer\resources.
  4. Writes the installer's SHA-256 next to it.

  Output goes to build\. Nothing is installed.

  Tools needed (not committed; tools\ is git-ignored):
    tools\ahk\AutoHotkey64.exe and tools\ahk\license.txt
      from the AutoHotkey 2.0.30 zip: https://www.autohotkey.com/download/2.0/
#>
[CmdletBinding()]
param(
    [string]$AhkDir,
    [string]$OutDir,
    [string]$InstallerName = 'Install-n52te.exe'
)

$ErrorActionPreference = 'Stop'
# Windows PowerShell 5.1 leaves $PSScriptRoot empty inside param() defaults.
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $AhkDir) { $AhkDir = Join-Path $root 'tools\ahk' }
if (-not $OutDir) { $OutDir = Join-Path $root 'build' }
$src = Join-Path $root 'src'
$res = Join-Path $src 'installer\resources'
$obj = Join-Path $OutDir 'obj'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$ahkExe = Join-Path $AhkDir 'AutoHotkey64.exe'
$ahkLicense = Join-Path $AhkDir 'license.txt'
foreach ($tool in $csc, $ahkExe, $ahkLicense) {
    if (-not (Test-Path $tool)) { throw "Missing build tool: $tool (see the header of build.ps1)" }
}

New-Item -ItemType Directory -Force $obj | Out-Null

function Invoke-Csc([string[]]$CscArgs) {
    & $csc /nologo /noconfig /platform:x64 /optimize+ /utf8output @CscArgs
    if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
}

# 1. Editor (AutoHotkey v2 script): syntax check only
Write-Host '== n52te Editor.ahk'
$editorScript = Join-Path $src 'editor\n52te Editor.ahk'
$errFile = Join-Path $obj 'ahk-validate.txt'
$p = Start-Process $ahkExe -ArgumentList '/ErrorStdOut', '/Validate', "`"$editorScript`"" `
    -Wait -PassThru -NoNewWindow -RedirectStandardError $errFile
if ($p.ExitCode -ne 0) {
    Get-Content $errFile | Write-Host
    throw "The editor script has errors (AutoHotkey /Validate exit code $($p.ExitCode))"
}

# 2. Remapper (console app)
Write-Host '== n52te_interception.exe'
$remapperExe = Join-Path $obj 'n52te_interception.exe'
Invoke-Csc @('/target:exe', "/out:$remapperExe", '/r:System.dll', '/r:System.Core.dll', (Join-Path $src 'remapper\Program.cs'))

# 3. Installer (WinForms app with embedded payload)
Write-Host "== $InstallerName"
$installerExe = Join-Path $OutDir $InstallerName
$payload = [ordered]@{
    RES_REMAPPER    = $remapperExe
    RES_DLL         = Join-Path $res 'interception.dll'
    RES_AHK         = $ahkExe
    RES_AHK_LICENSE = $ahkLicense
    RES_EDITOR      = $editorScript
    RES_DRIVER      = Join-Path $res 'install-interception.exe'
    RES_ICO         = Join-Path $res 'infomalena.ico'
    RES_FAVICON     = Join-Path $res 'favicon.png'
    RES_FOOTER      = Join-Path $res 'logo_footer.png'
    RES_LOGO        = Join-Path $res 'logo.png'
}
$cscArgs = @(
    '/target:winexe', "/out:$installerExe",
    "/win32icon:$(Join-Path $res 'infomalena.ico')",
    "/win32manifest:$(Join-Path $src 'installer\app.manifest')",
    '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll', '/r:Microsoft.CSharp.dll'
)
foreach ($name in $payload.Keys) { $cscArgs += "/resource:$($payload[$name]),$name" }
$cscArgs += Join-Path $src 'installer\Installer.cs'
Invoke-Csc $cscArgs

# 4. Checksum
$hash = (Get-FileHash $installerExe -Algorithm SHA256).Hash
Set-Content -Path "$installerExe.sha256" -Value $hash -NoNewline -Encoding Ascii
Write-Host ''
Write-Host "Built:   $installerExe"
Write-Host "SHA-256: $hash"
