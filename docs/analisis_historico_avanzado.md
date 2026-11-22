# Análisis histórico avanzado

Estado: integrado en `main` mediante el PR #22. Se recomienda seguir validando la interfaz con historiales reales en Windows.

En **Historial → Estadísticas** se agregan pestañas de riesgo, vehículo, horario, día semanal, mapa de calor, tendencia personal y comparación de sesiones. Los filtros del historial se aplican a estas vistas.

El índice de riesgo es `100 − atención media` y solo se ordenan sesiones con atención calculable y calidad de al menos 50/100. Es orientativo, no una medida de seguridad vial. Las alarmas por hora usan la suma del tiempo activo de monitoreo; si falta la calidad de una sesión terminada se usa su duración, y si no hay tiempo válido se muestra «—». PERCLOS y frecuencia de parpadeos se ponderan por tiempo ocular válido. La calidad se pondera por tiempo activo y la atención por tiempo con puntaje válido. El mapa agrupa sesiones por hora local de inicio, en bloques de cuatro horas.

## Prueba en Windows

1. Descarga el ZIP de la rama, ábrelo en Visual Studio 2022 y ejecuta `Presentacion`.
2. Haz al menos tres sesiones con la misma cédula en días u horas diferentes; registra otra placa y otra cédula. Alterna sesiones normales y alguna alarma, siempre con el vehículo detenido.
3. En **Historial → Estadísticas**, verifica que **Seguras y riesgosas** ordene de menor a mayor riesgo, y que una sesión antigua sin atención no reciba un riesgo inventado.
4. En **Tendencia personal**, cambia de cédula y comprueba el orden cronológico y la ausencia de valores cuando no se midieron.
5. Comprueba **Por vehículo**, **Por horario** y **Por día semanal**. El total de sesiones de cada grupo debe corresponder al historial filtrado. El mapa debe colocar las sesiones en el día y bloque horario local de inicio.
6. En **Comparar sesiones**, marca dos y luego tres sesiones. Deben aparecer sus valores individuales y una fila para el conjunto seleccionado. Desmarca una y comprueba que cambie el resultado.
7. Aplica filtros de fecha, conductor o vehículo en Historial y reabre Estadísticas. Las vistas nuevas deben limitarse a esas sesiones. Limpia los filtros y verifica que vuelvan.

Las comprobaciones automatizadas se ejecutaron desde Linux. La guía anterior permite repetir la validación visual y de datos en Windows.
