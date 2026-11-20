# Plan de características para FatigueDetect

## Estado de implementación

| Característica | Estado | Alcance actual |
|---|---|---|
| Registro e identificación facial local | Integrado en `main` | Plantillas numéricas locales y opción de eliminación. |
| SQLite e importación de historial CSV | Integrado en `main` | Sesiones y eventos vinculados. Las filas válidas se importan una vez y el CSV se conserva. |
| Fecha, hora y duración de eventos | Integrado en `main` | Marcas con milisegundos para eventos nuevos. Las alarmas antiguas conservan los datos disponibles. |
| Cierre ocular prolongado y alarma | Integrado en `main` | Usa el detector actual y sus umbrales. Los cierres son estimados. |
| Rostro no detectado, varios rostros y mala iluminación | Integrado en `main` | Registra episodios desde dos segundos. El rostro no detectado puede indicar salida de cuadro u oclusión. |
| Cronología en el historial | Integrado en `main` | El detalle de cada sesión muestra tipo, hora y duración de sus eventos. |
| Selector de cámara y vista previa | Integrado en `main` | Guarda la cámara elegida y la usa en el registro facial y el monitoreo. |
| Avisos de iluminación | Integrado en `main` | La vista previa indica el estado en directo. El monitoreo avisa ante oscuridad o sobreexposición persistente. |
| Avisos en vivo de ausencia o varios rostros | Integrado en `main` | Aparecen después de dos segundos y se retiran al recuperar una detección válida. |
| Resumen de eventos en el historial | Integrado en `main` | Agrupa por tipo y muestra cantidad, duración total, máxima y eventos sin duración. |
| Resumen al finalizar el monitoreo | Integrado en `main` | Muestra duración, alarmas, ausencias de rostro y conteos de los demás eventos destacados. |
| Configuración persistente de detección y sonido | Integrado en `main` | Guarda y restablece los umbrales de somnolencia y alarma y el estado del sonido. |
| Métricas de calidad de sesión | Integrado en `main` | FPS medios, porcentaje de fotogramas analizables, tiempo con un rostro válido y episodios de fallo de cámara. Las sesiones previas conservan la calidad sin medición. |
| Dashboard histórico básico | Integrado en `main` | Sesiones por día, semana y mes, duración promedio y franjas de mayor uso. |
| Exportación a Excel | Integrado en `main` | Sesiones, eventos y resúmenes. |
| Filtros y análisis histórico | Integrado en `main` | Fecha, conductor y vehículo. Dashboard con gráficos, alarmas por hora, intervalos y gravedad. |
| Gráficos en Excel y línea temporal | Integrado en `main` | Gráficos editables vinculados a celdas y vista gráfica de episodios en cada sesión. |
| Formato y calidad de cámara | Integrado en `main` | Resolución y FPS solicitados, avisos de imagen tapada, desenfocada o congelada y episodios guardados durante el monitoreo. |
| Gráficos de Excel explicativos | Pendiente | Mejorar diseño, títulos, unidades, leyendas y contexto para que cada gráfico permita interpretar los datos sin depender de las tablas. |
| Salida de audio para la alarma | Pendiente | Permitir seleccionar y probar parlantes, audífonos u otra salida de reproducción disponible en Windows, guardar la preferencia y usar la salida predeterminada como respaldo. |
| Puntos faciales y apertura de ojos | Integrado en `main` | MediaPipe Face Mesh en ONNX y estimación por ojo con calibración inicial. |
| Parpadeos, cierres prolongados y PERCLOS | Integrado en `main` | Conteos, PERCLOS de 60 s en vivo y acumulado por sesión. |
| Métricas oculares avanzadas | Integrado en `main` | Parpadeos por minuto y por duración, cierres anómalos, microsueños, tiempos cerrados, diferencia entre ojos y evolución de PERCLOS. |
| Ángulos de cabeza y posibles cabeceos | Integrado en `main` | Tres ángulos en vivo y registro de descenso con retorno a la postura neutra. |
| Resto de propuestas | Pendiente | Incluye bostezos, mirada, distracción y análisis histórico avanzado. |

La medición facial fue validada de forma inicial con una cámara Windows. La calibración de umbrales y las pruebas con más cámaras, iluminación y personas siguen pendientes.
Las métricas oculares avanzadas también fueron validadas manualmente con cámara y están integradas en `main`. Su calibración con más cámaras, condiciones de iluminación y personas sigue pendiente.

## Propósito del documento

Este documento reúne las características propuestas para evolucionar FatigueDetect, tanto las planteadas originalmente como otras que pueden aportar más datos útiles sobre la atención, la fatiga y el uso del sistema.

El objetivo principal no es implementar todo de inmediato, sino identificar:

