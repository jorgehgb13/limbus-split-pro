"""
Detección segura de CPU/GPU y comprobación previa de memoria (sección 10:
"Prueba previa de memoria antes de procesar canciones largas", "Fallback
automático a CPU con explicación clara", "No presentes una opción de GPU
que falle silenciosamente").
"""
from __future__ import annotations

import os
from dataclasses import dataclass


@dataclass
class DeviceDecision:
    device: str  # "cpu" | "cuda:0"
    reason: str
    estimated_memory_needed_bytes: int
    estimated_memory_available_bytes: int


def _cuda_available() -> tuple[bool, str]:
    try:
        import torch  # import perezoso: en máquinas sin GPU esto igual funciona,
        # torch.cuda.is_available() simplemente da False.
    except ImportError as exc:  # pragma: no cover - solo si el runtime está mal armado
        return False, f"No se pudo importar torch: {exc}"

    if not torch.cuda.is_available():
        return False, "No se detectó una GPU NVIDIA compatible con CUDA."

    try:
        _ = torch.cuda.get_device_name(0)
    except Exception as exc:  # noqa: BLE001 - queremos degradar a CPU ante cualquier fallo aquí
        return False, f"GPU detectada pero no utilizable ({exc})."

    return True, "GPU NVIDIA compatible detectada."


def _estimate_required_memory_bytes(duration_seconds: float, channels: int, sample_rate: int) -> int:
    # Estimación conservadora: el modelo trabaja internamente con varios
    # buffers intermedios del tamaño de la señal de entrada. Se usa un
    # multiplicador generoso para no subestimar y terminar con un
    # OUT_OF_MEMORY a mitad de proceso.
    raw_audio_bytes = duration_seconds * channels * sample_rate * 4  # float32
    return int(raw_audio_bytes * 12)


def _available_system_memory_bytes() -> int:
    try:
        import psutil  # type: ignore
        return psutil.virtual_memory().available
    except ImportError:
        # Sin psutil, se es conservador: se asume 4 GiB disponibles.
        return 4 * 1024 * 1024 * 1024


def decide_device(
    preference: str,
    duration_seconds: float,
    channels: int,
    sample_rate: int,
) -> DeviceDecision:
    needed = _estimate_required_memory_bytes(duration_seconds, channels, sample_rate)

    if preference == "cpu":
        available = _available_system_memory_bytes()
        return DeviceDecision("cpu", "Forzado por el usuario.", needed, available)

    gpu_ok, gpu_reason = (False, "No solicitada.") if preference == "cpu" else _cuda_available()

    if preference == "gpu" and not gpu_ok:
        available = _available_system_memory_bytes()
        return DeviceDecision(
            "cpu",
            f"Se pidió GPU pero no está disponible ({gpu_reason}). Se usa CPU automáticamente.",
            needed,
            available,
        )

    if preference == "auto" and gpu_ok:
        try:
            import torch
            free, _total = torch.cuda.mem_get_info(0)
        except Exception:  # noqa: BLE001
            free = 0
        if free >= needed:
            return DeviceDecision("cuda:0", gpu_reason, needed, free)
        # GPU presente pero sin memoria suficiente para este archivo: cae a
        # CPU con explicación, en vez de intentarlo y fallar a mitad de
        # camino.
        available = _available_system_memory_bytes()
        return DeviceDecision(
            "cpu",
            f"GPU detectada pero sin memoria suficiente para este archivo "
            f"(necesita ~{needed / 1e9:.1f} GB, libres ~{free / 1e9:.1f} GB). Se usa CPU.",
            needed,
            available,
        )

    available = _available_system_memory_bytes()
    return DeviceDecision("cpu", "CPU (modo automático, sin GPU disponible).", needed, available)
