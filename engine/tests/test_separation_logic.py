"""
Pruebas de las funciones puras de engine/limbus_engine/separation.py que NO
requieren torch/demucs instalados (esas partes solo se pueden probar de
verdad en el runner de Windows de CI — ver
.github/workflows/build-windows.yml, paso "Smoke test embedded runtime").
Aquí se cubre la lógica que sí es responsabilidad nuestra y sí podemos
verificar en cualquier máquina: detección de silencio y complemento Other.
"""
import numpy as np
import pytest

from limbus_engine.separation import SILENCE_RMS_THRESHOLD, has_meaningful_signal


def test_silence_is_detected_as_not_meaningful():
    silent = np.zeros((2, 44100), dtype=np.float32)
    assert has_meaningful_signal(silent) is False


def test_real_signal_is_detected_as_meaningful():
    t = np.linspace(0, 1, 44100, dtype=np.float32)
    tone = 0.2 * np.sin(2 * np.pi * 440 * t)
    stereo = np.stack([tone, tone])
    assert has_meaningful_signal(stereo) is True


def test_tiny_noise_below_threshold_is_not_meaningful():
    rng = np.random.default_rng(42)
    tiny_noise = (rng.random((2, 44100)).astype(np.float32) - 0.5) * (SILENCE_RMS_THRESHOLD / 10)
    assert has_meaningful_signal(tiny_noise) is False


def test_empty_array_is_not_meaningful():
    assert has_meaningful_signal(np.zeros((2, 0), dtype=np.float32)) is False


@pytest.mark.parametrize("threshold", [1e-3, 1e-5])
def test_custom_threshold_is_respected(threshold):
    t = np.linspace(0, 1, 1000, dtype=np.float32)
    quiet_tone = 1e-4 * np.sin(2 * np.pi * 100 * t)
    stereo = np.stack([quiet_tone, quiet_tone])
    result = has_meaningful_signal(stereo, threshold=threshold)
    expected = float(np.sqrt(np.mean(np.square(stereo)))) > threshold
    assert result == expected