- Qué se puede desarrollar rápidamente.
- Qué características necesitan mejorar primero la detección facial.
- Qué funciones requieren pruebas y calibración con usuarios reales.
- Cómo organizar los datos, los dashboards y los reportes para que el sistema pueda crecer sin volverse difícil de mantener.

## Escala de dificultad

La dificultad es una estimación técnica basada en el estado actual del proyecto.

| Dificultad | Significado |
|---|---|
| **Baja** | Se puede construir con la información o las herramientas que el proyecto ya tiene, sin cambios profundos. |
| **Baja-media** | Es relativamente directa, pero necesita una pantalla nueva, persistencia adicional o integración con una librería. |
| **Media** | Necesita una lógica temporal más elaborada, mejores modelos de detección o pruebas con la cámara. |
| **Media-alta** | Requiere modelos más avanzados, calibración y bastantes pruebas para evitar falsos positivos. |
| **Alta** | Necesita modelos especializados, datos de entrenamiento o medidas importantes de seguridad y privacidad. |

La dificultad de programar una función y la dificultad de hacerla confiable no siempre son iguales. Por ejemplo, crear un puntaje de atención es fácil; conseguir que ese puntaje represente correctamente el estado real del conductor requiere pruebas y calibración.

---

# 1. Características propuestas inicialmente

## Detección y análisis durante una sesión

| Característica | Dificultad | Descripción sencilla |
|---|---:|---|
| Contar los parpadeos de una sesión | **Media** | Contar cuántas veces el usuario cierra y abre los ojos. Para hacerlo bien se necesitan puntos faciales y no solamente saber si el detector encontró ojos. |
| Detectar cabeceos por sueño | **Media-alta** | Identificar el movimiento involuntario en el que la cabeza cae y vuelve a levantarse. Se debe medir el ángulo vertical de la cabeza y analizar el movimiento durante varios fotogramas. |
| Medir cuánto dura cada cierre de ojos | **Media** | Registrar la duración exacta de cada parpadeo o cierre prolongado para distinguir un parpadeo normal de un posible microsueño. |
| Registrar la hora exacta de cada evento | **Baja** | Guardar cuándo comenzó y terminó cada parpadeo prolongado, cabeceo, distracción, ausencia o alarma. Esta función debería ser una de las primeras. |
| Mostrar alertas visuales en vivo | **Baja-media** | Mostrar avisos en pantalla cuando los ojos permanezcan cerrados, el usuario se distraiga, se ausente o la cámara pierda el rostro. |
| Calcular un puntaje de atención de 0 % a 100 % | **Media** | Combinar cierres de ojos, cabeceos, distracciones, ausencias y otros indicadores. La fórmula inicial puede basarse en reglas, pero deberá calibrarse con pruebas. |
| Detectar más de una persona | **Baja** | Avisar cuando aparezca otra cara detrás o al lado del conductor. El detector actual ya puede devolver más de un rostro. |
| Registrar cuando la silla quede vacía | **Baja-media** | Crear un evento cuando no se detecte al conductor durante un tiempo configurable, evitando avisos por pérdidas de uno o dos fotogramas. |
| Detectar gafas de sol, gorra o mascarilla | **Alta** | Clasificar accesorios que dificulten ver el rostro. Necesita modelos específicos y puede producir errores con iluminación difícil o accesorios poco comunes. |
| Guardar una fotografía de evidencia | **Baja técnicamente / media por privacidad** | Tomar una captura cuando ocurra un evento grave. Es fácil de programar, pero necesita consentimiento, cifrado, control de acceso, eliminación y una política de conservación. |
| Avisar si la iluminación es mala | **Baja** | Analizar brillo, contraste, sombras y sobreexposición para indicar si el rostro está demasiado oscuro o encandilado. |

## Dashboard, estadísticas y reportes

| Característica | Dificultad | Descripción sencilla |
|---|---:|---|
| Promedio de sesiones por día, semana y mes | **Baja-media** | Mostrar cuántas sesiones se realizan en diferentes periodos y comparar el periodo actual con el anterior. |
| Identificar las horas de mayor uso | **Baja** | Agrupar las sesiones por hora o por franja: mañana, tarde y noche. También se puede crear un mapa de calor por día y hora. |
| Guardar un historial completo | **Baja-media** | Pasar del CSV a una base de datos SQLite que almacene sesiones, eventos y métricas sin depender de archivos abiertos en Excel. |
| Exportar los datos a Excel | **Baja-media** | Crear un archivo `.xlsx` con sesiones, eventos y resúmenes. Exportar las tablas es sencillo; incluir gráficos editables dentro de Excel requiere más trabajo. |
| Incluir gráficos en el archivo de Excel | **Media** | Agregar tendencias, distribución de eventos y comparaciones. Hay que elegir una librería compatible y revisar sus condiciones de licencia. |
| Hacer los gráficos de Excel autoexplicativos | **Baja-media** | Mejorar títulos, etiquetas, unidades, leyendas, colores y textos de interpretación. Cada gráfico debe indicar qué representa, el periodo analizado y la conclusión principal, sin obligar a consultar otra hoja. |
| Mejorar la estética del dashboard | **Media** | Rediseñar jerarquías, colores, tarjetas, gráficos, estados vacíos y navegación. Es conveniente hacerlo después de definir qué datos se mostrarán. |
| Crear más paneles para separar la información | **Baja-media** | Dividir el monitoreo en vivo, el resumen de sesión, el análisis histórico y la configuración para evitar una pantalla sobrecargada. |

