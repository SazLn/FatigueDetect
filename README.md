# Sistema de Detección de Fatiga al Volante - 4 proyectos (capas) con interfaz nueva

Abrir `FatigueDetect.sln` en Visual Studio 2022 (.NET 8 SDK) y ejecutar con F5.
Proyecto de inicio: `Presentacion`.

| Proyecto     | Tipo                     | Referencias              | Paquetes NuGet                        |
|--------------|--------------------------|--------------------------|---------------------------------------|
| Entidades    | Biblioteca de clases     | -                        | -                                     |
| Datos        | Biblioteca de clases     | Entidades                | OpenCvSharp4, OpenCvSharp4.Extensions, Microsoft.Data.Sqlite, Microsoft.ML.OnnxRuntime |
| Logica       | Biblioteca de clases     | Entidades, Datos         | OpenCvSharp4                          |
| Presentacion | Aplicacion Windows Forms | Logica, Datos, Entidades | OpenCvSharp4.runtime.win, ClosedXML   |

## Pantallas (menu lateral)
- Registro / Persona: cedula, nombre, apellidos, tipo de carro (editable) y placa (libre).
- Inicio: cámara en vivo, estado del conductor, gráfica, nivel de atención y avisos de iluminación o problemas de encuadre.
- Historial de sesiones: sesiones guardadas, métricas de calidad, resumen por tipo, cronología de eventos, estadísticas y exportación a Excel.
- Configuración: sensibilidad de detección, sonido de la alarma y selector de cámara con vista previa.

## Archivos guardados en Documentos\FatigueDetect\
- historial.sqlite (sesiones, eventos y métricas de calidad guardados localmente)
- historial.csv (historial de versiones anteriores, si existe)
- tipos_carro.txt
- camara_preferida.txt (cámara, resolución y FPS solicitados para este equipo)
- configuracion.json (umbrales de somnolencia y alarma, y estado del sonido)
- perfiles_biometricos.json (datos del conductor y descriptores numéricos del rostro; no contiene fotografías)

## Elegir la cámara y revisar la iluminación

En **Configuración → Cámara**, elige la cámara por el nombre que muestra Windows o selecciona **Automática**.
Pulsa **Actualizar lista** después de conectar una cámara externa y **Ver vista previa** para comprobar
el encuadre y la iluminación. La selección se guarda al cambiarla y se usa tanto para la captura
facial como para el monitoreo. Si la cámara guardada se desconecta, vuelve a conectarla o selecciona otra.
Puedes cambiar la cámara, la resolución y los FPS entre sesiones. La vista previa muestra el tamaño recibido y, si el controlador los informa, los FPS negociados. Si la cámara admite otro formato, puede usarlo aunque hayas seleccionado un valor distinto. La frecuencia medida durante el monitoreo aparece en el historial.

En **Configuración**, usa **Guardar cambios** para aplicar los umbrales de detección
y el sonido. Los valores se conservan entre ejecuciones. **Restablecer** aplica y
guarda los valores predeterminados (1,5 s para somnolencia, 3 s para alarma y sonido activo).

La vista previa indica si la imagen está oscura, sobreexpuesta o bien iluminada. En **Inicio**,
el aviso aparece debajo del video cuando la imagen permanece demasiado oscura o sobreexpuesta
durante al menos dos segundos. Desaparece al corregir la iluminación. La estimación usa el brillo
promedio de la imagen y el episodio se guarda en el historial al terminar la sesión.

Durante el monitoreo también aparece un aviso si el rostro permanece ausente o si se
detectan varias personas durante al menos dos segundos. El aviso desaparece cuando
vuelve a detectarse únicamente al conductor. Si la cámara deja de entregar imagen,
la interfaz muestra que no hay señal y limpia el aviso anterior.

