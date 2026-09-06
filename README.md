# Limbus Split Pro (Windows Edition)

Aplicación nativa de Windows (WPF, .NET 10 LTS) para separar una mezcla musical
en pistas independientes (voces, batería, bajo, y opcionalmente guitarra/piano)
usando modelos de separación de fuentes que corren **100% en local**, sin subir
audio a ningún servidor.

> Este repositorio se generó siguiendo el prompt maestro "Limbus Split Pro –
> Windows Edition" (docs/PROMPT_ORIGEN.md), adaptado a las condiciones reales
> encontradas durante la investigación de modelos y licencias
> (ver `docs/MODELS.md` y `docs/LICENSING.md`).

## Estado de este primer entregable

Este código se escribió y organizó **fuera de Windows** (entorno Linux sin GPU
ni acceso a un equipo Windows). Eso significa, siguiendo la regla de
transparencia del propio prompt:

- ✅ Arquitectura, código C# y Python completos y listos para compilar.
- ✅ Pipeline de CI (`.github/workflows/build-windows.yml`) que compila,
  prueba y empaqueta **en un runner Windows real** de GitHub Actions.
- ✅ Investigación de modelos y licencias documentada con fuentes.
- ❌ **Nada de esto se ha compilado, ejecutado ni probado todavía en Windows
  real.** Ese paso ocurre quld corras la pipeline (ver más abajo) o cuando
  compiles localmente en tu PC con Windows.

No trates el instalador como "listo para distribuir a terceros" hasta que la
lista de `docs/PENDING_TESTS.md` esté en verde.

## Cómo obtener tu instalador (.msi) usando GitHub Actions

1. Crea un repositorio nuevo y **vacío** en GitHub (sin README, sin licencia,
   sin .gitignore — para que no choque con estos archivos).
2. En tu máquina (o en este mismo entorno si tienes git configurado), dentro
   de esta carpeta:

   ```bash
   git init
   git add .
   git commit -m "Limbus Split Pro: primer scaffold completo"
   git branch -M main
   git remote add origin https://github.com/<tu-usuario>/<tu-repo>.git
   git push -u origin main
   ```

3. Entra a la pestaña **Actions** de tu repositorio en GitHub. El workflow
   "Build Windows" se dispara automáticamente con el push y corre en un
   runner `windows-latest` real de Microsoft.
4. Cuando termine (varios minutos, porque compila un runtime de Python
   embebido con PyTorch), abre la ejecución y descarga el artefacto
   `LimbusSplitPro-installer` desde la sección **Artifacts**. Ahí estará:
   - `LimbusSplitPro-Setup-x64.msi`
   - `checksums.sha256`
   - `SBOM.cdx.json`
   - `THIRD_PARTY_NOTICES.txt`
   - `test-report.md` (con lo que la pipeline sí pudo verificar automáticamente)
5. Instala el `.msi` en tu PC. La primera vez que elijas separar una canción,
   la app te pedirá permiso para descargar el modelo elegido (unos cientos de
   MB) directamente desde el repositorio oficial de Meta/Demucs, verificando
   su hash SHA-256 antes de usarlo. Esto es intencional: así el instalador no
   redistribuye pesos de terceros (ver `docs/MODELS.md`).

## Estructura del repositorio

```
src/LimbusSplitPro.App/      Interfaz WPF (MVVM)
src/LimbusSplitPro.Core/     Lógica: mezclador, IPC con el motor, manifiesto de modelos
tests/LimbusSplitPro.Core.Tests/  Pruebas unitarias (xUnit)
engine/limbus_engine/        Motor de separación en Python (demucs)
engine/scripts/              Scripts para armar el runtime Python embebido
installer/                   Proyecto WiX (MSI)
models/                      Manifiesto de modelos + esquema de verificación
docs/                        Arquitectura, licencias, pruebas pendientes, informe
.github/workflows/           Pipeline de CI en Windows
```

## Requisitos para compilar localmente en tu PC con Windows

- Windows 10 o 11, x64.
- .NET 10 SDK (LTS, soporte hasta noviembre de 2028).
- WiX Toolset **v5.x** vía `dotnet tool install --global wix --version 5.*`
  (deliberadamente NO v6/v7: desde WiX v6 existe un "Open Source Maintenance
  Fee" para uso que genere ingresos; v5 no lo tiene — ver `docs/LICENSING.md`).
- PowerShell 7+.

```powershell
git clone <tu-repo>
cd LimbusSplitPro
./engine/scripts/build_embedded_python.ps1
dotnet restore
dotnet build -c Release
dotnet test -c Release

# Publicar la app y copiar el motor embebido junto a ella (los mismos
# pasos que hace .github/workflows/build-windows.yml):
dotnet publish src/LimbusSplitPro.App/LimbusSplitPro.App.csproj -c Release -r win-x64 --self-contained true -o publish/
New-Item -ItemType Directory -Force -Path publish/engine/python, publish/engine/limbus_engine, publish/models | Out-Null
Copy-Item -Recurse -Force engine/embedded-python/* publish/engine/python/
Copy-Item -Recurse -Force engine/limbus_engine/* publish/engine/limbus_engine/
Copy-Item -Force models/manifest.json publish/models/manifest.json

# Empaquetar el instalador (requiere hashes ya pineados en models/manifest.json,
# ver engine/scripts/pin_model_hashes.py):
$publishDirFull = (Resolve-Path "publish").Path + "\"
dotnet build installer/LimbusSplitPro.Installer.wixproj -c Release -p:PublishDir=$publishDirFull -o artifacts/installer
```

## Licencias y modelos

Ver `docs/LICENSING.md` y `docs/MODELS.md`. Resumen: el código de este
repositorio es MIT. El motor usa Demucs (código MIT, Meta/adefossez) mediante
descarga bajo demanda de los pesos oficiales — no se redistribuyen dentro del
instalador porque el estatus exacto de licencia de los pesos entrenados con
MUSDB18 no está declarado de forma inequívoca por el autor para
redistribución comercial de terceros (hay hilos públicos contradictorios al
respecto). Para tu uso personal en tu propio equipo esto no es un problema:
es el mismo flujo que usa la CLI oficial de Demucs.
