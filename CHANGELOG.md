# Registro de cambios

## Sin publicar

- El historial muestra FPS medios, fotogramas analizables, tiempo con rostro válido y episodios de fallo de cámara por sesión.
- El dashboard histórico muestra recuentos diarios, semanales y mensuales, duración promedio y horas de mayor uso.
- Se puede exportar el historial a Excel con sesiones, eventos, métricas de calidad y resúmenes.

- El monitoreo muestra avisos persistentes cuando no detecta el rostro o encuentra varias personas durante al menos dos segundos.
- El historial resume los eventos de cada sesión por tipo, cantidad, duración total y duración máxima.
- Al perder la señal de cámara se limpia la alerta visual anterior para evitar mostrar un estado desactualizado.
- Se actualizó el plan para marcar el selector de cámara y los avisos de iluminación como integrados en `main`, y se ajustó la lista de trabajo pendiente para reflejar la funcionalidad disponible.

- Configuración permite elegir una cámara por nombre, guardar la selección y comprobar el encuadre y la iluminación en una vista previa.
- El monitoreo avisa cuando la imagen permanece oscura o sobreexpuesta durante dos segundos.

## Historial en SQLite

- El historial guarda sesiones y eventos en SQLite. Las filas válidas del CSV anterior se importan una vez.
- El monitoreo registra posibles cierres oculares prolongados, alarmas, falta de rostro, varios rostros y problemas de iluminación.
- El detalle de cada sesión muestra la cronología de eventos y la duración disponible.

## Identificación facial local

- Se añadió el registro voluntario del rostro, el autocompletado de datos tras el reconocimiento y la opción para eliminar el perfil biométrico.
