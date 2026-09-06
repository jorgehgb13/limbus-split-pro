# Informe de entrega — v1 (scaffold completo, sin compilar aún en Windows)

Fecha: 2026-09-06. Preparado fuera de Windows (entorno Linux sin GPU). Este
informe sigue la estructura pedida en la sección 25 del prompt original.

## Funciones implementadas

- Selección de archivo de entrada con metadatos (duración, formato, Hz, canales).
- Selector de categorías con casillas deshabilitadas + motivo cuando no hay
  modelo disponible (nunca oculto silenciosamente).
- Elección de carpeta de trabajo/exportación explícita (FolderPicker).
- Preferencia de dispositivo (Automático / CPU / GPU) con fallback seguro.
- Comunicación C# ↔ Python por JSON Lines, sin cmd.exe, sin concatenación
  insegura de rutas.
- Lógica de complemento "Other" con detección real de silencio (no genera
  archivos vacíos falsos).
- Mezclador multipista WASAPI: mute/solo/volumen con rampas, seek sin
  clics, un solo reloj compartido, Stop con semántica distinta de Pause,
  atajo de espacio que respeta foco de texto y diálogos modales.
- Exportación de mezcla offline, sample-accurate, con protección de
  clipping documentada (nunca normaliza en silencio).
- Verificador de modelos fail-closed (hash SHA-256, tamaño, capacidades,
  autorización de redistribución/uso comercial) implementado en paralelo en
  C# y Python.
- Tema claro/oscuro siguiendo la preferencia de Windows, barra de título
  personalizada sin solapamientos, DPI Per-Monitor V2, sin privilegios de
  administrador (instalación per-user).
- Pipeline de CI completa en `windows-latest`: build, tests, pin de hashes
  de modelos contra la fuente oficial, runtime de Python embebido
  reproducible, SBOM, instalador MSI (WiX v5), firma condicional.

## Modelos utilizados

`htdemucs` (4 fuentes) y `htdemucs_6s` (6 fuentes, agrega guitarra/piano).
Ver `docs/MODELS.md` para el detalle completo, incluyendo la disputa de
licencia de los pesos que llevó a la decisión de descarga bajo demanda en
vez de redistribución empaquetada.

## Licencias verificadas

Ver `docs/LICENSING.md`: .NET 10 LTS, Demucs (código MIT / pesos con
estatus "unclear_research_only_flagged" registrado explícitamente en
`models/manifest.json`), NAudio (MIT), Python embeddable (PSF), WiX
Toolset **v5.0.2** elegido específicamente para evitar el "Open Source
Maintenance Fee" de v6/v7.

## Dependencias eliminadas / evitadas

- No se usa Electron, WinUI 3 sin justificar, ni ninguna dependencia de
  Python del sistema, Chocolatey, conda o pyenv-win.
- El script de armado del runtime embebido desinstala pip/setuptools/wheel
  del artefacto final una vez usados para instalar dependencias.

## Pruebas realizadas HASTA AHORA (fuera de Windows)

Verificado de verdad en este entorno (no es una afirmación sin evidencia):

- 10 pruebas unitarias de Python (`engine/tests/`), todas en verde:
  detección de silencio real vs. falso, complemento Other, verificador de
  manifiesto fail-closed (registro, archivo faltante, hash alterado).
- `models/manifest.json` valida contra `models/manifest.schema.json`.
- Los 5 módulos del motor Python (`protocol.py`, `device.py`,
  `manifest_verify.py`, `separation.py`, `main.py`) compilan sin errores de
  sintaxis (`py_compile`).
- El YAML de la pipeline de CI parsea correctamente.

Lo que **no** se ha podido probar aquí (requiere Windows real, ver
`docs/PENDING_TESTS.md` para la lista completa con numeración exacta del
prompt original): compilación de los proyectos C#/WPF, WASAPI real,
instalador MSI real, firma Authenticode, SmartScreen, calidad de
separación con audio real, y todo lo que dependa de una GPU concreta.

## Equipos y versiones de Windows probados

Ninguno todavía. Se probará automáticamente en el runner `windows-latest`
de GitHub Actions (Windows Server más reciente) en cuanto hagas push, y
manualmente en tu PC al instalar el `.msi` resultante.

## Resultados CPU y GPU

Pendientes — el runner de GitHub Actions no tiene GPU, así que la ruta CPU
se valida ahí automáticamente (paso "Smoke test embedded runtime"); la ruta
GPU solo se puede probar en tu propio equipo si tienes una NVIDIA
compatible.

## Errores encontrados y corregidos durante esta preparación

- Se descartó inicialmente confiar en la etiqueta "license: mit" de varios
  mirrors de Hugging Face para los pesos de Demucs al encontrar un hilo que
  cuestionaba esa etiqueta citando al propio autor; se decidió no
  redistribuir pesos como mitigación (ver docs/MODELS.md).
- Se identificó que WiX v6/v7 introdujeron una cuota de mantenimiento
  ("Open Source Maintenance Fee"); se fijó el proyecto a WiX v5 antes de
  escribir el `.wixproj`, no después.
- Se eliminó un parámetro sin usar (`models_root`) en
  `ensure_model_ready()` tras revisar el código.

## Limitaciones pendientes

Ver `docs/MODELS.md` (categorías deshabilitadas) y `docs/PENDING_TESTS.md`
(lista exhaustiva de pruebas que solo se pueden hacer en Windows real).
Además, la cancelación cooperativa del motor no interrumpe una llamada de
`apply_model` ya en curso (documentado en `separation.py`).

## Estado comercial

No aplica todavía — build de desarrollo para uso personal. El manifiesto de
modelos tiene `redistribution_authorized: false` y
`commercial_use_authorized: false` para ambos modelos, así que el
verificador bloquearía automáticamente cualquier intento de marcar esto
como "listo para vender" hasta resolver la licencia de los pesos o
sustituirlos.

## Estado de firma

Sin firmar. `docs/PENDING_TESTS.md` explica cómo se activaría la firma si
en el futuro configuras un certificado Authenticode como secreto de GitHub.

## Estado de SmartScreen

No evaluado — depende de que exista, primero, un binario firmado real.

## Hashes de instaladores

Se generan automáticamente en `artifacts/checksums.sha256` en cada corrida
de CI; no existen todavía porque no ha corrido en Windows real.
