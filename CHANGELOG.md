# Registro de cambios

## Sin publicar

- Se integraron en `main` las métricas oculares avanzadas: parpadeos por minuto y sus duraciones mínima, promedio y máxima, además de la separación entre parpadeos cortos, prolongados, cierres anómalos y posibles microsueños.
- Guarda el cierre y el microsueño más largos, el tiempo total con ojos cerrados, la diferencia promedio entre ojos y la evolución de PERCLOS cada 10 segundos.
- Las nuevas métricas aparecen en vivo, en el resumen final, el historial y Excel. SQLite migra las sesiones existentes y conserva como desconocido el detalle que no se midió anteriormente.
- La validación manual con cámara confirmó el funcionamiento de las métricas oculares avanzadas. Sigue pendiente calibrar sus umbrales con más cámaras, condiciones de iluminación y personas.
- Se integró la medición de fatiga con 468 puntos faciales mediante ONNX, apertura ocular por ojo, conteo de parpadeos, cierres prolongados, PERCLOS de 60 segundos y acumulado por sesión.
- El monitoreo muestra ángulos de cabeza y detecta posibles cabeceos. Los conteos, tiempos oculares y eventos se guardan en SQLite y aparecen en el historial y Excel.
- La validación manual inicial con cámara Windows confirmó el funcionamiento básico. Siguen pendientes la calibración de umbrales y pruebas con más cámaras, iluminación y personas.
- El plan incorpora como pendientes gráficos de Excel más explicativos y una salida de audio seleccionable y comprobable para la alarma.
- El historial filtra por fecha, conductor y vehículo. El dashboard y Excel aplican los mismos filtros.
- El dashboard y Excel muestran gráficos de sesiones, alarmas por hora y eventos por gravedad, con intervalos entre alarmas. El historial añade una línea temporal por sesión.
- La vista previa y el monitoreo detectan posibles imágenes tapadas, desenfocadas o congeladas. La selección de resolución y FPS se guarda entre ejecuciones.

- El historial muestra FPS medios, fotogramas analizables, tiempo con rostro válido y episodios de fallo de cámara por sesión.
- El dashboard histórico muestra recuentos diarios, semanales y mensuales, duración promedio y horas de mayor uso.
- Se puede exportar el historial a Excel con sesiones, eventos, métricas de calidad y resúmenes.
- Al detener el monitoreo, se muestra un resumen con la duración, las alarmas, las ausencias de rostro y los demás eventos destacados.
- Se conservan entre ejecuciones los umbrales de somnolencia y alarma y el estado del sonido. Restablecer guarda los valores predeterminados.
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
