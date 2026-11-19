# Sistema de Detección de Fatiga al Volante - 4 proyectos (capas) con interfaz nueva

Abrir `FatigueDetect.sln` en Visual Studio 2022 (.NET 8 SDK) y ejecutar con F5.
Proyecto de inicio: `Presentacion`.

| Proyecto     | Tipo                     | Referencias              | Paquetes NuGet                        |
|--------------|--------------------------|--------------------------|---------------------------------------|
| Entidades    | Biblioteca de clases     | -                        | -                                     |
| Datos        | Biblioteca de clases     | Entidades                | OpenCvSharp4, OpenCvSharp4.Extensions, Microsoft.Data.Sqlite |
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
- camara_preferida.txt (selección de cámara para este equipo)
- configuracion.json (umbrales de somnolencia y alarma, y estado del sonido)
- perfiles_biometricos.json (datos del conductor y descriptores numéricos del rostro; no contiene fotografías)

## Elegir la cámara y revisar la iluminación

En **Configuración → Cámara**, elige la cámara por el nombre que muestra Windows o selecciona **Automática**.
Pulsa **Actualizar lista** después de conectar una cámara externa y **Ver vista previa** para comprobar
el encuadre y la iluminación. La selección se guarda al cambiarla y se usa tanto para la captura
facial como para el monitoreo. Si la cámara guardada se desconecta, vuelve a conectarla o selecciona otra.
Puedes cambiar la cámara entre sesiones.

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

- **Posible cierre ocular prolongado.** Se registra desde que el detector deja de encontrar ambos
  ojos hasta que los vuelve a detectar o se interrumpe el seguimiento, siempre que llegue al
  umbral de somnolencia o alarma. La medición depende del detector de ojos actual.
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

## Calidad de sesión, estadísticas y Excel

El detalle de una sesión nueva muestra:

- **FPS medios.** Fotogramas procesados divididos por el tiempo de monitoreo activo. Los intervalos sin señal cuentan como tiempo activo. Las pausas quedan fuera.
- **Fotogramas analizables.** Porcentaje de fotogramas procesados con exactamente un rostro detectado e iluminación adecuada.
- **Tiempo con rostro válido.** Suma de los intervalos entre fotogramas consecutivos con exactamente un rostro. Las pausas y los fallos de lectura interrumpen el intervalo.
- **Fallos de cámara.** Una lectura vacía o fallida inicia un episodio. Las lecturas fallidas consecutivas se cuentan como un solo episodio hasta que vuelve la imagen o se pausa el monitoreo. La medición empieza cuando la cámara abre correctamente.

Las sesiones anteriores conservan sus datos y muestran «Calidad: sin medición».

En **Historial de sesiones → Estadísticas**, consulta el total de sesiones y su duración promedio, con desgloses por día, semana, mes y hora de inicio. Se usa la hora local, las semanas comienzan el lunes y las franjas horarias se ordenan por número de sesiones. El promedio usa las sesiones con hora de finalización.

En **Historial de sesiones → Exportar Excel**, elige dónde guardar el archivo .xlsx. Contiene el resumen general, las sesiones con métricas de calidad, los eventos con hora, el resumen de eventos por sesión y los desgloses históricos. Los ID de sesión vinculan las hojas. Las celdas correspondientes a valores desconocidos quedan vacías. El archivo incluye los datos personales que aparecen en el historial.

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
