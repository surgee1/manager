param([string]$Destination = '', [string]$Version = '6.2.11', [switch]$IncludeSource)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repoRoot = Split-Path $PSScriptRoot -Parent
$profileSource = [IO.File]::ReadAllText((Join-Path $repoRoot 'RBX Alt Manager\Forms\ThemeEditor.cs'))
$catalog = [regex]::Match($profileSource, 'SupportedProfiles\s*=\s*\{([^}]+)\}').Groups[1].Value
if (!$catalog) { throw 'Visual profile catalog not found.' }
foreach ($profile in [regex]::Matches($catalog, '"([a-z0-9_]+)"')) {
  $id = $profile.Groups[1].Value
  if ($id -ne 'nyan' -and !(Test-Path -LiteralPath (Join-Path $repoRoot ('Themes\' + $id + '.png')))) {
    throw ('Missing visual profile asset: ' + $id)
  }
}
if (!$Destination) { $Destination = Join-Path $repoRoot 'build\artifacts' }
$null = New-Item -ItemType Directory -Path $Destination -Force
$Destination = (Resolve-Path $Destination).Path
$stage = Join-Path $repoRoot ('build\package-' + [guid]::NewGuid().ToString('N'))
$app = Join-Path $stage 'Spectrum-Manager'
$null = New-Item -ItemType Directory -Path $app -Force
$binary = Join-Path $repoRoot 'build\Debug'
$rootFiles = @(Get-ChildItem $binary -File | Where-Object { $_.Extension -eq '.dll' -or $_.Name -in @('Roblox Account Manager.exe', 'Roblox Account Manager.exe.config', 'SpectrumUpdater.exe', 'log4.config') })
foreach ($file in $rootFiles) { Copy-Item -LiteralPath $file.FullName -Destination $app }
if (!(Test-Path (Join-Path $app 'SpectrumUpdater.exe'))) { throw 'Updater has not been built.' }
# Old managers only accept the legacy helper name in the update plan. On first
# startup the updated app atomically promotes it to SpectrumUpdater.exe and removes it.
Copy-Item -LiteralPath (Join-Path $app 'SpectrumUpdater.exe') -Destination (Join-Path $app 'SurgeeUpdater.exe')
Copy-Item -LiteralPath (Join-Path $repoRoot 'Themes') -Destination (Join-Path $app 'Themes') -Recurse
Copy-Item -LiteralPath (Join-Path $repoRoot 'Browser Extension') -Destination (Join-Path $app 'Browser Extension') -Recurse
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $app
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $app
$manifestFiles = [ordered]@{}
Get-ChildItem $app -File -Recurse | Where-Object { $_.Name -notin @('LICENSE', 'README.md', 'SpectrumUpdater.exe') } | Sort-Object FullName | ForEach-Object {
  $relative = $_.FullName.Substring($app.Length + 1).Replace('\', '/')
  $manifestFiles[$relative] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
$manifest = [ordered]@{ kind = 'surgee-manager'; version = $Version; files = $manifestFiles }
[IO.File]::WriteAllText((Join-Path $app 'surgee-release.json'), ($manifest | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
$appArchive = Join-Path $Destination ('Spectrum-Manager-v' + $Version + '.zip')
if (Test-Path $appArchive) { throw 'Artifact already exists: use a new version or output directory.' }
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $appArchive)
$extensionArchive = Join-Path $Destination 'Spectrum-Browser-Extension.zip'
if (Test-Path $extensionArchive) { throw 'Extension artifact already exists.' }
[IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $repoRoot 'Browser Extension'), $extensionArchive)

if ($IncludeSource) {
$sourceRoot = Join-Path $repoRoot ('build\source-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $sourceRoot -Force
$allowedRoots = @('RBX Alt Manager', 'Browser Extension', 'Themes', 'UpdateHelper', 'Tests', 'scripts', '.github')
foreach ($name in $allowedRoots) {
  $source = Join-Path $repoRoot $name
  if (!(Test-Path $source)) { continue }
  Get-ChildItem -LiteralPath $source -File -Recurse -Force | Where-Object {
    $_.FullName -notmatch '[\\/](obj|bin|build|packages|Backups|\.vs)[\\/]' -and
    $_.Name -notmatch '(?i)^(AccountData|RAMSettings|RAMTheme|RecentGames|FavoritePlaces|AccountFolders)\.' -and
    $_.Name -notlike 'LaunchProfiles.json*' -and
    $_.Name -notlike '*.surgee-backup.json*' -and
    $_.Extension -notin @('.user', '.pdb', '.log', '.cache')
  } | ForEach-Object {
    $relative = $_.FullName.Substring($repoRoot.Length + 1)
    $target = Join-Path $sourceRoot $relative
    $null = New-Item -ItemType Directory -Path (Split-Path $target) -Force
    Copy-Item -LiteralPath $_.FullName -Destination $target
  }
}
foreach ($name in @('LICENSE', 'README.md', '.gitignore', 'RBX Alt Manager.sln', 'RAMAccount.lua', 'Launcher')) {
  $source = Join-Path $repoRoot $name
  if (Test-Path -LiteralPath $source -PathType Leaf) { Copy-Item -LiteralPath $source -Destination $sourceRoot }
}
$sourceArchive = Join-Path $Destination ('Spectrum-Source-v' + $Version + '.zip')
if (Test-Path $sourceArchive) { throw 'Source artifact already exists.' }
[IO.Compression.ZipFile]::CreateFromDirectory($sourceRoot, $sourceArchive)
}
Write-Output $Destination