## Cámara y configuración

| Característica | Dificultad | Descripción sencilla |
|---|---:|---|
| Elegir entre cámara integrada y cámara externa | **Baja** | Enumerar las cámaras disponibles, mostrar una vista previa y recordar la cámara preferida. |
| Cambiar resolución y fotogramas por segundo | **Baja-media** | Permitir elegir calidad y rendimiento según la cámara y el equipo. |
| Probar la cámara desde Configuración | **Baja** | Abrir una vista previa antes de iniciar la sesión y mostrar si el rostro, la iluminación y la posición son adecuados. |
| Seleccionar y probar la salida de audio | **Baja-media** | Enumerar las salidas de reproducción disponibles en Windows, permitir elegir parlantes o audífonos, reproducir una alarma de prueba y recordar la selección. Si el dispositivo deja de estar disponible, usar la salida predeterminada y mostrar un aviso. |

---

# 2. Características adicionales recomendadas

## Métricas de ojos y somnolencia

| Característica | Dificultad | Descripción sencilla |
|---|---:|---|
| Parpadeos por minuto | **Media** | Integrado en `main` sobre el tiempo ocular válido. |
| Duración mínima, máxima y promedio de los parpadeos | **Media** | Integrado en `main` para parpadeos confirmados entre 60 y 400 ms. |
| Distribución de parpadeos cortos y prolongados | **Media** | Integrado en `main` con separación a 200 ms y cierres anómalos por encima de 400 ms. |
| Contar microsueños | **Media** | Integrado en `main` desde el umbral de somnolencia configurado. |
| Microsueño más largo | **Media** | Integrado en `main` junto con el cierre ocular más largo. |
| Tiempo total con los ojos cerrados | **Media** | Integrado en `main` con el criterio ocular usado por PERCLOS. |
| Porcentaje de la sesión con ojos cerrados | **Media** | Integrado en `main` sobre el tiempo ocular válido. |
| PERCLOS | **Media** | Integrado en `main` con una ventana móvil de 60 segundos y acumulado para toda la sesión. |
| Evolución del PERCLOS | **Media** | Integrado en `main` con muestras cada 10 s y al interrumpir la observación. |
| Diferencia entre ojo izquierdo y derecho | **Media** | Integrado en `main` como diferencia absoluta en vivo y promedio temporal por sesión. |
| Tiempo hasta el primer síntoma de fatiga | **Baja-media** | Medir cuánto tiempo pasó desde el inicio hasta el primer evento relevante. |

## Cabeza, postura, mirada y distracción

| Característica | Dificultad | Descripción sencilla |
|---|---:|---|
| Ángulo horizontal de la cabeza | **Media** | Medir cuánto gira el usuario hacia la izquierda o la derecha. |
| Ángulo vertical de la cabeza | **Media** | Medir cuánto mira hacia arriba o hacia abajo. Es una pieza necesaria para detectar cabeceos. |
| Inclinación lateral | **Media** | Detectar cuando la cabeza se inclina hacia un hombro. |
| Número y duración de cabeceos | **Media-alta** | Contar el patrón completo de caída y recuperación de la cabeza, no solamente una posición baja. |
| Giros de cabeza por dirección | **Media** | Indicar cuántas veces se miró a cada lado y durante cuánto tiempo. |
| Tiempo mirando fuera de la zona frontal | **Media-alta** | Calcular cuánto tiempo se mantiene la cabeza o la mirada fuera de una zona considerada segura. |
| Distracción más larga | **Media-alta** | Guardar el episodio continuo de distracción con mayor duración. |
| Porcentaje de la sesión distraído | **Media-alta** | Dividir el tiempo distraído entre el tiempo total analizado. |
| Postura encorvada | **Media-alta** | Detectar un descenso o inclinación sostenida de cabeza y hombros. Puede necesitar un modelo de postura corporal. |
| Distancia aproximada a la cámara | **Baja-media** | Usar el tamaño del rostro como aproximación para avisar si está demasiado cerca o lejos. |
| Rostro parcialmente fuera del encuadre | **Baja-media** | Avisar cuando la cara toca los bordes o no está completamente visible. |
| Detectar bostezos | **Media-alta** | Medir la apertura de la boca y su duración mediante puntos faciales. |
| Contar bostezos por hora | **Media-alta** | Crear una métrica temporal para analizar si aumentan durante la sesión. |
| Estimar desviación de mirada | **Media-alta** | Aproximar si los ojos miran al frente, abajo o a los lados. No reemplaza un equipo especializado de seguimiento ocular. |

