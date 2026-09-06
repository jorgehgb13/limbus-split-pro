"""
Protocolo JSON Lines entre LimbusSplitPro.App (C#) y este motor.
Debe mantenerse en sincronía con src/LimbusSplitPro.Core/Engine/EngineProtocol.cs.

Regla dura: stdout se usa EXCLUSIVAMENTE para líneas JSON de este protocolo.
Cualquier log técnico (progreso interno de torch, warnings, etc.) va a
stderr. Nunca se debe hacer `print()` suelto en ningún otro módulo de este
paquete: todo pasa por `emit_*` de aquí.
"""
from __future__ import annotations

import json
import sys
import threading
from typing import Any, Optional

_stdout_lock = threading.Lock()


def _write_json_line(payload: dict[str, Any]) -> None:
    line = json.dumps(payload, ensure_ascii=False)
    with _stdout_lock:
        sys.stdout.write(line + "\n")
        sys.stdout.flush()


def emit_stage(stage: str) -> None:
    _write_json_line({"type": "stage", "stage": stage})


def emit_progress(pct: float) -> None:
    _write_json_line({"type": "progress", "pct": pct})


def emit_error(code: str, message: str) -> None:
    _write_json_line({"type": "error", "code": code, "message": message})


def emit_done(stems: list[dict[str, Any]]) -> None:
    _write_json_line({"type": "done", "stems": stems})


def log(message: str) -> None:
    """Log técnico: va a stderr, nunca a stdout."""
    sys.stderr.write(message + "\n")
    sys.stderr.flush()


def read_commands():
    """Generador que produce un dict por cada línea JSON válida de stdin.
    Una línea que no parsea se loguea a stderr y se ignora (no se cae el
    proceso por un mensaje malformado)."""
    for raw_line in sys.stdin:
        raw_line = raw_line.strip()
        if not raw_line:
            continue
        try:
            yield json.loads(raw_line)
        except json.JSONDecodeError as exc:
            log(f"[protocol] línea de stdin no es JSON válido, ignorada: {exc}")


# Códigos de error — deben calzar exactamente con EngineErrorCode en C#.
class ErrorCode:
    MODEL_MISSING = "MODEL_MISSING"
    HASH_MISMATCH = "HASH_MISMATCH"
    UNSUPPORTED_FORMAT = "UNSUPPORTED_FORMAT"
    OUT_OF_MEMORY = "OUT_OF_MEMORY"
    GPU_INCOMPATIBLE = "GPU_INCOMPATIBLE"
    PERMISSION_DENIED = "PERMISSION_DENIED"
    FILE_LOCKED = "FILE_LOCKED"
    NETWORK_PATH_UNAVAILABLE = "NETWORK_PATH_UNAVAILABLE"
    AUDIO_DEVICE_UNAVAILABLE = "AUDIO_DEVICE_UNAVAILABLE"
    CANCELLED_BY_USER = "CANCELLED_BY_USER"
    UNKNOWN = "UNKNOWN"
