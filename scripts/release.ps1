# Локальная сборка self-contained .exe и упаковка в zip.
# Не делает push, не создаёт GH Release — только собирает и пакует.
# Удобно когда нет доступа к GitHub Actions или надо проверить релизный
# артефакт перед пушем тега.
#
# Использование:
#   .\scripts\release.ps1                   # версию читает из RfmDownloader.csproj
#   .\scripts\release.ps1 -Version 1.0.0    # явная версия
#
# Результат:
#   publish\RfmDownloader.exe
#   RfmDownloader-<version>-win-x64.zip   (в корне репозитория)

param(
    [string]$Version = ''
)

$ErrorActionPreference = 'Stop'

# Путь до корня репо: скрипт лежит в scripts\, поднимаемся на уровень выше
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

# Если версия не передана — берём из csproj
if ([string]::IsNullOrWhiteSpace($Version)) {
    $csprojXml = [xml](Get-Content RfmDownloader.csproj -Raw)
    $Version = $csprojXml.Project.PropertyGroup.Version
    if ([string]::IsNullOrWhiteSpace($Version)) {
        throw "Не удалось прочитать <Version> из RfmDownloader.csproj — укажите параметр -Version"
    }
    Write-Host "Версия из csproj: $Version"
} else {
    Write-Host "Версия из параметра: $Version"
}

# Чистим прошлый publish, чтобы не унести лишнее в zip
if (Test-Path publish) { Remove-Item -Recurse -Force publish }

Write-Host "Собираю self-contained .exe..."
# Явно указываем csproj — без этого dotnet publish видит solution
# и пытается паблишить тестовый проект, который не Exe и не поддерживает PublishSingleFile.
dotnet publish RfmDownloader.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version `
    -o publish | Out-Host

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish завершился с кодом $LASTEXITCODE"
}

$exe = Join-Path 'publish' 'RfmDownloader.exe'
if (-not (Test-Path $exe)) {
    throw "Не нашёл $exe после publish"
}

$zip = "RfmDownloader-$Version-win-x64.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }

Write-Host "Пакую в $zip..."
$files = @(
    $exe,
    'README.md',
    'LICENSE',
    'config.ini.example'
)
Compress-Archive -Path $files -DestinationPath $zip -Force

$size = (Get-Item $zip).Length
Write-Host ""
Write-Host "Готово:"
Write-Host "  $exe                ($((Get-Item $exe).Length) байт)"
Write-Host "  $zip   ($size байт)"
Write-Host ""
Write-Host "Чтобы запушить релиз через GitHub Actions:"
Write-Host "  git tag v$Version"
Write-Host "  git push origin v$Version"
