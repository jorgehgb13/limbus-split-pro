# Investigación de modelos de separación (julio–septiembre 2026)

Esta investigación se hizo con fuentes primarias (repositorio oficial, issues
del propio autor, README) y NO se aceptó automáticamente la etiqueta
"license: mit" de mirrors de terceros en Hugging Face, porque se encontró
evidencia de que esa etiqueta se copia de forma inconsistente entre mirrors.

## Hallazgo importante sobre licencia de los pesos de Demucs

- El **código** de Demucs (`facebookresearch/demucs`, ahora archivado y
  continuado por el autor en `adefossez/demucs`) es MIT — confirmado
  directamente en el README oficial: *"Demucs is released under the MIT
  license as found in the LICENSE file."*
- Existe una discusión pública abierta (issue #327 del repo oficial, y un
  hilo de discusión en el Hub de Hugging Face sobre `adefossez/HTDemucs`) que
  pregunta explícitamente si **los pesos entrenados** heredan esa misma
  licencia MIT o si están sujetos a los términos del dataset MUSDB18(-HQ)
  usado para entrenarlos, lo cual limitaría su redistribución comercial. No
  se encontró una respuesta pública, inequívoca y autorizada del autor que
  cierre esa pregunta. Varios mirrors de terceros en Hugging Face marcan los
  pesos como `license: mit` sin citar una fuente distinta de "mismo que el
  código", lo cual no es prueba suficiente para el estándar de este proyecto
  (sección 7 del prompt original: "Prohibición de asumir que los pesos son
  comerciales porque el repositorio sea MIT").

### Decisión tomada

**No se redistribuyen pesos de Demucs dentro del instalador.** En su lugar:

- La app descarga el modelo elegido **bajo demanda**, la primera vez que se
  necesita, directamente desde la fuente oficial (paquete `demucs`, que a su
  vez descarga desde el bucket oficial de Meta/AWS referenciado por
  `demucs.pretrained.get_model`).
- Se verifica el hash SHA-256 del archivo descargado contra el manifiesto
  (`models/manifest.json`) antes de permitir su uso.
- Esto reproduce exactamente el mismo flujo que la CLI oficial de `demucs`
  usa para cualquier usuario individual, y evita que Limbus Split Pro se
  convierta en el distribuidor de un peso cuya licencia de redistribución
  comercial no está confirmada.
- **Para tu uso personal en tu propio equipo** (que es el caso de este
  build) esto no representa ningún problema legal: estás usando el modelo
  igual que si hubieras instalado la CLI oficial tú mismo.
- Si en algún momento quisieras **redistribuir la app ya compilada con los
  modelos incluidos a terceros** (venderla, publicarla), habría que resolver
  antes esa ambigüedad con el autor o sustituir el modelo — el manifiesto ya
  deja el campo `redistribution_authorized` en `false` para que el
  verificador *fail-closed* bloquee ese escenario automáticamente hasta que
  se corrija.

## Modelos habilitados en este v1

| Modelo | Fuente | Licencia código | Licencia pesos | Salidas | Uso en la app |
|---|---|---|---|---|---|
| `htdemucs` | `facebookresearch/demucs` (adefossez/demucs) | MIT | Ver nota arriba — descarga bajo demanda, no redistribuida | drums, bass, other, vocals | Modelo por defecto para Voces/Batería/Bajo/Other |
| `htdemucs_6s` | Igual repo, variante de 6 fuentes | MIT | Igual nota | drums, bass, other, vocals, guitar, piano | Se usa automáticamente solo si el usuario marca "Guitarra" o "Piano y teclados" |

## Categorías del documento original que quedan DESHABILITADAS en este v1

Siguiendo la sección 5 del prompt ("Si no existe un modelo técnicamente válido
y con licencia adecuada: deja la opción desactivada, explica por qué, no
generes una pista vacía"):

| Categoría pedida | Motivo de no incluirla todavía |
|---|---|
| Voz principal vs. coros/segundas voces (separadas) | No se encontró un modelo local, verificado y con licencia de pesos clara que separe voz líder de coros de forma confiable; los candidatos conocidos (proyectos comunitarios tipo UVR de-reverb/de-echo/lead-vocal) tienen licencias de pesos poco documentadas o de solo investigación. |
| Efectos vocales / reverberación / ambiente vocal (pista propia) | Mismo motivo: los modelos de-reverb comunitarios verificados con licencia clara para este uso no están confirmados. |
| Ruido / artefactos (pista propia) | No hay un modelo de "residual de ruido" separado y verificado que no sea simplemente el resto del "other". |
| Bombo / Caja / Toms / Platos por separado (batería detallada) | Requiere un modelo específico de separación de partes de batería; los candidatos encontrados no tienen procedencia y licencia de pesos verificables al mismo nivel que Demucs. |
| Guitarra acústica vs. eléctrica por separado | `htdemucs_6s` da una sola pista "guitar" (no distingue acústica/eléctrica). |
| Otros instrumentos individuales (más allá de guitarra/piano) | No cubiertos por los modelos htdemucs disponibles. |

Estas casillas deben mostrarse **desactivadas en la UI** con un tooltip que
enlaza a esta sección, no ocultarse silenciosamente (así el usuario entiende
por qué faltan, tal como pide el prompt).

## Próxima iteración posible

Si quieres, en una siguiente iteración puedo investigar específicamente
modelos de separación de partes de batería y de voz líder/coros con licencia
de pesos verificable (por ejemplo, revisando modelos entrenados sobre
datasets con licencia permisiva declarada, en vez de MUSDB), y activar esas
categorías solo si superan el mismo estándar de verificación.