## Calidad de detección y confiabilidad de los datos

| Característica | Dificultad | Descripción sencilla |
|---|---:|---|
| Puntaje de calidad de la imagen | **Baja-media** | Combinar brillo, contraste, nitidez, tamaño del rostro y posición en un indicador fácil de entender. |
| Porcentaje de fotogramas analizables | **Baja** | Medir en qué proporción de la sesión el rostro y los ojos estuvieron disponibles para el análisis. |
| Confianza de cada evento | **Media** | Guardar qué tan seguro está el sistema de que ocurrió un cabeceo, parpadeo o distracción. |
| Separar “atención” de “calidad de datos” | **Baja-media** | Evitar castigar al usuario por una cámara oscura o mal colocada. Un puntaje mide la atención y otro la confiabilidad de la medición. |
| Detectar cámara tapada o desenfocada | **Baja** | Avisar si la imagen es casi uniforme, está completamente oscura o perdió demasiada nitidez. |
| Detectar congelamiento de imagen | **Baja-media** | Comparar fotogramas consecutivos para identificar si la cámara dejó de actualizarse. |
| Registrar caídas de FPS | **Baja** | Medir si el equipo procesa menos fotogramas de los esperados. |
| Registrar errores de cámara | **Baja** | Guardar desconexiones, cambios de dispositivo y fallos de captura. |

## Métricas y resúmenes de sesión

| Característica | Dificultad | Descripción sencilla |
|---|---:|---|
| Alertas por hora | **Baja** | Normalizar el número de alertas según la duración de la sesión. |
| Minuto de mayor fatiga | **Baja-media** | Encontrar el periodo con más cierres, cabeceos o distracciones. |
| Tendencia de la atención | **Media** | Indicar si la atención se mantuvo estable, mejoró o empeoró. |
| Tiempo entre alertas | **Baja** | Mostrar la frecuencia con la que se repiten los episodios. |
| Tiempo de recuperación después de una alarma | **Media** | Medir cuánto tarda el usuario en volver a un estado normal después del aviso. |
| Respuesta a las alertas | **Media** | Determinar si después de una alarma el usuario abre los ojos, corrige la postura o vuelve al encuadre. |
| Resumen automático de la sesión | **Baja-media** | Generar frases como “La fatiga aumentó en los últimos 18 minutos” usando reglas basadas en los datos. |
| Eventos por gravedad | **Baja** | Clasificar eventos como informativos, leves, moderados o graves. |
| Línea temporal completa | **Baja-media** | Mostrar en un mismo gráfico cuándo ocurrieron cierres de ojos, cabeceos, ausencias, distracciones y alarmas. |

## Análisis histórico y personalización

| Característica | Dificultad | Descripción sencilla |
|---|---:|---|
| Comparar sesiones del mismo conductor | **Baja-media** | Ver si los indicadores mejoran o empeoran con el tiempo. |
| Promedio personal de parpadeos | **Media** | Crear una referencia propia, ya que cada persona tiene un patrón diferente. |
| Umbrales personalizados | **Media** | Ajustar sensibilidad según el rostro, las gafas, la cámara y el comportamiento normal del conductor. |
| Comparar sesión actual con el promedio personal | **Media** | Mostrar diferencias de PERCLOS, cabeceos, distracciones y puntaje. |
| Detectar cambios anormales | **Media-alta** | Avisar cuando una sesión se aleja considerablemente del comportamiento habitual. |
| Sesiones más seguras y más riesgosas | **Baja** | Ordenar sesiones por puntaje, alertas o gravedad. |
| Comparar por vehículo | **Baja-media** | Analizar si la fatiga o las distracciones cambian según el vehículo registrado. |
| Comparar por hora o franja del día | **Baja** | Identificar si hay más riesgo en la mañana, tarde, noche o madrugada. |
| Comparar días de la semana | **Baja** | Detectar patrones semanales de uso y fatiga. |

## Seguridad biométrica y prueba de vida

