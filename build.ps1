<#
.SYNOPSIS
  Builds Instalar-n52te.exe from the sources in src\.

.DESCRIPTION
  1. Compiles src\editor\Editor n52te.ahk with Ahk2Exe (AutoHotkey v2 base).
  2. Compiles src\remapper\Program.cs (n52te_interception.exe) with the
     .NET Framework 4.x C# compiler that ships with Windows.
  3. Compiles src\installer\Installer.cs, embedding the two programs above
     plus the files in src\installer\resources, into the installer.
  4. Writes the installer's SHA-256 next to it.

  Output goes to build\. Nothing is installed or run.

  Tools needed (not committed; tools\ is git-ignored):
    tools\ahk\AutoHotkey64.exe      AutoHotkey 2.0.30  https://www.autohotkey.com/download/2.0/
    tools\ahk\Compiler\Ahk2Exe.exe  Ahk2Exe            https://github.com/AutoHotkey/Ahk2Exe/releases
#>
[CmdletBinding()]
param(
    [string]$AhkDir,
    [string]$OutDir,
    [string]$InstallerName = 'Instalar-n52te.exe'
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
$ahkBase = Join-Path $AhkDir 'AutoHotkey64.exe'
$ahk2exe = Join-Path $AhkDir 'Compiler\Ahk2Exe.exe'
foreach ($tool in $csc, $ahkBase, $ahk2exe) {
    if (-not (Test-Path $tool)) { throw "Missing build tool: $tool (see the header of build.ps1)" }
}

New-Item -ItemType Directory -Force $obj | Out-Null

function Invoke-Csc([string[]]$CscArgs) {
    & $csc /nologo /noconfig /platform:x64 /optimize+ /utf8output @CscArgs
    if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
}

# 1. Editor (AutoHotkey v2)
Write-Host '== Editor n52te.exe'
$editorExe = Join-Path $obj 'Editor n52te.exe'
Remove-Item $editorExe -ErrorAction SilentlyContinue
$ahkArgs = @(
    '/in', "`"$(Join-Path $src 'editor\Editor n52te.ahk')`"",
    '/out', "`"$editorExe`"",
    '/base', "`"$ahkBase`"",
    '/icon', "`"$(Join-Path $res 'infomalena.ico')`"",
    '/silent', 'verbose'
)
$p = Start-Process $ahk2exe -ArgumentList $ahkArgs -Wait -PassThru -NoNewWindow
if ($p.ExitCode -ne 0 -or -not (Test-Path $editorExe)) { throw "Ahk2Exe failed with exit code $($p.ExitCode)" }

# 2. Remapper (console app)
Write-Host '== n52te_interception.exe'
$remapperExe = Join-Path $obj 'n52te_interception.exe'
Invoke-Csc @('/target:exe', "/out:$remapperExe", '/r:System.dll', '/r:System.Core.dll', (Join-Path $src 'remapper\Program.cs'))

# 3. Installer (WinForms app with embedded payload)
Write-Host "== $InstallerName"
$installerExe = Join-Path $OutDir $InstallerName
$payload = [ordered]@{
    RES_REMAPPER = $remapperExe
    RES_DLL      = Join-Path $res 'interception.dll'
    RES_EDITOR   = $editorExe
    RES_DRIVER   = Join-Path $res 'install-interception.exe'
    RES_ICO      = Join-Path $res 'infomalena.ico'
    RES_FAVICON  = Join-Path $res 'favicon.png'
    RES_FOOTER   = Join-Path $res 'logo_footer.png'
    RES_LOGO     = Join-Path $res 'logo.png'
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
