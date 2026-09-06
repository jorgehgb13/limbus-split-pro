import hashlib
import json
from pathlib import Path

import pytest

from limbus_engine.manifest_verify import ModelVerificationError, verify_model_file


@pytest.fixture()
def fake_model(tmp_path: Path):
    content = b"contenido de prueba, no es un modelo real"
    model_file = tmp_path / "fake-model.th"
    model_file.write_bytes(content)
    digest = hashlib.sha256(content).hexdigest()

    manifest = {
        "schema_version": "1.0",
        "models": [
            {
                "id": "fake-model",
                "sha256": digest,
                "expected_size_bytes": len(content),
                "relative_path": "fake-model.th",
            }
        ],
    }
    manifest_path = tmp_path / "manifest.json"
    manifest_path.write_text(json.dumps(manifest))

    return manifest_path, model_file


def test_correct_file_passes(fake_model):
    manifest_path, model_file = fake_model
    verify_model_file(manifest_path, "fake-model", model_file)  # no debe lanzar


def test_unregistered_model_raises(fake_model):
    manifest_path, model_file = fake_model
    with pytest.raises(ModelVerificationError) as exc:
        verify_model_file(manifest_path, "no-existe", model_file)
    assert exc.value.code == "MODEL_MISSING"


def test_missing_file_raises(fake_model):
    manifest_path, model_file = fake_model
    with pytest.raises(ModelVerificationError) as exc:
        verify_model_file(manifest_path, "fake-model", model_file.parent / "no-esta.th")
    assert exc.value.code == "MODEL_MISSING"


def test_tampered_file_raises_hash_mismatch(fake_model):
    manifest_path, model_file = fake_model
    model_file.write_bytes(b"contenido ALTERADO, distinto tamano y hash!!")
    with pytest.raises(ModelVerificationError) as exc:
        verify_model_file(manifest_path, "fake-model", model_file)
    assert exc.value.code == "HASH_MISMATCH"
