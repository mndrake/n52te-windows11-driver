<#
.SYNOPSIS
  Builds n52te-config.exe, n52te Editor.exe and Install-n52te.exe from the sources in src\.

.DESCRIPTION
  Uses the .NET Framework 4.x C# compiler that ships with Windows (C# 5); nothing else to install.

  1. Compiles src\core\*.cs and src\configtool\*.cs (n52te-config.exe), the command-line tool that
     reads, switches and writes the keypad's onboard keymaps, and runs its self-test
     (n52te-config selftest; uses Belkin's Default.ini too if N52TE_BELKIN_DEFAULT_INI points at it).
  2. Compiles src\core\*.cs and src\onboardeditor\*.cs (n52te Editor.exe), the onboard keymap editor.
  3. Compiles src\installer\Installer.cs (Install-n52te.exe), embedding both programs.
  4. Writes the installer's SHA-256 next to it.

  Output goes to build\. Nothing is installed. Both programs need UsbDk at run time, which the
  installer does not bundle.
#>
[CmdletBinding()]
param(
    [string]$OutDir,
    [string]$InstallerName = 'Install-n52te.exe'
)

$ErrorActionPreference = 'Stop'
# Windows PowerShell 5.1 leaves $PSScriptRoot empty inside param() defaults.
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $OutDir) { $OutDir = Join-Path $root 'build' }
$src = Join-Path $root 'src'
$obj = Join-Path $OutDir 'obj'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "Missing the .NET Framework C# compiler: $csc" }
New-Item -ItemType Directory -Force $obj | Out-Null

function Invoke-Csc([string[]]$CscArgs) {
    & $csc /nologo /noconfig /platform:x64 /optimize+ /utf8output /warnaserror+ @CscArgs
    if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
}

function Sources([string[]]$Folders) {
    @(Get-ChildItem ($Folders | ForEach-Object { Join-Path $src $_ }) -Filter *.cs | ForEach-Object FullName)
}

# 1. Keymap tool (console app) and its self-test
Write-Host '== n52te-config.exe'
$configExe = Join-Path $obj 'n52te-config.exe'
Invoke-Csc (@('/target:exe', "/out:$configExe", '/r:System.dll', '/r:System.Core.dll') + (Sources 'core', 'configtool'))
& $configExe selftest | Out-Host
if ($LASTEXITCODE -ne 0) { throw "n52te-config selftest failed (exit code $LASTEXITCODE)" }

# 2. Editor (WinForms)
Write-Host '== n52te Editor.exe'
$editorExe = Join-Path $obj 'n52te Editor.exe'
Invoke-Csc (@('/target:winexe', "/out:$editorExe", '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll') + (Sources 'core', 'onboardeditor'))

# 3. Installer (WinForms app with both programs embedded)
Write-Host "== $InstallerName"
$installerExe = Join-Path $OutDir $InstallerName
Invoke-Csc @(
    '/target:winexe', "/out:$installerExe",
    "/win32manifest:$(Join-Path $src 'installer\app.manifest')",
    '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll',
    '/r:System.Management.dll', '/r:Microsoft.CSharp.dll',
    "/resource:$editorExe,RES_EDITOR", "/resource:$configExe,RES_CONFIG",
    (Join-Path $src 'installer\Installer.cs')
)
foreach ($exe in $editorExe, $configExe) {
    try { Copy-Item $exe $OutDir -Force }
    catch { Write-Warning "couldn't update $(Split-Path -Leaf $exe) in $OutDir (is it running?); the new build is in $obj" }
}

# 4. Checksum
$hash = (Get-FileHash $installerExe -Algorithm SHA256).Hash
Set-Content -Path "$installerExe.sha256" -Value $hash -NoNewline -Encoding Ascii
Write-Host ''
Write-Host "Built:   $installerExe"
Write-Host "SHA-256: $hash"
