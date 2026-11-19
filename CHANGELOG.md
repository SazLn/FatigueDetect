# Registro de cambios

## Sin publicar

- Configuración permite elegir una cámara por nombre, guardar la selección y comprobar el encuadre y la iluminación en una vista previa.
- El monitoreo avisa cuando la imagen permanece oscura o sobreexpuesta durante dos segundos.

## Historial en SQLite

- El historial guarda sesiones y eventos en SQLite. Las filas válidas del CSV anterior se importan una vez.
- El monitoreo registra posibles cierres oculares prolongados, alarmas, falta de rostro, varios rostros y problemas de iluminación.
- El detalle de cada sesión muestra la cronología de eventos y la duración disponible.

## Identificación facial local

- Se añadió el registro voluntario del rostro, el autocompletado de datos tras el reconocimiento y la opción para eliminar el perfil biométrico.
