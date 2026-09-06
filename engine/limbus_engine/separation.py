"""
Lógica de separación real. Sin archivos silenciosos falsos (sección 5/6 del
prompt original): antes de escribir cualquier pista se comprueba que tenga
señal real con `has_meaningful_signal`.

LIMITACIÓN CONOCIDA (documentada, no oculta): la cancelación cooperativa en
este v1 solo se comprueba ANTES y DESPUÉS de la llamada a
`demucs.apply.apply_model`, no dentro de ella (esa función no expone un
gancho de cancelación por segmento sin parchear la librería). Para archivos
muy largos, cancelar puede tardar hasta que termine el segmento en curso.
Ver docs/PENDING_TESTS.md.
"""
from __future__ import annotations

import threading
from pathlib import Path
from typing import Callable, Optional

import numpy as np
import soundfile as sf

from limbus_engine import protocol
from limbus_engine.manifest_verify import ModelVerificationError, verify_model_file

SILENCE_RMS_THRESHOLD = 1e-4


class SeparationCancelled(Exception):
    pass


def has_meaningful_signal(waveform: np.ndarray, threshold: float = SILENCE_RMS_THRESHOLD) -> bool:
    """RMS por encima del umbral => hay señal real, no un archivo
    "silencioso falso" (sección 5/6)."""
    if waveform.size == 0:
        return False
    rms = float(np.sqrt(np.mean(np.square(waveform))))
    return rms > threshold


def ensure_model_ready(model_id: str, manifest_path: Path, cache_dir_for_torch: Path):
    """Descarga (si hace falta) y verifica el modelo, dejando el hub de
    torch apuntando a nuestra propia carpeta controlada (nunca site-packages
    del usuario ni una ruta fuera de nuestro control)."""
    import os

    os.environ["TORCH_HOME"] = str(cache_dir_for_torch)
    cache_dir_for_torch.mkdir(parents=True, exist_ok=True)

    from demucs.pretrained import get_model  # import perezoso: puede tardar / requerir descarga

    protocol.emit_stage("loading_model")
    model = get_model(model_id)

    # Ubicar el archivo de pesos ya descargado para poder verificarlo. La
    # ubicación exacta depende de cómo demucs organiza su caché interna
    # (torch hub); se busca el .th/.safetensors más reciente bajo
    # cache_dir_for_torch como mejor esfuerzo razonable.
    candidates = list(cache_dir_for_torch.rglob("*.th")) + list(cache_dir_for_torch.rglob("*.safetensors"))
    if not candidates:
        raise ModelVerificationError(
            protocol.ErrorCode.MODEL_MISSING,
            "No se encontró ningún archivo de pesos tras la descarga; no se puede verificar.",
        )
    newest = max(candidates, key=lambda p: p.stat().st_mtime)

    try:
        verify_model_file(manifest_path, model_id, newest)
    except ModelVerificationError:
        # Fail-closed: si el hash no cuadra, se borra el archivo sospechoso
        # para forzar una descarga limpia la próxima vez, en vez de dejarlo
        # ahí para que alguien lo use "por accidente".
        newest.unlink(missing_ok=True)
        raise

    return model


def run_separation(
    input_path: Path,
    output_dir: Path,
    model,
    device: str,
    native_sources_to_export: list[str],
    fold_into_other: list[str],
    cancel_event: threading.Event,
    progress_cb: Callable[[float], None],
) -> list[dict]:
    import torch
    from demucs.apply import apply_model
    from demucs.audio import AudioFile, convert_audio

    protocol.emit_stage("separating")

    wav = AudioFile(str(input_path)).read(
        streams=0, samplerate=model.samplerate, channels=model.audio_channels
    )
    wav = convert_audio(wav, model.samplerate, model.samplerate, model.audio_channels)
    original_mix = wav.numpy().copy()

    if cancel_event.is_set():
        raise SeparationCancelled()

    def _progress(state):
        # apply_model(..., progress=True) llama a un callback interno de
        # tqdm-like; aquí lo adaptamos a nuestro formato de evento.
        try:
            pct = float(state.get("segment_offset", 0)) / max(float(state.get("audio_length", 1)), 1.0)
            progress_cb(min(99.0, pct * 100.0))
        except Exception:  # noqa: BLE001 - el progreso es cosmético, nunca debe tumbar la separación
            pass

    with torch.no_grad():
        sources = apply_model(
            model,
            wav[None],
            device=device,
            shifts=1,
            split=True,
            overlap=0.25,
            progress=False,
        )[0]

    if cancel_event.is_set():
        raise SeparationCancelled()

    protocol.emit_stage("writing_stems")
    source_names = model.sources  # p. ej. ["drums", "bass", "other", "vocals"] o con guitar/piano
    name_to_tensor = {name: sources[i].numpy() for i, name in enumerate(source_names)}

    output_dir.mkdir(parents=True, exist_ok=True)
    results: list[dict] = []

    for native_name in native_sources_to_export:
        arr = name_to_tensor[native_name]
        if not has_meaningful_signal(arr):
            protocol.log(f"'{native_name}' no tiene señal significativa; no se exporta como pista vacía.")
            continue
        results.append(_write_stem(output_dir, native_name, arr, model.samplerate))

    other_arr = sum((name_to_tensor[n] for n in fold_into_other), np.zeros_like(original_mix))
    if has_meaningful_signal(other_arr):
        results.append(_write_stem(output_dir, "Other", other_arr, model.samplerate))
    else:
        protocol.log("'Other' no tiene señal residual real; no se exporta un archivo vacío.")

    _log_reconstruction_error(original_mix, name_to_tensor, source_names)

    return results


def _write_stem(output_dir: Path, name: str, arr: np.ndarray, sample_rate: int) -> dict:
    path = output_dir / f"{name}.wav"
    # arr: (channels, length) -> soundfile espera (length, channels)
    sf.write(str(path), arr.T, sample_rate, subtype="PCM_24")
    return {
        "name": name,
        "path": str(path),
        "sample_rate": sample_rate,
        "channels": arr.shape[0],
        "duration_seconds": arr.shape[1] / sample_rate,
    }


def _log_reconstruction_error(original_mix: np.ndarray, name_to_tensor: dict, source_names: list[str]) -> None:
    """No es un gate que bloquee la exportación (el prompt pide 'el menor
    error posible', no un umbral exacto), pero sí queda registrado para el
    informe de calidad (sección 25)."""
    total = sum(name_to_tensor[n] for n in source_names)
    max_len = min(total.shape[1], original_mix.shape[1])
    diff = np.abs(total[:, :max_len] - original_mix[:, :max_len])
    protocol.log(f"Error máximo de reconstrucción de la mezcla: {float(diff.max()):.6f}")
