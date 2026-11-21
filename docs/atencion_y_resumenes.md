# Atención y resúmenes: propuesta para descarga

Rama: `descarga/atencion-y-resumenes`, basada en `main` después del PR #14.
No está integrada en `main`. El bloque de cabeza, postura y distracción pertenece al colaborador.

## Comportamiento disponible

El medidor en vivo muestra un puntaje de 0 a 100 o «—» cuando la medición no alcanza los requisitos.
Al detener la sesión se abre un informe con resumen automático, evolución y descuentos por muestra,
respuesta a cada alarma y acceso a la línea temporal. Se puede reabrir en Historial → Atención y resumen.
Excel añade Atención, Evolución atención y Respuesta a alarmas. Los datos antiguos no se reconstruyen
como si hubieran sido medidos: muestran «sin medición».

Se conservan muestras aproximadamente cada segundo, con su instante UTC, tiempo activo, tiempo ocular
representado, calidad, puntaje y descuentos. Se guardan en JSON versionado dentro de la nueva tabla
AtencionSesiones, en la misma transacción que sesión y eventos, con borrado en cascada.

## Fórmula experimental v1

Los factores disponibles se normalizan por la suma de sus pesos. Cada descuento es
`100 × peso × riesgo / suma de pesos disponibles`; el puntaje es `100 − suma de descuentos`.

| Factor | Peso | Riesgo (limitado a 0–1) |
|---|---:|---|
| PERCLOS reciente de 60 s | 40 | PERCLOS / 0,40 |
| Cierres prolongados confirmados en los últimos 60 s | 25 | Cantidad / 3 |
| Cabeceos confirmados en los últimos 60 s | 20 | Cantidad / 3 |
| Distracción reciente, cuando exista medición | 15 | Proporción distraída / 0,30 |

Un cierre prolongado entra una sola vez al alcanzar el umbral configurado, sin esperar a reabrir
los ojos. Cabeceos se incluyen cuando existe pose calibrada en la muestra; su detector no cambia.
Sin pose se omite ese factor y se normaliza con los otros pesos. Una entrada de distracción desconocida
se omite; no se interpreta como cero distracción. Los 60 s son tiempo transcurrido, incluso tras pausas.

La calidad acumulada de detección debe alcanzar 50/100 y la cobertura ocular acumulada, 50 % del
tiempo activo. Además se exige una apertura ocular válida actual y PERCLOS disponible (2 s válidos).
La calidad actúa como condición de confianza del resultado, sin descontarla como conducta del conductor.
Una sesión con mala cobertura puede quedarse sin puntaje aunque recupere la imagen brevemente.
Los pesos y umbrales son reglas iniciales pendientes de calibración, no una medida médica de atención.

El puntaje final es el promedio de muestras válidas ponderado por tiempo ocular representado.
El informe enumera descuentos medios por factor. Estos describen las muestras en las que el factor
estuvo disponible; con disponibilidad variable sus medias no tienen por qué sumar el descuento medio total.
El tiempo representado excluye separaciones mayores de 250 ms y mediciones inválidas.

## Métricas temporales y alarmas

- Primer síntoma: inicio del primer cierre que alcanza el umbral de somnolencia o del primer cabeceo
  confirmado. Se expresa desde el inicio activo y excluye pausas. «No observado» no garantiza ausencia de fatiga.
- Minuto de mayor fatiga observada: minuto activo con menor puntaje ponderado, con al menos 2 s válidos.
  Se numeran desde 1; puede ser parcial. Empates eligen el primero. Es un indicador basado en la fórmula.
- Tendencia: compara los tercios inicial y final de las muestras válidas, ponderados por tiempo.
  Requiere al menos 6 muestras y 30 s válidos: más de +5 puntos mejora, menos de −5 empeora, el resto es estable.
- Respuesta ocular: tiempo activo desde la activación de alarma hasta la primera observación de ambos
  ojos con apertura de al menos 55 % y alarma desactivada. No demuestra que el sonido causara la respuesta.
- Recuperación confirmada: desde activación hasta completar 2 s continuos con ambos ojos abiertos,
  cabeza frontal (horizontal ±20°, vertical y lateral ±15°), calidad ≥50 y sin distracción si se mide.
  Es una recuperación ocular/frontal observada, sin estimar mirada ni estado clínico.
- Pausa, pérdida de imagen, final de sesión o nueva alarma sin recuperación dejan la anterior como
  no confirmada, conservando la respuesta ocular si se alcanzó. No se rellenan huecos con valores sanos.

## Integración con el colaborador

`FatigaEnVivo.ProporcionDistraccion` es una entrada opcional: `null` significa no medido; un valor
entre 0 y 1 indica la proporción reciente distraída. El detector deberá asignarla al resultado de
`RastreadorFatiga.Observar`, antes de llamar a `RastreadorAtencion.Observar` en DetectionController.
Debe basarse en intervalos válidos y su propia ventana documentada de 60 s. Cuando no exista medición,
debe enviar null, no cero. Por ahora la aplicación siempre envía null y el informe lo identifica.

Los detectores de nuevos eventos pueden llamar a `SesionConduccion.RegistrarEvento` con tipo, inicio
y fin. La línea temporal genera sus pistas automáticamente y permite desplazamiento vertical.
Para códigos que aún no pertenecen al enum, usar `TipoEventoSesion.EventoExterno` y `CodigoTipo`;
el repositorio conserva también los códigos desconocidos leídos de una base de una versión posterior.
El detalle, la línea temporal y la hoja Eventos muestran el código original. Los resúmenes por gravedad
tratan tipos externos como gravedad baja hasta que se añada su clasificación específica.
No se generan eventos de distracción, bostezos o postura en esta entrega.

## Verificación reproducible

```sh
dotnet build FatigueDetect.sln -c Release -p:EnableWindowsTargeting=true
dotnet run --project tests/Atencion.Pruebas -c Release
```

Las pruebas ejecutan la fórmula, ventanas, respuesta y recuperación, interrupciones, tendencia,
compatibilidad con una base sin AtencionSesiones, persistencia, códigos futuros, borrado en cascada
y exportación con sesiones nuevas, antiguas y selección vacía. El exportador se ejecuta sin interfaz
mediante un adaptador de nombres de eventos; la app completa se compila para Windows.

Pendiente antes de integrar: cámara y presentación Windows, calibración de fórmula y umbrales,
integración real de distracción y validación con distintas personas, gafas, cámaras e iluminación.

## Prueba manual con cámara Windows

1. Respaldar historial.sqlite y ejecutar esta rama. Mirar al frente y calibrar. Confirmar puntaje o
   «—» durante el inicio, y que el puntaje aparece al disponer de datos suficientes.
2. Hacer una sesión de más de un minuto, alternando ojos abiertos, cierres prolongados y cabeceos.
   Detener y contrastar los factores, primer síntoma, minuto de menor puntaje y tendencia.
3. Activar una alarma, reabrir los ojos y mantener postura frontal durante 2 s. Revisar los dos tiempos
   de respuesta. Repetir pausando o saliendo del encuadre antes de esos 2 s: no debe confirmar recuperación.
4. Pausar varios segundos y reanudar. Confirmar que el gráfico deja un hueco y el tiempo activo excluye la pausa.
5. Reiniciar la app y comparar informe histórico y Excel con el resumen final. Una sesión anterior
   debe mostrar atención sin medición. Revisar las pistas y la tabla de todos los eventos.
