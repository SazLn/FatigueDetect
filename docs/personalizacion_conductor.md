# Personalización por conductor

Estado: integrada en `main` mediante el PR #17 después de la validación manual inicial.

## Alcance

La propuesta crea una línea base local e independiente para cada cédula. Conserva el tiempo ocular
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
médica. La personalización tampoco modifica el EAR, el tiempo de somnolencia o el umbral que activa la
alarma; solo adapta la comparación histórica a la variación normal observada de cada persona.

## Persistencia y visualización

SQLite guarda el perfil por cédula y la comparación junto a cada sesión. El resumen final y la ventana
**Atención y resumen** indican el progreso de calibración, valores actuales, línea base, diferencias y el
motivo por el que una sesión se incorporó o se excluyó. Excel añade la hoja **Comparación personal**.
Las sesiones anteriores permanecen como «sin comparación personal». **Eliminar historial** también borra
los perfiles, porque se derivan de esas sesiones.

## Prueba manual con cámara

1. Usa la misma cédula y graba tres sesiones normales con al menos un minuto de medición ocular válida.
   Mantén calidad superior a 70 y cobertura ocular superior al 60 %, sin provocar alarmas.
2. Comprueba que las dos primeras indiquen calibración en progreso y que la tercera deje el perfil calibrado.
3. Cierra y abre la aplicación. Graba una cuarta sesión normal y confirma que muestra la línea base anterior
   y que la incorpora como nueva referencia.
4. En una quinta sesión, provoca cierres suficientes para elevar PERCLOS o reducir la atención. Verifica que
   se marque el cambio y que el número de sesiones de la línea base no aumente.
5. Repite con otra cédula y confirma que comienza una calibración independiente.
6. Prueba poca luz, salir del encuadre o una sesión menor de un minuto. Debe explicar la exclusión y no avanzar
   la calibración.
7. Revisa el resumen histórico y exporta Excel. Los mismos valores y estados deben aparecer en la hoja
   **Comparación personal**.
8. Elimina el historial y confirma que la siguiente sesión comienza una línea base nueva.

Queda pendiente repetir estas pruebas con distintas personas, cámaras, gafas, distancias e iluminación y
ajustar los límites con datos reales antes de proponer su integración en `main`.
