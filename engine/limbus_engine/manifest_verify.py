"""
Espejo en Python de LimbusSplitPro.Core/Licensing/ModelManifestVerifier.cs.
Fail-closed: cualquier duda sobre el archivo del modelo -> se rechaza.
"""
from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from pathlib import Path


class ModelVerificationError(Exception):
    def __init__(self, code: str, message: str):
        super().__init__(message)
        self.code = code
        self.message = message


@dataclass
class ModelEntry:
    id: str
    sha256: str
    expected_size_bytes: int
    relative_path: str


def _load_manifest(manifest_path: Path) -> dict[str, ModelEntry]:
    data = json.loads(manifest_path.read_text(encoding="utf-8"))
    entries: dict[str, ModelEntry] = {}
    for m in data["models"]:
        entries[m["id"]] = ModelEntry(
            id=m["id"],
            sha256=m["sha256"],
            expected_size_bytes=m["expected_size_bytes"],
            relative_path=m["relative_path"],
        )
    return entries


def _sha256_of(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def verify_model_file(manifest_path: Path, model_id: str, local_file_path: Path) -> None:
    """Lanza ModelVerificationError si algo no cuadra. No retorna nada si
    todo está bien (éxito silencioso, error explícito)."""
    from limbus_engine.protocol import ErrorCode

    entries = _load_manifest(manifest_path)
    entry = entries.get(model_id)
    if entry is None:
        raise ModelVerificationError(
            ErrorCode.MODEL_MISSING,
            f"El modelo '{model_id}' no está en el manifiesto. No se usará.",
        )

    if not local_file_path.exists():
        raise ModelVerificationError(
            ErrorCode.MODEL_MISSING,
            f"No se encontró el archivo del modelo en '{local_file_path}'.",
        )

    actual_size = local_file_path.stat().st_size
    if actual_size != entry.expected_size_bytes:
        raise ModelVerificationError(
            ErrorCode.HASH_MISMATCH,
            f"Tamaño inesperado para '{model_id}': esperado {entry.expected_size_bytes}, "
            f"encontrado {actual_size}.",
        )

    actual_hash = _sha256_of(local_file_path)
    if actual_hash.lower() != entry.sha256.lower():
        raise ModelVerificationError(
            ErrorCode.HASH_MISMATCH,
            f"Hash SHA-256 no coincide para '{model_id}'. El archivo se eliminará y deberá "
            "volver a descargarse.",
        )
