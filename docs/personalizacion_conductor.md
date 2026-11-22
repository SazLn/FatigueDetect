# Personalización por conductor

Estado base: integrada en `main` mediante el PR #17. Ampliación contextual disponible en
`descarga/calibracion-personalizada`, pendiente de prueba manual y decisión de integración.

## Alcance

La versión de `main` crea una línea base local e independiente para cada cédula. Conserva el tiempo ocular
válido, tiempo con ojos cerrados, parpadeos y los promedios de atención disponibles. Al finalizar una
sesión compara sus parpadeos por minuto, PERCLOS y atención con esa referencia.

El perfil queda calibrado al reunir tres sesiones válidas y al menos 180 segundos de medición ocular.
Antes de ese punto, las sesiones aceptadas alimentan la calibración pero no se presentan como una
comparación contra una referencia ya establecida.

## Sesiones aceptadas como referencia

Una sesión solo actualiza el perfil si cumple todas estas condiciones:

- Identificación no vacía y medición ocular avanzada disponible.
- Al menos 60 segundos de tiempo ocular válido.
- Puntaje de calidad de 70/100 o más.
- Cobertura ocular del 60 % o más.
- Ninguna alarma, posible microsueño o cierre anómalo.

Una sesión puede compararse desde 30 segundos válidos, calidad 50/100 y cobertura ocular 50 %, aunque
no reúna las condiciones para actualizar el perfil. Cuando el perfil ya está calibrado, una sesión con
un cambio anormal tampoco se incorpora: así un episodio de fatiga no arrastra el promedio hacia el riesgo.

## Cambios anormales

- Parpadeos: diferencia absoluta superior al mayor valor entre 3 parpadeos/min y dos desviaciones
  estándar de las sesiones de referencia.
- PERCLOS: aumento superior al mayor valor entre 5 puntos porcentuales y dos desviaciones estándar.
- Atención: descenso superior al mayor valor entre 10 puntos y dos desviaciones estándar.

Los límites son experimentales. Detectar una diferencia no demuestra fatiga ni reemplaza una evaluación
médica. La personalización de `main` no modifica el EAR, el tiempo de somnolencia o el umbral que activa
la alarma; solo adapta la comparación histórica a la variación normal observada de cada persona.

## Ampliación contextual y calibración guiada

La rama de descarga separa el perfil mediante la combinación de cédula, cámara y placa. Al iniciar una
sesión se abre un asistente que permite reutilizar una referencia guardada para ese contexto o crear otra.
Una calibración nueva exige una postura frontal estable, al menos 15 muestras válidas durante dos segundos
y dos parpadeos completos. La cámara se libera antes de abrir el monitoreo.

Con tres sesiones de referencia, la línea base puede anticipar los tiempos configurados usando el PERCLOS
y la atención habituales. La reducción es de 0 a 0,30 segundos, nunca retrasa una alerta y conserva los
mínimos de configuración. Es una regla experimental, no una calibración médica. Los perfiles anteriores
sirven como punto de partida del primer contexto y después cada combinación evoluciona por separado.

## Persistencia y visualización

SQLite guarda el perfil por cédula; en la ampliación también usa cámara y vehículo. La comparación queda
junto a cada sesión. El resumen final y la ventana
**Atención y resumen** indican el progreso de calibración, valores actuales, línea base, diferencias y el
motivo por el que una sesión se incorporó o se excluyó. Excel añade la hoja **Comparación personal**.
Las sesiones anteriores permanecen como «sin comparación personal». **Eliminar historial** también borra
los perfiles, porque se derivan de esas sesiones.

## Prueba manual con cámara

1. Descarga `descarga/calibracion-personalizada`, compila y abre la aplicación en Windows.
2. Registra una sesión. En el asistente elige **Calibrar ahora**, mira al frente y completa dos parpadeos.
   **Continuar** solo debe habilitarse al terminar y la cámara debe pasar al monitoreo sin quedar bloqueada.
3. Detén la sesión y vuelve a usar la misma cédula, cámara y placa. Comprueba que aparece
   **Usar perfil guardado**.
4. Cambia la placa o la cámara. Debe solicitar una calibración independiente y no reutilizar automáticamente
   la referencia del contexto anterior.
5. Usa la misma cédula y graba tres sesiones normales de al menos un minuto ocular válido. Mantén calidad
   superior a 70 y cobertura ocular superior al 60 %, sin provocar alarmas.
6. Comprueba que las dos primeras indiquen calibración en progreso y que la tercera deje el perfil calibrado.
7. Inicia una cuarta sesión y revisa los umbrales efectivos en Inicio. Nunca deben ser mayores que los
   configurados ni reducirse más de 0,30 s.
8. Provoca de forma segura, con el vehículo detenido, cierres suficientes para elevar PERCLOS o reducir la
   atención. Debe marcar el cambio sin aumentar el número de sesiones de referencia.
9. Prueba poca luz, salir del encuadre o una sesión menor de un minuto. Debe explicar la exclusión.
10. Cierra y abre la aplicación, revisa el resumen y exporta Excel. El perfil debe conservarse.
11. Elimina el historial y confirma que la siguiente sesión comienza una línea base nueva y ya no ofrece
    referencias contextuales guardadas.

Queda pendiente repetir estas pruebas con distintas personas, cámaras, gafas, distancias e iluminación y
ajustar los límites con datos reales antes de proponer su integración en `main`.
