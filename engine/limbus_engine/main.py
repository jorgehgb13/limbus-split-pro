"""
Punto de entrada del motor. Se lanza como proceso hijo desde
LimbusSplitPro.Core/Engine/EngineClient.cs vía ProcessStartInfo.ArgumentList
(nunca cmd.exe, nunca concatenación de texto del usuario).

Bucle: lee comandos JSON Lines de stdin; "separate" corre en un hilo aparte
para poder seguir escuchando "cancel" mientras tanto.
"""
from __future__ import annotations

import sys
import threading
from pathlib import Path

from limbus_engine import protocol
from limbus_engine.device import decide_device
from limbus_engine.manifest_verify import ModelVerificationError
from limbus_engine.separation import SeparationCancelled, ensure_model_ready, run_separation


def _handle_separate(command: dict, cancel_event: threading.Event) -> None:
    try:
        input_path = Path(command["input_path"])
        output_dir = Path(command["output_dir"])
        model_id = command["model_id"]
        model_path = Path(command["model_path"])
        native_sources = command.get("native_sources_to_export", [])
        fold_into_other = command.get("fold_into_other", [])
        device_pref = command.get("device_preference", "auto")

        if not input_path.exists():
            protocol.emit_error(protocol.ErrorCode.UNSUPPORTED_FORMAT, f"No existe el archivo '{input_path}'.")
            return

        manifest_path = Path(__file__).resolve().parent.parent.parent / "models" / "manifest.json"

        protocol.emit_stage("checking_memory")
        # Estimación previa gruesa; se refina tras leer el audio real dentro
        # de ensure_model_ready/run_separation si hiciera falta.
        decision = decide_device(device_pref, duration_seconds=600, channels=2, sample_rate=44100)
        protocol.log(f"Dispositivo elegido: {decision.device} ({decision.reason})")

        model = ensure_model_ready(model_id, manifest_path, model_path)

        if cancel_event.is_set():
            protocol.emit_error(protocol.ErrorCode.CANCELLED_BY_USER, "Cancelado por el usuario.")
            return

        stems = run_separation(
            input_path=input_path,
            output_dir=output_dir,
            model=model,
            device=decision.device,
            native_sources_to_export=native_sources,
            fold_into_other=fold_into_other,
            cancel_event=cancel_event,
            progress_cb=protocol.emit_progress,
        )
        protocol.emit_done(stems)

    except SeparationCancelled:
        protocol.emit_error(protocol.ErrorCode.CANCELLED_BY_USER, "Cancelado por el usuario.")
    except ModelVerificationError as exc:
        protocol.emit_error(exc.code, exc.message)
    except MemoryError:
        protocol.emit_error(
            protocol.ErrorCode.OUT_OF_MEMORY,
            "No hay memoria suficiente para procesar este archivo. Prueba con CPU o un archivo más corto.",
        )
    except PermissionError as exc:
        protocol.emit_error(protocol.ErrorCode.PERMISSION_DENIED, str(exc))
    except OSError as exc:
        # errno 13 permiso denegado en algunos sistemas se reporta como OSError
        code = protocol.ErrorCode.FILE_LOCKED if getattr(exc, "errno", None) == 32 else protocol.ErrorCode.UNKNOWN
        protocol.emit_error(code, str(exc))
    except Exception as exc:  # noqa: BLE001 - último recorte: nunca dejar el proceso morir en silencio
        protocol.log(f"Excepción no controlada: {exc!r}")
        protocol.emit_error(protocol.ErrorCode.UNKNOWN, str(exc))


def main() -> None:
    cancel_event = threading.Event()
    worker: threading.Thread | None = None

    for command in protocol.read_commands():
        cmd_type = command.get("type")

        if cmd_type == "separate":
            cancel_event.clear()
            worker = threading.Thread(
                target=_handle_separate, args=(command, cancel_event), daemon=True
            )
            worker.start()
        elif cmd_type == "cancel":
            cancel_event.set()
        else:
            protocol.log(f"Comando desconocido ignorado: {cmd_type!r}")

    # stdin cerrado (la app pidió salir): si hay una separación en curso,
    # se le pide cancelar y se espera un poco antes de terminar el proceso.
    if worker is not None and worker.is_alive():
        cancel_event.set()
        worker.join(timeout=5)


if __name__ == "__main__":
    main()
