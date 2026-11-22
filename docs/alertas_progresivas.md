# Alertas progresivas

Estado: disponible en `descarga/alertas-progresivas`, pendiente de validación en Windows y decisión de integración.

## Funcionamiento

El nivel preventivo aparece con atención de 75 o menos, PERCLOS desde 15 % o distracción reciente desde
15 %. El nivel alto usa atención de 50 o menos, somnolencia ocular o distracción desde 30 %. El crítico
se activa con la alarma ocular o atención de 25 o menos. Solo intervienen métricas disponibles; la calidad
insuficiente no se interpreta como fatiga.

Los sonidos son pulsos del mismo archivo de alarma y la salida elegida en Configuración:

- Preventivo: 35 % de volumen, 0,7 s y enfriamiento de 30 s.
- Alto: 65 % de volumen, 1,5 s y enfriamiento de 15 s.
- Crítico: 100 % de volumen, 4 s y enfriamiento de 5 s.

Una escalada interrumpe el pulso anterior y reproduce el nivel nuevo. Para reducir una alerta se requieren
ojos abiertos, cabeza frontal, calidad de al menos 50/100 y atención de al menos 70/100 de forma continua.
El nivel crítico baja después de 3 s, el alto después de otros 5 s y el preventivo después de 8 s. Perder
el rostro, la pose o la medición reinicia el tiempo. Esto describe recuperación observada, no aptitud clínica
para continuar conduciendo.

## Prueba con cámara en Windows

Realiza todas las pruebas con el vehículo detenido:

1. Descarga la rama, compila en Release y configura una salida de audio con volumen moderado.
2. Inicia una sesión, mira al frente y espera a que la atención sea medible. Comprueba el estado normal.
3. Provoca señales leves breves, por ejemplo mirar fuera del frente, hasta obtener **Alerta preventiva**.
   Repite la señal inmediatamente: el sonido no debe reiniciarse en cada fotograma.
4. Mantén un cierre suficiente para el estado de somnolencia. La tarjeta debe pasar a **Alerta alta** y el
   pulso debe sonar más fuerte que el preventivo.
5. Mantén el cierre hasta la alarma. Debe pasar inmediatamente a **Alerta crítica** y usar el sonido máximo.
6. Abre los ojos y mira al frente. La pantalla debe indicar recuperación pendiente; debe bajar primero a
   alta después de 3 s, luego a preventiva después de 5 s más y finalmente a normal después de 8 s más.
7. Repite una alerta y, durante la recuperación, sal del encuadre o gira la cabeza. El contador debe reiniciarse
   y el nivel no debe bajar mientras falte una medición válida.
8. Desactiva el sonido en Configuración y repite. La progresión visual debe funcionar sin audio.
9. Detén la sesión. En el historial y la línea temporal deben aparecer **Alerta preventiva**, **Alerta alta**
   y **Alerta crítica** con sus duraciones. Exporta Excel y confirma los mismos eventos.

Los umbrales son experimentales y deben probarse con distintas cámaras, personas e iluminación antes de
integrar la rama.
