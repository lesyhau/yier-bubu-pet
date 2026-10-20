<#
.SYNOPSIS
    Builds the Yier & Bubu Windows installer(s) with Inno Setup 6.

.DESCRIPTION
    For each architecture: publishes a self-contained single-file exe to dist\win-<arch>,
    copies it to dist\YierBubuPet-win-<arch>.exe (the portable download), then compiles
    installer\YierBubuPet.iss into dist\YierBubuPet-<version>-setup-<arch>.exe.
    Attach all of dist\*.exe to the GitHub release.

    Needs the .NET 8 SDK and Inno Setup 6.3 or newer (https://jrsoftware.org/isdl.php).

.EXAMPLE
    .\installer\build.ps1                    # x64 only
    .\installer\build.ps1 -Arch x64, arm64   # both
#>
param(
    [ValidateSet("x64", "arm64")]
    [string[]] $Arch = @("x64"),

    # Path to ISCC.exe; found automatically when Inno Setup is installed in the usual place or on PATH.
    [string] $Iscc
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot

if (-not $Iscc) {
    $candidates = @(
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source,
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LocalAppData\Programs\Inno Setup 6\ISCC.exe"
    )
    $Iscc = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $Iscc) { throw "Inno Setup 6 not found. Install it from https://jrsoftware.org/isdl.php or pass -Iscc <path to ISCC.exe>." }
}

foreach ($a in $Arch) {
    $out = Join-Path $repo "dist\win-$a"
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }

    Write-Host "== Publishing win-$a ==" -ForegroundColor Cyan
    dotnet publish (Join-Path $repo "src\YierBubuPet\YierBubuPet.csproj") -c Release -r "win-$a" --self-contained `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
        -o $out
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for win-$a" }

    # The portable exe, named the way the app's self-update looks for it on the Releases page.
    Copy-Item (Join-Path $out "YierBubuPet.exe") (Join-Path $repo "dist\YierBubuPet-win-$a.exe") -Force

    Write-Host "== Building installer for $a ==" -ForegroundColor Cyan
    & $Iscc "/DArch=$a" (Join-Path $PSScriptRoot "YierBubuPet.iss")
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed for $a" }
}

Write-Host "`nDone. Installers are in $(Join-Path $repo 'dist')" -ForegroundColor Green