La vista previa y el monitoreo avisan si la imagen parece tapada o desenfocada durante dos segundos, o si las muestras de imagen se repiten durante tres segundos. Estas señales se estiman por oscuridad y contraste, nitidez y repetición. Un encuadre oscuro, uniforme o completamente estático puede dar un falso aviso. Durante el monitoreo se guardan los episodios confirmados y los cuadros sospechosos quedan fuera del análisis de fatiga.

## Historial y registro de eventos

Al pulsar **Detener monitoreo**, aparece el resumen final de la sesión con su duración,
el número de alarmas, los episodios sin rostro detectado y los demás eventos destacados
agrupados por tipo.

Al detener el monitoreo o cerrar la aplicación normalmente, se guardan la sesión y sus
eventos en una sola operación de SQLite. En **Historial de sesiones**, selecciona una fila
para ver los eventos en orden, con su hora de inicio y duración. SQLite conserva el inicio
y el fin en UTC con precisión de milisegundos y la interfaz muestra la hora local.
El detalle incluye un resumen por tipo con la cantidad de eventos, su duración total
y la duración máxima disponible. Los eventos importados sin duración se indican por separado.

- **Posible microsueño.** Se registra cuando la apertura estimada de ambos ojos permanece bajo el umbral
  durante el tiempo de somnolencia configurado. El evento termina al reabrir los ojos o al interrumpirse
  la observación.
- **Posible cabeceo.** Se registra al completar un descenso de la cabeza y volver cerca de la postura calibrada.
- **Alarma.** Se registra una vez por activación, aunque el sonido esté desactivado.
- **Rostro no detectado, varios rostros, luz insuficiente o luz excesiva.** Se registran cuando
  la condición dura al menos dos segundos. La iluminación se estima con el brillo promedio de la imagen.

Pausar el monitoreo, perder la imagen o detener la sesión cierra los eventos activos.
Los eventos se guardan al terminar la sesión, por lo que un cierre forzado puede perder la sesión en curso.

En el primer acceso al historial se importan las filas válidas de `historial.csv`, si existe,
y las horas de alarma disponibles. Los registros antiguos conservan su conteo original y
las alarmas importadas tienen duración desconocida. El CSV se conserva como respaldo.
Al usar **Eliminar historial**, se borran las sesiones, sus eventos y sus métricas en SQLite. El CSV
se conserva y la importación no vuelve a ejecutarse.

El selector de cámara y los avisos de iluminación forman parte de `main` desde la integración del PR #3.
Consulta el [plan de características](docs/plan_caracteristicas_fatiguedetect.md)
y el [registro de cambios](CHANGELOG.md) para ver el estado actual y las funciones pendientes.

## Medición facial de fatiga

El monitoreo ejecuta localmente el modelo MediaPipe Face Mesh de 468 puntos convertido a ONNX. El modelo
se incluye en `FatigueDetect/Presentacion/Resources/modelos/` y Visual Studio lo copia al compilar.
La app conserva en SQLite los conteos y tiempos acumulados de la sesión y los eventos confirmados de cierre
prolongado y cabeceo. Las coordenadas faciales y los ángulos por fotograma se usan en memoria.

1. En **Inicio**, mira al frente con luz adecuada, la cabeza estable y ambos ojos abiertos hasta que aparezca
   la apertura de cada ojo. La calibración inicial requiere unos 1,5 s de observación continua.
2. Verifica la apertura en vivo y parpadea con naturalidad. Se cuenta un parpadeo si ambos ojos pasan de
   abiertos a cerrados y vuelven a abrirse entre 60 y 400 ms. Los cierres prolongados usan el tiempo de somnolencia configurado.
3. Inclina la cabeza hacia abajo y vuelve al frente. Un descenso de al menos 15° que retorna a 6° de la referencia
   entre 150 ms y 1,5 s cuenta como un posible cabeceo. En vivo aparecen los ángulos vertical, horizontal y lateral en grados
   respecto a la postura frontal de la calibración.
