# Confiabilidad explicable de los eventos

Estado: implementada únicamente en `descarga/confianza-eventos-calidad`. No está integrada en `main`.

## Qué representa

Cada evento nuevo recibe un puntaje de confiabilidad de 0 a 100:

- **Alta:** 80 a 100.
- **Media:** 60 a menos de 80.
- **Baja:** menos de 60.
- **Sin medición:** sesión anterior o detector sin datos compatibles.

Este valor estima qué tan sólidos son los datos que respaldan el registro. No es la probabilidad de que
el conductor tenga fatiga, no reemplaza una revisión visual y no es un diagnóstico médico.

## Factores utilizados

La fórmula depende del tipo de evento:

| Evento | Factores |
|---|---|
| Cierre prolongado, cierre anómalo y alarma | Calidad de sesión 35 %, cobertura ocular 40 % y persistencia temporal 25 %. |
| Posible cabeceo | Calidad de sesión 40 %, cobertura facial 35 % y persistencia temporal 25 %. |
| Cámara tapada, desenfocada o congelada | Persistencia 70 %, estabilidad de FPS 15 % y continuidad de captura 15 %. |
| Ausencia, varios rostros e iluminación | Persistencia 55 %, estabilidad de FPS 25 % y continuidad de captura 20 %. |

La persistencia llega gradualmente al máximo según el tipo: 0,8 s para un cierre anómalo, 0,75 s para
un cabeceo, 3 s para cierres prolongados y alarmas, y 5 s para eventos de entorno o cámara. Estos tiempos
no cambian los umbrales del detector; solo califican la evidencia después de registrar el evento.

Una advertencia aparece cuando la calidad, cobertura o persistencia baja del 70 %, cuando la estabilidad
de FPS baja del 80 %, cuando faltan métricas o cuando hubo fallos de captura. Los eventos de detectores
futuros conservan la evaluación que ese detector proporcione en lugar de sobrescribirla.

## Persistencia y compatibilidad

La evaluación completa —versión, puntaje, factores y advertencias— se guarda como JSON en la tabla
`Eventos`. La migración añade la columna sin modificar los eventos existentes. Esos registros muestran
«sin medición» porque no se inventa una confianza retroactiva.

El resumen final cuenta eventos con confianza alta, media, baja o ausente y explica las limitaciones
generales de la sesión. El historial y la línea temporal muestran el nivel por evento. La hoja **Eventos**
del Excel añade puntaje, nivel y explicación.

## Prueba manual con cámara

Realiza las pruebas con el vehículo detenido:

1. Graba una sesión de al menos un minuto con iluminación frontal, rostro centrado y FPS estables.
2. Provoca un cierre prolongado controlado y, si lo deseas, una alarma. Al detener, comprueba que cada
   evento muestre puntaje, nivel y explicación.
3. Repite con poca luz o saliendo del encuadre durante varios segundos. Deben aparecer los eventos de
   entorno y la explicación debe mencionar cobertura o fotogramas analizables bajos en el resumen.
4. Repite tapando parcialmente la cámara o desenfocándola. Un episodio sostenido debe obtener más
   persistencia que uno muy breve.
5. Reduce los FPS seleccionados o carga el equipo. Si el tiempo con FPS bajos es importante, la explicación
   debe señalar la baja estabilidad.
6. Abre una sesión creada por una versión anterior: debe indicar «sin medición», sin asignar un valor falso.
7. Exporta Excel y comprueba las tres columnas nuevas de la hoja **Eventos**.

Queda pendiente validar si los pesos y cortes 60/80 resultan comprensibles y estables con distintas cámaras,
personas, iluminación, gafas y rendimiento antes de proponer el merge.