| Característica | Dificultad | Descripción sencilla |
|---|---:|---|
| Pedir que el usuario gire la cabeza | **Media** | Presentar una instrucción aleatoria y comprobar el movimiento. Detiene fotografías estáticas. |
| Pedir que el usuario parpadee | **Media** | Verificar un parpadeo después de mostrar la instrucción. |
| Combinar varias instrucciones aleatorias | **Media-alta** | Pedir secuencias diferentes para dificultar suplantaciones sencillas. |
| Detectar una fotografía o pantalla | **Alta** | Usar un modelo antisuplantación que analice textura, reflejos y profundidad aparente. |
| Detectar reproducción de video | **Alta** | Requiere una prueba de vida más robusta; los movimientos básicos no son suficientes. |
| Cifrar perfiles biométricos | **Media** | Proteger los descriptores almacenados y limitar quién puede leerlos. |
| Política de eliminación y conservación | **Baja-media** | Permitir borrar perfiles y definir cuánto tiempo se conservan evidencias y datos personales. |

---

# 3. Qué puede implementarse rápidamente

Las siguientes funciones ofrecen mucho valor y no dependen todavía de un modelo facial avanzado:

1. Guardar las sesiones y eventos en SQLite.
2. Registrar inicio, fin, duración y hora exacta de cada evento.
3. Detectar ausencia prolongada del conductor y avisarla en vivo. **Integrado en `main` mediante el PR #5.**
4. Avisar si aparecen varias personas. **Integrado en `main` mediante el PR #5.**
5. Analizar si la imagen está oscura o sobreexpuesta y mostrar avisos en la vista previa y durante el monitoreo.
6. Permitir seleccionar la cámara, guardar la preferencia y reutilizarla en biometría y monitoreo.
7. Mostrar alertas visuales en vivo para las condiciones que todavía no se muestran. **Ausencia y varios rostros integrados en `main`; las alertas de futuros detectores siguen pendientes.**
8. Registrar calidad de imagen, FPS y porcentaje de fotogramas analizables.
9. Crear estadísticas de sesiones por día, semana, mes, hora y día de la semana.
10. Crear un dashboard histórico inicial.
11. Exportar tablas y resúmenes básicos a Excel.
12. Mostrar una línea temporal de alarmas, ausencias y problemas de cámara.
13. Mejorar los gráficos de Excel con títulos, etiquetas, unidades y explicaciones que permitan entenderlos por sí solos.
14. Permitir seleccionar parlantes u otra salida de audio y reproducir una alarma de prueba desde Configuración.

Estas características crean la infraestructura necesaria para añadir después métricas más avanzadas sin tener que rehacer el almacenamiento y los dashboards.

---

# 4. Mejora técnica necesaria para ojos, cabeza y mirada

El proyecto usa Haar para localizar candidatos de rostro y ejecuta MediaPipe Face Mesh en ONNX para medir los párpados y estimar la pose de la cabeza. El detector anterior localizaba los ojos mediante Haar.

Para medir parpadeos, cabeceos, bostezos, postura y mirada conviene incorporar un modelo de **puntos faciales** o *facial landmarks*. Este modelo permite localizar de manera estable:

- Bordes de los ojos.
- Pupilas o región del iris, dependiendo del modelo.
- Cejas.
- Nariz.
- Boca.
- Contorno del rostro.

Con esos puntos se pueden calcular:

- Apertura de cada ojo.
- Apertura de la boca.
- Posición y ángulo de la cabeza.
- Dirección aproximada de la mirada.
- Movimiento del rostro entre fotogramas.

Una opción apropiada para el proyecto sería ejecutar un modelo ONNX localmente desde .NET. De esa forma no se envían imágenes a servicios externos y el sistema puede seguir funcionando sin conexión a internet.

---

# 5. Límites que deben quedar claros

## Prueba de vida

Pedir “gira la cabeza a la derecha” o “parpadea” evita que alguien use una fotografía impresa o una imagen estática. Sin embargo, un video preparado podría superar esa prueba. Por eso hay dos niveles diferentes:

- **Prueba de vida básica:** instrucciones aleatorias de movimiento o parpadeo.
- **Antisuplantación robusta:** modelo especializado para fotografías, pantallas y videos.

La primera es razonable para una versión académica o experimental. La segunda necesita bastante más trabajo y pruebas.

## Estimación de mirada

Una cámara web puede estimar si una persona mira al frente, hacia abajo o hacia los lados. No puede determinar con total precisión qué objeto está mirando como lo haría un equipo especializado de seguimiento ocular.

La precisión depende de:

- Resolución de la cámara.
- Posición de la cámara.
- Distancia al usuario.
- Iluminación.
- Gafas y reflejos.
- Calibración individual.

## Puntaje de atención

El puntaje no debería presentarse como una verdad médica. Es un indicador calculado por el sistema. Debe mostrar también qué factores lo redujeron y cuál fue la calidad de los datos.

Ejemplo:

