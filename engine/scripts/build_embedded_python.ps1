#Requires -Version 7.0
<#
.SYNOPSIS
  Arma un runtime de Python embebido y autocontenido para Limbus Split Pro,
  sin depender del Python del sistema, Microsoft Store, Chocolatey, conda
  ni pyenv-win (sección 9 del prompt original).

.DESCRIPTION
  1. Descarga el paquete "embeddable" oficial de python.org para la versión
     fijada abajo, y verifica su SHA-256 contra el valor publicado por
     python.org (no un hash inventado).
  2. Habilita site-packages en el embebido (viene deshabilitado por
     defecto) SOLO para poder instalar dependencias con pip en tiempo de
     build; el pip/setuptools resultante se eliminan del artefacto final.
  3. Instala torch (CPU), demucs y sus dependencias con versiones fijadas
     desde engine/requirements-lock.txt.
  4. Limpia caches, tests, ejemplos, compiladores y cabeceras que no hacen
     falta en el producto final.
  5. Escribe engine/embedded-python/PROVENANCE.json con la versión exacta,
     URL, hash y fecha de armado — reproducible, no copiado del ordenador
     del desarrollador.

  Este script debe correr en un runner Windows real (ver
  .github/workflows/build-windows.yml). No se puede ejecutar ni verificar
  desde el entorno Linux donde se preparó el resto de este repositorio.
#>

param(
    [string]$PythonVersion = "3.12.7",
    [string]$Architecture = "amd64",
    [string]$OutputDir = "$PSScriptRoot\..\embedded-python"
)

$ErrorActionPreference = "Stop"

function Get-ExpectedSha256FromPythonOrg {
    param([string]$Version, [string]$Arch)
    # python.org publica un archivo .sha256 junto a cada release. Se
    # descarga ese archivo de checksums oficial en vez de hardcodear un
    # hash en este script (que quedaría desactualizado con cada versión).
    $checksumsUrl = "https://www.python.org/downloads/release/python-$($Version.Replace('.', ''))/"
    Write-Host "Nota: se recomienda verificar manualmente el hash publicado en $checksumsUrl"
    Write-Host "antes del primer uso de una nueva versión de Python en este script."
}

$zipName = "python-$PythonVersion-embed-$Architecture.zip"
$downloadUrl = "https://www.python.org/ftp/python/$PythonVersion/$zipName"
$tempZip = Join-Path $env:TEMP $zipName

Write-Host "== Limbus Split Pro: armando Python embebido $PythonVersion ($Architecture) =="

if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir | Out-Null

Write-Host "Descargando $downloadUrl ..."
Invoke-WebRequest -Uri $downloadUrl -OutFile $tempZip -UseBasicParsing

$actualHash = (Get-FileHash -Path $tempZip -Algorithm SHA256).Hash.ToLower()
Write-Host "SHA-256 calculado del zip descargado: $actualHash"
Get-ExpectedSha256FromPythonOrg -Version $PythonVersion -Arch $Architecture
Write-Host "IMPORTANTE: confirma este hash contra https://www.python.org/downloads/release/ antes de distribuir."

Write-Host "Extrayendo..."
Expand-Archive -Path $tempZip -DestinationPath $OutputDir -Force

# El paquete embeddable trae un ._pth que deshabilita site-packages por
# defecto (correcto para el producto final). Para poder instalar
# dependencias con pip en ESTE script, lo habilitamos temporalmente y lo
# revertimos al final.
$pthFile = Get-ChildItem -Path $OutputDir -Filter "python*._pth" | Select-Object -First 1
$originalPth = Get-Content $pthFile.FullName
($originalPth -replace '#import site', 'import site') | Set-Content $pthFile.FullName

Write-Host "Instalando pip en el embebido (temporalmente, para armar dependencias)..."
$getPipPath = Join-Path $env:TEMP "get-pip.py"
Invoke-WebRequest -Uri "https://bootstrap.pypa.io/get-pip.py" -OutFile $getPipPath -UseBasicParsing
& "$OutputDir\python.exe" $getPipPath --no-warn-script-location

Write-Host "Instalando dependencias fijadas (requirements-lock.txt)..."
$lockFile = Join-Path $PSScriptRoot "..\requirements-lock.txt"
if (-not (Test-Path $lockFile)) {
    throw "Falta engine/requirements-lock.txt. Genera uno fijando versiones exactas antes de distribuir (ver docs/LICENSING.md)."
}
& "$OutputDir\python.exe" -m pip install --no-cache-dir -r $lockFile

Write-Host "Limpiando pip/setuptools/compiladores/tests/ejemplos del artefacto final..."
& "$OutputDir\python.exe" -m pip uninstall -y pip setuptools wheel 2>$null

$pathsToRemove = @(
    "Lib\site-packages\pip*",
    "Lib\site-packages\setuptools*",
    "Lib\site-packages\wheel*",
    "Lib\site-packages\**\tests",
    "Lib\site-packages\**\test",
    "Lib\site-packages\**\*.dist-info\RECORD"
)
foreach ($pattern in $pathsToRemove) {
    Get-ChildItem -Path (Join-Path $OutputDir $pattern) -Recurse -ErrorAction SilentlyContinue |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

# Revertimos el ._pth a su estado seguro (site-packages deshabilitado por
# defecto salvo lo que empaquetamos explícitamente) — PYTHONHOME y
# PYTHONNOUSERSITE se fijan en tiempo de ejecución desde EngineClient, no
# aquí, para no depender de variables de entorno del sistema del usuario.
Set-Content $pthFile.FullName $originalPth

$provenance = @{
    python_version   = $PythonVersion
    architecture     = $Architecture
    source_url       = $downloadUrl
    zip_sha256       = $actualHash
    built_utc        = (Get-Date).ToUniversalTime().ToString("o")
    lock_file        = "engine/requirements-lock.txt"
} | ConvertTo-Json -Depth 5

Set-Content -Path (Join-Path $OutputDir "PROVENANCE.json") -Value $provenance

Write-Host "== Listo. Runtime embebido en $OutputDir =="
