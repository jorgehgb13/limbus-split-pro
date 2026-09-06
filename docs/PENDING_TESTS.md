# Pruebas pendientes en Windows real

Este código se escribió en un entorno Linux sin Windows ni GPU. Todo lo de
esta lista requiere haberse ejecutado ya sea en el runner `windows-latest` de
GitHub Actions (algunas cosas) o en tu propio PC con Windows (las que
necesitan hardware de audio real, tu tarjeta gráfica, o interacción humana).
Numeración según la sección 23 del prompt original.

## Lo que la pipeline de CI SÍ puede verificar automáticamente en windows-latest

- [ ] 1. Compilación reproducible desde un checkout limpio.
- [ ] 2. Arranque sin Visual Studio instalado (el runner no lo tiene).
- [ ] 3. Arranque sin Python del sistema (se usa el embebido).
- [ ] 11. Ejecución en CPU (el runner no tiene GPU).
- [ ] 14–19. Duración/frecuencia/canales/reconstrucción de mezcla — con un
  archivo de prueba sintético que se agrega a `tests/fixtures/`.
- [ ] 39–40. Arquitectura x64 correcta de EXE/DLL y dependencias completas
  (vía el paso de auditoría de DLL en el workflow).
- [ ] 43. SBOM generado y no vacío.
- [ ] 45. Ausencia de modelos no autorizados embebidos en el instalador
  (el verificador fail-closed corre en CI).

## Lo que SOLO puedes verificar tú, en tu PC, con hardware real

- [ ] 12–13. GPU compatible (NVIDIA) y fallback a CPU — depende de tu tarjeta.
- [ ] 20–22. Mute/Solo/Volumen durante reproducción real con tu tarjeta de
  sonido (WASAPI).
- [ ] 23. Seek suave sin clics — solo se percibe escuchando de verdad.
- [ ] 27. Cambio de dispositivo de audio (desconectar/conectar audífonos).
- [ ] 28. Suspensión y reanudación de Windows.
- [ ] 29–30. Rutas Unicode largas y carpetas de OneDrive en tu propio disco.
- [ ] 33–35. Instalación limpia, actualización y desinstalación reales.
- [ ] 36–38. Ejecución como usuario sin privilegios, ausencia de escritura en
  Program Files, ausencia de rutas del ordenador de compilación — revisar
  manualmente tras instalar.
- [ ] 41–42. Firma Authenticode real (necesitas un certificado de firma de
  código propio; este repo no incluye ni puede generar uno) y comportamiento
  de SmartScreen/Defender con esa firma.
- [ ] Calidad real de separación escuchando resultados con canciones reales
  (con batería, voz, bajo y guitarra reales, no solo el fixture sintético).

## Nota sobre firma de código

El prompt exige firma Authenticode antes de considerar la app "comercial".
Este repositorio **no incluye ningún certificado ni secreto de firma** (eso
violaría las reglas de seguridad de este proyecto). Si más adelante consigues
un certificado de firma de código (de una CA como DigiCert, SSL.com, etc.),
el paso de firma en `.github/workflows/build-windows.yml` está preparado
como condicional (`if: secrets.SIGNING_CERTIFICATE != ''`) para activarse
solo cuando configures esos secretos en GitHub — nunca se pide que los pegues
en texto plano en ningún archivo del repo.