4. Detén el monitoreo y revisa el resumen, la línea temporal y la hoja **Sesiones** del Excel. Las sesiones anteriores
   conservan sus eventos y muestran las nuevas métricas faciales como «sin medición».

PERCLOS se estima como el porcentaje de tiempo ocular válido con ambos ojos al 20 % o menos de apertura estimada.
La apertura se calcula a partir de la distancia entre párpados respecto al ancho de cada ojo, con una referencia
abierta calibrada y una cerrada aproximada. En vivo se usa una ventana móvil de 60 s y en el historial toda la sesión.
Se incluyen los intervalos válidos de parpadeo que cumplen el umbral. El cálculo requiere 2 s válidos y excluye
pausas, mediciones inválidas y separaciones mayores a 250 ms entre fotogramas. La pose usa una focal aproximada.
La validación en una cámara Windows real confirmó el funcionamiento básico. La calibración de umbrales requiere más
pruebas con distintas cámaras, iluminación y personas. La procedencia y la conversión se detallan en
[la ficha del modelo](FatigueDetect/Presentacion/Resources/modelos/ORIGEN.md).

### Métricas oculares avanzadas

La aplicación amplía cada sesión con estas mediciones:

- Frecuencia de parpadeos por minuto de tiempo ocular válido.
- Duración mínima, promedio y máxima de los parpadeos.
- Parpadeos cortos de 60 a 200 ms y prolongados de más de 200 a 400 ms.
- Cierres anómalos de más de 400 ms que no alcanzan el umbral de microsueño.
- Microsueños desde el umbral de somnolencia configurado, cierre más largo y microsueño más largo.
- Tiempo total y porcentaje del tiempo ocular válido con ambos ojos cerrados.
- Diferencia promedio entre la apertura estimada del ojo izquierdo y derecho.
- Evolución de PERCLOS con muestras cada 10 s y al interrumpir la observación.

Las métricas se muestran en vivo y en el resumen final, se guardan en SQLite y aparecen en el historial y Excel.
La exportación añade las columnas avanzadas a **Sesiones** y una hoja **Evolución PERCLOS**. Las sesiones anteriores
conservan las métricas disponibles y muestran el detalle nuevo como «sin medición». La validación manual con cámara
confirmó el funcionamiento de estas métricas. La clasificación y los umbrales aún deben calibrarse con distintas
cámaras, condiciones de iluminación y personas.

## Salida de audio de la alarma

En **Configuración → Salida de audio de la alarma**, elige una salida de Windows, como parlantes, audífonos o Bluetooth. **Probar** reproduce la alarma durante dos segundos en la salida elegida. **Actualizar** vuelve a consultar los dispositivos y **Guardar cambios** conserva la selección. Si la salida guardada falla durante el monitoreo, el programa intenta reproducir el sonido en la salida predeterminada de Windows.

## Calidad de sesión, estadísticas y Excel

El detalle de una sesión nueva muestra:

- **FPS medios.** Fotogramas procesados divididos por el tiempo de monitoreo activo. Los intervalos sin señal cuentan como tiempo activo. Las pausas quedan fuera.
- **Fotogramas analizables.** Porcentaje de fotogramas procesados con exactamente un rostro detectado e iluminación adecuada.
- **Tiempo con rostro válido.** Suma de los intervalos entre fotogramas consecutivos con exactamente un rostro. Las pausas y los fallos de lectura interrumpen el intervalo.
- **Fallos de cámara.** Una lectura vacía o fallida inicia un episodio. Las lecturas fallidas consecutivas se cuentan como un solo episodio hasta que vuelve la imagen o se pausa el monitoreo. La medición empieza cuando la cámara abre correctamente.

Las sesiones anteriores conservan sus datos y muestran «Calidad: sin medición».

