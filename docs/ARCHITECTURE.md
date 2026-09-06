# Arquitectura

## Decisión de plataforma

- **UI**: WPF sobre **.NET 10** (LTS actual, GA nov-2025, soporte hasta
  nov-2028; confirmado por `dotnet/core/releases.md`). Se descartó WinUI 3
  porque el prompt exige verificar que el empaquetado y la reproducción
  multipista sean igual de fiables antes de usarlo, y WPF + NAudio es la
  combinación con más historial probado en apps de audio de escritorio.
- **Motor de separación**: proceso Python separado (no embebido en el
  proceso .NET), comunicándose por **stdin/stdout con JSON Lines** y logs
  técnicos por stderr. Esto aísla cualquier fallo del motor (memoria, CUDA,
  etc.) del proceso de UI.
- **Empaquetado**: MSI vía **WiX Toolset v5.x** (ver `docs/LICENSING.md` para
  por qué v5 y no v6/v7).

## Diagrama de procesos

```
┌─────────────────────────────┐        JSON Lines (stdin/stdout)      ┌──────────────────────────┐
│  LimbusSplitPro.App (WPF)   │ ─────────────────────────────────────▶│ engine/limbus_engine      │
│  - MainViewModel            │◀───────────────────────────────────── │ (python, proceso hijo)    │
│  - MixerViewModel           │        eventos: progress/stage/error   │  - carga modelo (torch)   │
│  - AudioMixerEngine (NAudio)│                                         │  - separa por segmentos   │
│  - ModelManifestVerifier    │                                         │  - escribe stems .wav     │
└─────────────────────────────┘                                         └──────────────────────────┘
```

## Por qué JSON Lines y no otro protocolo

- Cero dependencias nuevas (no requiere gRPC ni named pipes de terceros).
- Cada línea es un evento independiente: `{"type":"progress","stage":"loading_model","pct":12}`,
  `{"type":"stage","name":"separating"}`, `{"type":"error","code":"OUT_OF_MEMORY","message":"..."}`,
  `{"type":"done","stems":[{"name":"vocals","path":"...","sha256":"..."}]}`.
- Fácil de loguear íntegro para diagnóstico sin parsear binario.
- `ProcessStartInfo.ArgumentList` se usa para lanzar Python — nunca se
  concatenan rutas del usuario en una cadena de comandos (evita inyección de
  argumentos con rutas que contienen espacios o comillas).

## Lógica de "Other" (complemento)

Implementada en `SeparationPlanner` (`src/LimbusSplitPro.Core/Models/SeparationPlanner.cs`)
y ejecutada en `engine/limbus_engine/separation.py`:

1. El usuario elige categorías (ej. "Voces").
2. El motor siempre calcula **todas** las fuentes nativas del modelo elegido
   (p. ej. htdemucs da vocals/drums/bass/other).
3. "Other" que se entrega al usuario = suma de las fuentes nativas **no
   seleccionadas** + el residual "other" propio del modelo (todo lo que el
   modelo no pudo asignar a ninguna fuente). Nunca es un archivo generado
   artificialmente ni silencioso: si la suma da silencio real (p. ej. el
   usuario seleccionó literalmente todo), no se escribe el archivo.
4. Antes de exportar, se verifica que `sum(stems_entregados) ≈ mezcla original`
   dentro de una tolerancia (comprobación de reconstrucción, sección 23.19
   del prompt original).

## Qué categorías del prompt original SÍ se implementan en este v1 y cuáles no

Ver `docs/MODELS.md` para el detalle y la razón exacta de cada categoría no
disponible (ninguna se simula con archivos vacíos, siguiendo la regla
explícita del prompt).

## Rutas y datos mutables

Siguiendo la sección 19 del prompt:

- Instalación (solo lectura): `%LOCALAPPDATA%\Programs\Limbus Split Pro\`
  (instalación por usuario, sin requerir admin — ver `docs/LICENSING.md` sobre
  por qué se eligió instalación per-user).
- Modelos descargados: `%LOCALAPPDATA%\Limbus Split Pro\Models\`
- Caché: `%LOCALAPPDATA%\Limbus Split Pro\Cache\`
- Logs: `%LOCALAPPDATA%\Limbus Split Pro\Logs\` (rotativos)
- Temporales de proceso: `%TEMP%\LimbusSplitPro\<guid>\`, limpiados tras éxito.
- Exportaciones: carpeta elegida explícitamente por el usuario vía
  `FolderPicker`, nunca una ubicación por defecto oculta.
