# Decisiones de licenciamiento de herramientas y dependencias

## WiX Toolset: se fija la versión 5.x, deliberadamente NO 6 o 7

Investigado en la documentación oficial de FireGiant (`docs.firegiant.com/wix/osmf/`)
y en la propia página de GitHub de wixtoolset:

- Desde **WiX v6** (abril 2025) el proyecto introdujo un "Open Source
  Maintenance Fee" (OSMF): si usas el toolset y tu organización genera más de
  10.000 USD/año, se requiere pagar un patrocinio (GitHub Sponsors) y aceptar
  una EULA.
- **WiX v7** (abril 2026) mantiene el mismo esquema (con el umbral de 10.000
  USD/año) pero además **obliga a aceptar la EULA explícitamente** en scripts
  de build/CI, o el comando falla con `WIX7015`.
- **WiX v5** es anterior a la introducción del OSMF: su código sigue bajo la
  licencia de código abierto original, sin fee ni EULA adicional,
  independientemente de los ingresos del proyecto que lo use.

Como no sabemos si "Limbus Split Pro" se quedará en uso personal o se
convertirá en un producto comercial ("Pro" está en el nombre), se fija la
pipeline y las instrucciones locales a:

```powershell
dotnet tool install --global wix --version 5.*
wix extension add --global WixToolset.UI.wixext/5.*
wix extension add --global WixToolset.Util.wixext/5.*
```

Si en el futuro decides que el proyecto genera más de 10.000 USD/año y
quieres funciones nuevas de WiX 6/7, esa es una decisión de negocio a tomar
conscientemente, no un fee al que se entra "por accidente" al actualizar una
dependencia.

## .NET 10 (LTS)

Confirmado en `learn.microsoft.com` / `dotnet/core` (releases.md): .NET 10 se
publicó el 11 de noviembre de 2025 como LTS, con soporte hasta el 14 de
noviembre de 2028. .NET 8 (la LTS anterior) termina soporte el 10 de
noviembre de 2026 — está a semanas de expirar en el momento de escribir esto,
así que no tendría sentido fijar el proyecto a esa versión.

## Demucs (código)

MIT, confirmado en el LICENSE del repositorio oficial `facebookresearch/demucs`
(archivado, continuado en `adefossez/demucs`). Ver `docs/MODELS.md` para la
discusión completa sobre los pesos entrenados.

## NAudio

MIT. Se usa para WASAPI (reproducción) y para exportación de WAV con
precisión de sample. No se usa ningún códec propietario a través de NAudio
en este v1 (solo WAV/PCM); si más adelante se agrega MP3/FLAC/AAC, hay que
repetir este mismo ejercicio de verificación antes de fijar la librería
(sección 12 del prompt original sobre LGPL/GPL en FFmpeg).

## Embedded Python

Se usa el paquete "embeddable" oficial distribuido por python.org
(`python-<version>-embed-amd64.zip`), bajo la licencia PSF (permisiva,
compatible con distribución comercial). El script
`engine/scripts/build_embedded_python.ps1` fija la versión exacta, descarga
desde `python.org` (dominio oficial) y verifica el hash SHA-256 publicado por
python.org antes de continuar — ese hash se registra en
`engine/embedded-python/PROVENANCE.json` generado por el propio script en
tiempo de build (no se hardcodea aquí un hash adivinado).

## PyTorch / ONNX Runtime

Ambos BSD-style (PyTorch: modified BSD; ONNX Runtime: MIT), sin restricciones
de uso comercial. Se fija la versión exacta en
`engine/requirements-lock.txt`, generado por la pipeline de CI la primera vez
que corre en Windows (no se puede resolver `pip` contra el índice completo de
PyPI para paquetes con ruedas específicas de CUDA/Windows desde este entorno
de preparación, que no tiene Windows ni GPU).
