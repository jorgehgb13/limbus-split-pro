"""
Este script SOLO se ejecuta dentro de la pipeline de CI en un runner
Windows real con acceso a internet (ver .github/workflows/build-windows.yml,
paso "Pin model hashes"). No se puede ejecutar de forma útil en el entorno
donde se preparó este repositorio (sin acceso a los servidores de descarga
de los pesos).

Qué hace:
1. Para cada modelo en models/manifest.json, fuerza la descarga real
   (demucs.pretrained.get_model) hacia una carpeta de trabajo temporal.
2. Calcula el SHA-256 y el tamaño real del archivo de pesos descargado.
3. Reescribe models/manifest.json reemplazando "PENDING_CI_PIN" y el
   tamaño placeholder por los valores reales, calculados en ese momento
   contra la fuente oficial — nunca copiados de una lista externa sin
   verificar (sección 7 del prompt original).

Si algún modelo sigue sin poder resolverse (por ejemplo, la fuente oficial
cambió de URL), el script termina con código de salida distinto de cero y
la pipeline debe fallar ahí, no seguir adelante con un manifiesto a medias.
"""
from __future__ import annotations

import hashlib
import json
import os
import sys
from pathlib import Path


def sha256_of(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def resolve_and_pin(model_id: str, work_dir: Path) -> tuple[str, int, Path]:
    work_dir.mkdir(parents=True, exist_ok=True)

    # IMPORTANTE: demucs 4.1.0 resuelve sus pesos pre-entrenados a través
    # de Hugging Face Hub (huggingface_hub), NO del mecanismo antiguo de
    # torch hub — por eso fijar solo TORCH_HOME no bastaba (los archivos
    # terminaban en el caché por defecto de HF, fuera de nuestro control,
    # y nuestra búsqueda no los encontraba). Se fijan ambas variables de
    # entorno apuntando a la misma carpeta controlada.
    os.environ["TORCH_HOME"] = str(work_dir)
    os.environ["HF_HOME"] = str(work_dir)
    os.environ["HUGGINGFACE_HUB_CACHE"] = str(work_dir)

    from demucs.pretrained import get_model  # requiere `pip install demucs` en el runner

    get_model(model_id)  # esto descarga el/los archivo(s) de pesos si faltan

    candidates = list(work_dir.rglob("*.th")) + list(work_dir.rglob("*.safetensors"))
    if not candidates:
        # Diagnóstico: si esto vuelve a fallar, listar qué SÍ se descargó
        # ayuda mucho más que solo decir "no se encontró nada".
        everything = list(work_dir.rglob("*"))
        listing = "\n".join(f"  - {p.relative_to(work_dir)}" for p in everything[:50]) or "  (carpeta vacía)"
        raise RuntimeError(
            f"No se encontró ningún archivo .th/.safetensors para '{model_id}' tras la descarga.\n"
            f"Contenido de {work_dir}:\n{listing}"
        )

    newest = max(candidates, key=lambda p: p.stat().st_mtime)
    digest = sha256_of(newest)
    size = newest.stat().st_size
    return digest, size, newest


def main() -> int:
    repo_root = Path(__file__).resolve().parent.parent.parent
    manifest_path = repo_root / "models" / "manifest.json"
    work_dir = repo_root / ".ci-model-cache"

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))

    any_failed = False
    for entry in manifest["models"]:
        model_id = entry["id"]
        print(f"[pin_model_hashes] Resolviendo '{model_id}'...", flush=True)
        try:
            digest, size, path = resolve_and_pin(model_id, work_dir / model_id)
        except Exception as exc:  # noqa: BLE001
            print(f"[pin_model_hashes] ERROR resolviendo '{model_id}': {exc}", file=sys.stderr)
            any_failed = True
            continue

        entry["sha256"] = digest
        entry["expected_size_bytes"] = size
        entry["relative_path"] = str(path.relative_to(work_dir / model_id))
        print(f"[pin_model_hashes] '{model_id}' -> sha256={digest} size={size}")

    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    if any_failed:
        print("[pin_model_hashes] Uno o más modelos no se pudieron pinear. Build debe fallar aquí.",
              file=sys.stderr)
        return 1

    print("[pin_model_hashes] manifest.json actualizado con hashes reales.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