El monitoreo muestra los FPS de procesamiento en ventanas de al menos dos segundos y avisa cuando bajan de 12 FPS. Las ventanas bajas consecutivas cuentan como un solo episodio. La pausa excluye ese tiempo del cálculo. El puntaje de calidad (0–100) combina fotogramas analizables (45 %), cobertura facial (35 %) y la proporción del tiempo evaluado con FPS suficientes (20 %). Las sesiones previas conservan sus métricas, con el nuevo puntaje vacío cuando falta la medición de estabilidad.

El resumen final muestra el tiempo con FPS bajos, la cobertura y un indicador ocular de atención (1 − PERCLOS). El indicador expresa la proporción del tiempo ocular válido con ojos abiertos. Su cobertura indica cuánto de la sesión activa pudo evaluarse.

En **Historial de sesiones**, filtra por fecha inicial y final inclusivas, nombre o cédula, placa o tipo de vehículo, presencia de alarmas, puntaje y caídas de FPS. La tabla, las estadísticas y el Excel usan esa selección. En **Estadísticas → Comparar períodos**, compara el rango elegido o una ventana de 7, 30 o 90 días con un período anterior de igual duración. Ambos períodos aplican los mismos filtros de conductor, vehículo, alarmas y calidad. Selecciona una sesión y pulsa **Ver línea temporal** para ver la hora y duración de cada episodio. Las alarmas antiguas sin hora se indican con un recuento.

En **Historial de sesiones → Estadísticas**, consulta el total de sesiones y su duración promedio, con desgloses por día, semana, mes y hora de inicio. Se usa la hora local, las semanas comienzan el lunes y las franjas horarias se ordenan por número de sesiones. El promedio usa las sesiones con hora de finalización. Las alarmas por hora se cuentan según su hora local de inicio. El intervalo entre alarmas usa inicios consecutivos dentro de una misma sesión con todas sus horas registradas. La gravedad es **alta** para alarmas, **media** para posibles cierres prolongados y problemas de cámara, y **baja** para avisos de encuadre o iluminación.

En **Historial de sesiones → Exportar Excel**, elige dónde guardar el archivo .xlsx. Contiene el resumen general, las sesiones con métricas de calidad y fatiga, la evolución de PERCLOS, los eventos con hora, el resumen de eventos por sesión y los desgloses históricos. Los ID de sesión vinculan las hojas. El libro incluye gráficos editables de sesiones, alarmas por hora y eventos por gravedad, vinculados a los datos de sus hojas. Los gráficos incluyen títulos, unidades, leyendas, contexto y conclusiones calculadas a partir de los datos. Los gráficos temporales muestran hasta 24 periodos recientes con sesiones, en orden cronológico. La tabla conserva todos los periodos. Las celdas correspondientes a valores desconocidos quedan vacías. El archivo incluye los datos personales que aparecen en el historial.

## Identificación biométrica experimental

La pantalla `Registro / Persona` permite registrar el rostro del conductor y, en usos posteriores,
identificarlo para completar automáticamente sus datos:

1. Primera vez: completa cédula, nombre, apellidos y vehículo; pulsa **Registrar rostro** y acepta
   el consentimiento local. Mira a la cámara y gira ligeramente la cabeza hasta obtener 5 capturas.
2. Siguientes veces: pulsa **Identificarme**. Al obtener 3 capturas válidas, el sistema compara el
   descriptor con los perfiles guardados y rellena el formulario si la coincidencia es suficiente.
3. El enlace **Eliminar perfil biométrico** borra el descriptor asociado a la cédula indicada.

La captura rechaza imágenes demasiado oscuras, sobreexpuestas o borrosas y exige que aparezca una
sola cara. La cámara se libera al terminar para que el monitor de fatiga pueda abrirla después.

> Esta implementación LBPH local es un prototipo para pruebas, no un mecanismo de autenticación
> certificado. No incluye todavía una prueba de vida robusta y puede ser vulnerable a una fotografía.
> Siempre se ofrece confirmación manual cuando la identificación no se ha completado.