```text
Atención final: 76 %
Factores principales:
- 2 cierres prolongados
- 4 cabeceos
- 38 segundos de distracción
- Calidad de medición: 91 %
```

## Fotografías de evidencia

Guardar fotografías puede ser útil, pero implica datos personales sensibles. Si se habilita esta función, debería incluir:

- Consentimiento explícito.
- Opción para desactivarla.
- Cifrado de los archivos.
- Tiempo máximo de conservación.
- Eliminación desde la aplicación.
- Acceso restringido.
- Registro de quién consultó o exportó las evidencias, si el sistema llega a tener múltiples usuarios.

---

# 6. Dashboards recomendados

No es conveniente mostrar todo en una sola pantalla. La interfaz sería más clara si la información se divide según el momento y el propósito de uso.

## Dashboard 1: Monitoreo en vivo

Este sería el panel principal durante la sesión.

### Información recomendada

- Video en vivo.
- Estado del rostro: detectado, ausente, parcialmente visible o varias personas.
- Estado de los ojos: abiertos, cerrados o detección poco confiable.
- Estado de la cabeza: frontal, girada, inclinada o posible cabeceo.
- Calidad de iluminación.
- Calidad general de la detección.
- Atención actual.
- Parpadeos por minuto.
- PERCLOS del último minuto.
- Tiempo del cierre ocular actual.
- Cantidad de cabeceos.
- Tiempo distraído.
- Tiempo fuera del encuadre.
- FPS de procesamiento.
- Línea temporal corta con los últimos eventos.
- Alertas visibles con colores y niveles de gravedad.

### Diseño sugerido

- Video grande a la izquierda.
- Tarjetas de métricas a la derecha.
- Indicador de atención visible, pero sin ocupar toda la pantalla.
- Línea temporal en la parte inferior.
- Colores consistentes: normal, advertencia y riesgo.
- Mensajes específicos como “mejora la iluminación” en vez de un simple “error”.

## Dashboard 2: Resumen de la sesión

Se mostraría al detener el monitoreo.

### Información recomendada

- Duración total.
- Puntaje final de atención.
- Puntaje de calidad de los datos.
- Parpadeos y parpadeos por minuto.
- PERCLOS promedio y máximo.
- Cierres prolongados y microsueños.
- Cabeceos.
- Bostezos.
- Distracciones.
- Ausencias.
- Tiempo con varias personas en pantalla.
- Alarmas activadas.
- Minuto de mayor riesgo.
- Evento más largo de cada categoría.
- Gráfico de atención durante la sesión.
- Línea temporal completa.
- Lista cronológica de eventos.
- Evidencias asociadas, si están habilitadas.
- Resumen automático en lenguaje natural.

## Dashboard 3: Análisis histórico

Este panel serviría para estudiar el uso y la evolución del conductor.

### Información recomendada

- Sesiones por día, semana y mes.
- Duración promedio.
- Horas de mayor uso.
- Días de mayor uso.
- Mapa de calor por día y hora.
- Evolución del puntaje de atención.
- Evolución del PERCLOS.
- Evolución de parpadeos, cabeceos y distracciones.
- Alarmas por hora de conducción.
- Comparación del periodo actual con el anterior.
- Sesiones de mayor y menor riesgo.
- Filtros por conductor, fecha, vehículo, hora y gravedad.

## Dashboard 4: Perfil del conductor

Este panel reuniría datos propios de cada persona.

### Información recomendada

- Datos personales y vehículo habitual.
- Estado del perfil biométrico.
- Fecha de la última actualización biométrica.
- Promedio personal de parpadeos.
- PERCLOS habitual.
- Umbrales personalizados.
- Historial de puntuaciones.
- Tendencia de atención.
- Sesiones más seguras y más riesgosas.
- Comparación con el promedio personal.
- Gestión y eliminación del perfil biométrico.

## Dashboard 5: Calidad y configuración

Este panel ayudaría a configurar el sistema y a entender por qué una detección puede fallar.

### Información recomendada

- Cámara seleccionada.
- Vista previa de cámara.
- Resolución y FPS.
- Nivel de iluminación.
- Nitidez y contraste.
- Tamaño y posición del rostro.
- Porcentaje de fotogramas analizables.
- Umbral de ojos cerrados.
- Umbral de ausencia.
- Umbral de distracción.
- Sensibilidad de cabeceos.
- Configuración de sonidos y alertas.
- Selector de salida de audio de Windows para parlantes o audífonos, con prueba de alarma y respaldo en el dispositivo predeterminado.
- Configuración de evidencias.
- Tiempo de conservación de datos.
- Prueba de cámara y altavoz.

---

# 7. Estructura de datos recomendada

