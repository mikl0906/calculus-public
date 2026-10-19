<#
.SYNOPSIS
  Builds the Calculus Link .tsep: Release build -> Tsep/extension -> checks ->
  Tekla's TeklaExtensionPackage.BatchBuilder -> Tsep/out/CalculusLink_<version>.tsep

.DESCRIPTION
  The package version is the <Version> in CalculusLink.csproj (major.minor),
  so the app and the package can't drift. The packager ships with every
  Tekla Structures 2022+ installation; the newest one installed is used
  unless -TeklaBin points at a specific bin folder.

  Fails (and packages nothing) when:
  - a Tekla or Trimble assembly is in the build output — those must never be
    shipped; Tekla's patched .exe.config loads its own at runtime;
  - the files in Tsep/extension and the <File> list in TsepDefinition.xml
    differ — add or remove the <File> entries by hand (new Id = a new GUID).

.EXAMPLE
  ./Tsep/build-tsep.ps1
  ./Tsep/build-tsep.ps1 -TeklaBin 'C:\TeklaStructures\2024.0\bin'
#>
param(
    [string]$TeklaBin
)

$ErrorActionPreference = 'Stop'
$tsep = $PSScriptRoot
$root = Split-Path $tsep -Parent
$manifest = Join-Path $tsep 'TsepDefinition.xml'
$staging = Join-Path $tsep 'extension'
$outDir = Join-Path $tsep 'out'
$buildOutput = Join-Path $root 'CalculusLink\bin\Release\net48'

# --- Packager ---------------------------------------------------------------
if (-not $TeklaBin) {
    $TeklaBin = Get-ChildItem 'HKLM:\SOFTWARE\Trimble\Tekla Structures' -ErrorAction SilentlyContinue |
        ForEach-Object { Get-ItemProperty (Join-Path $_.PSPath 'setup') -ErrorAction SilentlyContinue } |
        Where-Object { $_.MainDir -and $_.Version } |
        Sort-Object { [version]$_.Version } -Descending |
        ForEach-Object { Join-Path $_.MainDir "$($_.Version)\bin" } |
        Where-Object { Test-Path (Join-Path $_ 'TeklaExtensionPackage.BatchBuilder.exe') } |
        Select-Object -First 1
}
$builder = if ($TeklaBin) { Join-Path $TeklaBin 'TeklaExtensionPackage.BatchBuilder.exe' }
if (-not $builder -or -not (Test-Path $builder)) {
    throw 'TeklaExtensionPackage.BatchBuilder.exe not found: install Tekla Structures, or pass -TeklaBin <its bin folder>.'
}

# --- Version ----------------------------------------------------------------
$csproj = [xml](Get-Content (Join-Path $root 'CalculusLink\CalculusLink.csproj') -Raw)
$version = [version](($csproj.Project.PropertyGroup | ForEach-Object { $_.Version }) | Where-Object { $_ } | Select-Object -First 1)
$packageVersion = "$($version.Major).$($version.Minor)"

# --- Build and stage --------------------------------------------------------
dotnet build (Join-Path $root 'CalculusLink.slnx') -c Release -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }

if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory $staging | Out-Null
Get-ChildItem $buildOutput -File | Where-Object { $_.Extension -in '.dll', '.exe', '.config', '.pdb' } |
    Copy-Item -Destination $staging

# --- Checks -----------------------------------------------------------------
$trimble = Get-ChildItem $staging -Filter '*.dll' | Where-Object { $_.Name -match '^(Tekla\.|Trimble\.|DotNetKit)' }
if ($trimble) {
    throw "Trimble assemblies in the build output (never ship them): $($trimble.Name -join ', ')"
}

$listed = ([xml](Get-Content $manifest -Raw)).TEP.Component.File |
    Where-Object { $_.Source -like '%Extension%\*' } |
    ForEach-Object { $_.Source.Substring('%Extension%\'.Length) }
$staged = (Get-ChildItem $staging -File).Name
$diff = Compare-Object @($listed) @($staged)
if ($diff) {
    $missing = ($diff | Where-Object SideIndicator -eq '<=').InputObject
    $unlisted = ($diff | Where-Object SideIndicator -eq '=>').InputObject
    throw ("TsepDefinition.xml and the build output differ.`n" +
        "  Listed but not built: $($missing -join ', ')`n" +
        "  Built but not listed: $($unlisted -join ', ')")
}

# --- Package ----------------------------------------------------------------
New-Item -ItemType Directory $outDir -Force | Out-Null
$package = Join-Path $outDir "CalculusLink_$packageVersion.tsep"
& $builder -i $manifest -o $package -v $packageVersion
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $package)) { throw 'TeklaExtensionPackage.BatchBuilder failed' }

Write-Host "Built $package with $(Split-Path $builder -Parent)"
