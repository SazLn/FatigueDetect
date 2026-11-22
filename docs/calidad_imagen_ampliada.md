# Calidad de imagen ampliada

## Estado

Propuesta disponible en `descarga/calidad-imagen-ampliada`. No está integrada en `main` y requiere
validación manual en Windows con cámaras y condiciones variadas.

La rama reconoce perfiles biométricos `FDENC1:` creados por `descarga/proteccion-datos` si conserva
`clave_datos.bin` y se ejecuta con el mismo usuario de Windows. Mantiene cifrados esos perfiles al guardar,
pero no cifra perfiles JSON normales desde esta propuesta.

## Qué mide

Cada fotograma recibe puntajes orientativos de 0 a 100:

- **Brillo:** favorece una luminancia media útil y penaliza oscuridad o sobreexposición.
- **Contraste:** usa la dispersión de intensidades para detectar una señal demasiado uniforme.
- **Nitidez:** usa la varianza del Laplaciano como indicio de enfoque y detalle.
- **Posición facial:** combina centrado, tamaño aparente y separación de los bordes cuando hay un solo rostro.

El diagnóstico **cámara/entorno** combina brillo (35 %), contraste (25 %) y nitidez (40 %). El
diagnóstico de **colocación** usa la posición facial. La ausencia de rostro no inventa una posición:
se refleja en la cobertura facial que ya medía la aplicación.

Para sesiones nuevas, el puntaje global pondera fotogramas analizables (35 %), cobertura facial
(25 %), estabilidad de FPS (15 %), brillo (10 %), contraste (5 %), nitidez (5 %) y posición facial
(5 %). Si una métrica no existe, se excluye y se normalizan los pesos restantes; así los registros
anteriores siguen siendo legibles.

## Dónde aparece

- En vivo, junto a los FPS y mediante una recomendación de cámara/entorno o colocación.
- En el resumen final y en el detalle del historial, con los factores que redujeron el puntaje.
- En SQLite, mediante columnas añadidas de forma compatible a `CalidadSesiones`.
- En Excel, al final de la hoja **Sesiones**, sin desplazar las columnas anteriores.

## Prueba manual recomendada

1. Inicia una sesión con luz frontal, lente limpia y rostro centrado a una distancia normal. Comprueba
   que cámara/entorno y colocación sean estables y que no aparezca una recomendación persistente.
2. Reduce la luz y luego apunta una lámpara hacia la cámara. Debe bajar brillo y aparecer el aviso de
   cámara/entorno; la colocación no debería caer por sí sola.
3. Cubre parcialmente la lente o desenfócala. Deben bajar contraste o nitidez y mantenerse los avisos
   existentes de imagen tapada/desenfocada cuando se confirme la condición.
4. Con buena imagen, desplázate a izquierda/derecha, acércate, aléjate y coloca el rostro junto a un
   borde. Debe bajar colocación mientras cámara/entorno permanece razonablemente alto.
5. Vuelve al centro y comprueba que la recomendación desaparezca. Detén la sesión y revisa resumen,
   historial y las columnas nuevas del Excel.
6. Abre una sesión guardada antes de esta rama. Debe cargar sin error y mostrar «sin medición» para
   los indicadores nuevos.

## Límites pendientes

Los umbrales son heurísticos, no una certificación de la cámara ni una medida médica. La nitidez depende
del contenido de la escena y una pared uniforme puede parecer desenfocada. La posición usa el rectángulo
facial, no una medida física exacta. Antes de integrar se deben probar cámaras internas y USB, varias
resoluciones, luz diurna/nocturna, fondos y personas diversas; después podrán ajustarse los umbrales.