El archivo CSV actual es suficiente para un historial pequeño, pero se vuelve limitado cuando hay que guardar eventos, filtros, gráficos y métricas temporales. La opción recomendada es **SQLite** porque funciona de manera local, no necesita instalar un servidor y se integra bien con .NET.

## Tabla `Conductores`

Información básica del usuario.

```text
Id
Cedula
Nombre
Apellidos
FechaRegistro
FechaActualizacion
```

## Tabla `Vehiculos`

Permite que una persona pueda tener uno o varios vehículos.

```text
Id
ConductorId
TipoCarro
Placa
Activo
```

## Tabla `PerfilesBiometricos`

Guarda la referencia biométrica sin almacenar fotografías de registro.

```text
Id
ConductorId
DescriptorCifrado
VersionModelo
FechaRegistro
FechaActualizacion
```

Guardar la versión del modelo es importante porque un descriptor creado por un modelo puede no ser compatible con otro modelo futuro.

## Tabla `Sesiones`

Contiene el resumen general de cada uso.

```text
Id
ConductorId
VehiculoId
Inicio
Fin
DuracionSegundos
PuntajeAtencion
PuntajeCalidad
CantidadAlarmas
Observaciones
```

## Tabla `Eventos`

Cada cambio relevante se guarda como un evento.

```text
Id
SesionId
Tipo
Inicio
Fin
DuracionMilisegundos
Gravedad
Confianza
ValorNumerico
Unidad
RutaEvidenciaOpcional
```

Ejemplo conceptual:

```text
Tipo: Cabeceo
Inicio: 2026-11-18 21:14:32.180
Fin: 2026-11-18 21:14:33.420
Duración: 1.24 segundos
Gravedad: Alta
Confianza: 91 %
```

## Tabla `MetricasTemporales`

No es necesario guardar todos los fotogramas. Se pueden guardar resúmenes cada pocos segundos o cada minuto.

```text
Id
SesionId
FechaHora
Atencion
CalidadDeteccion
Perclos
ParpadeosPorMinuto
AnguloCabezaHorizontal
AnguloCabezaVertical
TiempoDistraidoAcumulado
Fps
Brillo
```

## Tabla `Configuracion`

Permite conservar preferencias y umbrales.

```text
Clave
Valor
FechaActualizacion
```

## Tabla `Evidencias`

Solo sería necesaria si se activa la captura de imágenes.

```text
Id
EventoId
RutaCifrada
FechaCaptura
FechaEliminacionProgramada
HashIntegridad
```

## Ventajas de esta estructura

- Permite añadir nuevos tipos de evento sin cambiar todas las sesiones anteriores.
- Facilita filtros y gráficos.
- Evita crear columnas interminables en un CSV.
- Permite exportar solo el periodo seleccionado.
- Separa datos personales, sesiones, eventos y evidencias.
- Facilita borrar los datos de un conductor.
- Permite actualizar el modelo biométrico sin perder el historial.

---

# 8. Motor central de eventos

Antes de añadir muchos detectores conviene crear una pieza central que reciba observaciones y genere eventos.

Por ejemplo, el detector podría informar en cada fotograma:

```text
Rostro detectado: sí
Cantidad de rostros: 1
Ojo izquierdo: 32 % abierto
Ojo derecho: 29 % abierto
Ángulo vertical de cabeza: 18°
Iluminación: adecuada
Confianza: 88 %
```

El motor temporal sería responsable de decidir:

- Cuándo comenzó un cierre de ojos.
- Cuándo terminó.
- Si fue un parpadeo normal o un microsueño.
- Si un movimiento completo corresponde a un cabeceo.
- Si una ausencia fue suficientemente larga para guardarse.
- Cuándo debe activarse o detenerse una alerta.
- Qué gravedad y confianza debe tener el evento.

Esta separación evita que la interfaz, la cámara y las reglas de negocio terminen mezcladas en el mismo código.

---

# 9. Orden recomendado de implementación

## Etapa 1: Base de datos y eventos

Prioridad alta porque desbloquea los dashboards y evita rehacer el almacenamiento después.

1. Integrar SQLite para sesiones y eventos.
2. Importar una sola vez las filas válidas del historial CSV.
3. Guardar inicio, fin y duración de los eventos disponibles.
4. Registrar cierres oculares prolongados, alarmas, rostro ausente, varios rostros y condiciones de iluminación.
5. Permitir seleccionar y probar la cámara, guardar la preferencia y compartirla entre biometría y monitoreo.
6. Mostrar avisos de oscuridad y sobreexposición en la vista previa y durante el monitoreo.
7. Mostrar avisos en vivo de ausencia o varios rostros y resumir los eventos en el historial. **Integrado en `main` mediante el PR #5.**
8. Registrar FPS, porcentaje de fotogramas analizables, tiempo con rostro válido y fallos de cámara. **Integrado en `main`.**
9. Solicitar resolución y FPS, y detectar imágenes tapadas, desenfocadas o congeladas. **Integrado en `main`.**

## Etapa 2: Dashboard inicial

1. Rediseñar el monitoreo en vivo.
2. Ampliar el resumen de sesión con las futuras métricas de calidad, fatiga y atención. El resumen básico al detener el monitoreo está integrado en `main`.
3. Crear el análisis histórico básico con duración promedio. **Integrado en `main`.**
4. Añadir filtros por fecha, conductor y vehículo. **Integrado en `main`.**
5. Mostrar sesiones por día, semana y mes. **Integrado en `main`.**
6. Mostrar horas de mayor uso. **Integrado en `main`.**
7. Exportar sesiones, eventos y resúmenes a Excel. **Integrado en `main`.** Los gráficos editables de sesiones, alarmas y gravedad están implementados.
8. Mostrar alarmas por hora, tiempo entre alarmas, gravedad y línea temporal de la sesión. **Integrado en `main`.**
9. Mejorar los gráficos de Excel para que incluyan contexto, unidades, leyendas y conclusiones comprensibles sin consultar las tablas. **Pendiente.**
10. Seleccionar y probar la salida de audio para la alarma, conservar la preferencia y usar la salida predeterminada como respaldo. **Pendiente.**

## Etapa 3: Puntos faciales y métricas de fatiga

1. Integrar un modelo de 468 puntos faciales en ONNX. **Integrado en `main`.**
2. Medir la apertura de cada ojo. **Integrado en `main`.**
3. Contar parpadeos. **Integrado en `main`.** Incluye frecuencia, clasificación y duraciones.
4. Medir cierres prolongados. **Integrado en `main`.** Incluye microsueños y cierres anómalos.
5. Calcular PERCLOS de 60 s y por sesión. **Integrado en `main`.** Incluye su evolución temporal.
6. Estimar ángulos de cabeza. **Integrado en `main`.**
7. Detectar posibles cabeceos. **Integrado en `main`.**
8. Detectar bostezos.
9. Estimar distracción y mirada.

## Etapa 4: Puntajes y personalización

1. Crear el puntaje de calidad de detección.
2. Crear una primera fórmula de atención.
3. Mostrar las razones del puntaje.
4. Crear promedios personales.
5. Permitir calibración por conductor.
6. Comparar sesiones y detectar tendencias.

## Etapa 5: Seguridad y funciones avanzadas

1. Añadir una prueba de vida básica.
2. Probar un modelo antisuplantación.
3. Cifrar perfiles biométricos y evidencias.
4. Añadir detección de accesorios.
5. Añadir evidencias opcionales y su política de conservación.
6. Crear reportes avanzados con gráficos.

---

# 10. Prioridades sugeridas

## Mayor valor con menor dificultad

- SQLite.
- Registro detallado de eventos.
- Ausencias.
- Varias personas.
- Iluminación.
- Selección de cámara.
- Estadísticas de uso.
- Dashboard histórico inicial.
- Alertas visuales.
- Exportación básica.

## Mayor valor, pero requieren mejorar el detector

- Parpadeos.
- Duración de cierre ocular.
- PERCLOS.
- Microsueños.
- Cabeceos.
- Bostezos.
- Ángulos de cabeza.
- Distracción.
- Puntaje de atención.

## Deben dejarse para una etapa avanzada

- Antisuplantación robusta.
- Detección confiable de accesorios.
- Mirada de alta precisión.
- Fotografías de evidencia en entornos reales.
- Clasificaciones que pretendan tener validez médica.

---

# 11. Recomendación principal

La mejor base para continuar el proyecto es trabajar primero en estas tres piezas:

1. **SQLite para almacenar los datos.**
2. **Un motor central de eventos temporales.**
3. **Un modelo de puntos faciales para ojos, boca y posición de cabeza.**

SQLite permite consultar y representar la información. El motor de eventos convierte las mediciones de la cámara en datos útiles. Los puntos faciales desbloquean parpadeos, PERCLOS, cabeceos, bostezos y distracciones.

Después de esas tres piezas, la mayoría de los dashboards y reportes se convierten en una forma de consultar y visualizar información que el sistema ya está generando, en lugar de implementar cada pantalla como una función aislada.

El objetivo final debería ser que cada sesión produzca tres resultados claramente separados:

- **Qué ocurrió:** eventos y línea temporal.
- **Qué significa:** métricas, tendencias y puntaje de atención.
- **Qué tan confiable fue:** calidad de imagen, disponibilidad del rostro y confianza de las detecciones.

Esta separación hará que FatigueDetect sea más útil, más explicable y mucho más fácil de mejorar con el tiempo.
