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

FatigueDetect cifra los perfiles biométricos y los identificadores personales del
historial. La clave local se guarda en `clave_datos.bin` protegida por el usuario actual de Windows; no debe
eliminarse por separado. En **Configuración → Protección y conservación de datos** se puede activar una
retención entre 30 y 3650 días, crear respaldos `.fdbak` con contraseña y restaurarlos. El respaldo incluye
una clave portátil dentro del contenedor cifrado, por lo que puede restaurarse con la contraseña. La
restauración valida el archivo y la base SQLite y requiere reiniciar la aplicación. Consulta
[Protección de datos](docs/proteccion_datos.md).
La creación del respaldo usa una instantánea consistente de SQLite, por lo que funciona aunque la aplicación
mantenga conexiones internas abiertas y no copia directamente un archivo de base de datos en uso.

## Elegir la cámara y revisar la iluminación

### Interfaz adaptable de Configuración

La rama `ui/mejoras-graficas` inicia la renovación visual con la pantalla **Configuración**. En ventanas
amplias presenta sensibilidad y protección de datos a la izquierda y la cámara a la derecha; cuando el
ancho disponible disminuye, reorganiza las tarjetas en una sola columna y habilita desplazamiento vertical.
La implementación reproduce con controles WinForms nativos los selectores `− / valor / +`, interruptores,
campos y botones redondeados del prototipo visual. Los controles dependientes se desactivan de forma visible,
los tiempos incompatibles se señalan junto al campo y la vista previa 16:10, con borde discontinuo e icono de
cámara, diferencia los estados vacío, apertura, captura, detención y error sin estirar la imagen. Los límites
funcionales y la persistencia no cambian. Esta rama debe validarse en Windows a
1366×768, 1920×1080 y 2560×1440 con escalado de 100 %, 125 %, 150 % y, cuando sea posible, 200 % antes de
integrarse en `main`.

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

### Calidad de imagen ampliada

FatigueDetect evalúa cada fotograma con puntajes de 0 a 100 para
brillo, contraste y nitidez, y evalúa la posición cuando existe exactamente un rostro. En vivo separa
**cámara/entorno** (luz, contraste y enfoque) de **colocación** (centrado, distancia aparente y margen
respecto a los bordes), de modo que un problema técnico no se atribuya al conductor.

Los promedios se guardan en SQLite, aparecen en el resumen final y el historial, y se exportan en las
columnas nuevas de la hoja **Sesiones**. El puntaje global usa los indicadores disponibles; las sesiones
anteriores conservan su cálculo y muestran las métricas nuevas como «sin medición». Los umbrales son
heurísticos y todavía deben validarse con cámaras, resoluciones, iluminación y tonos de piel diversos.
Consulta [calidad de imagen ampliada](docs/calidad_imagen_ampliada.md) para conocer la fórmula y la prueba manual.
Los perfiles biométricos y el historial usan la protección de datos integrada.

## Historial y registro de eventos

### Interfaz renovada de Historial

La rama `ui/mejoras-graficas` traslada el prototipo visual de **Historial de sesiones** a WinForms. La
cabecera, los filtros y las acciones mantienen la jerarquía de la referencia; la tabla conserva anchos de
columna estables, fija la fecha al desplazarse horizontalmente y ofrece desplazamiento en ambos ejes. El
panel de detalle mantiene visibles su cabecera y sus botones mientras desplaza de forma independiente las
métricas reales de calidad, ojos y parpadeos, los resúmenes y los eventos. Las consultas, filtros,
exportación, borrado y ventanas de detalle siguen usando los datos y servicios existentes.
Las acciones de cabecera y filtros mantienen su alineación, y los textos del resumen ajustan su altura
para no solaparse con las secciones siguientes al aplicar escalado de Windows.

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

## Atención y resúmenes

La aplicación incorpora un puntaje experimental de atención de 0 a 100,
sus factores de descuento, el tiempo activo hasta el primer cierre prolongado o cabeceo, el minuto
con menor puntaje y la tendencia durante la sesión. Al detener el monitoreo se abre un informe con
resumen automático, evolución, factores, respuesta a alarmas y acceso a todos los eventos.
El informe también se consulta desde **Historial → Atención y resumen** y añade tres hojas al Excel.

La calidad de detección condiciona la disponibilidad del puntaje; se exige al menos 50/100 de calidad
y 50 % de cobertura ocular. Una pausa o pérdida de medición interrumpe la recuperación y deja huecos
en la evolución. Los registros antiguos indican «sin medición». La distracción reciente se estima en
una ventana de 60 segundos a partir del tiempo con la cabeza fuera de la zona frontal; no mide pupilas.

La validación manual confirmó el funcionamiento básico con cámara e interfaz en Windows. La fórmula
y sus umbrales aún requieren calibración con más personas y condiciones. Los límites, la integración y las pruebas se explican en
[Atención y resúmenes](docs/atencion_y_resumenes.md).

## Personalización por conductor

Desde el PR #24, la aplicación amplía esta función con un asistente antes del monitoreo.
Permite reutilizar la calibración del mismo conductor, cámara y placa, o crear una nueva mirando al
frente y realizando dos parpadeos. La referencia obtenida se aplica a la apertura ocular y a la postura
frontal durante esa sesión. Cada combinación de cámara y vehículo conserva una línea base independiente.

Cuando una línea base contextual ya reúne tres sesiones válidas, el programa puede anticipar de forma
limitada los umbrales de somnolencia y alarma según su PERCLOS y atención habituales. Nunca aumenta los
tiempos configurados: la reducción máxima es de 0,30 segundos y respeta los mínimos de seguridad. La
pantalla de monitoreo muestra los tiempos efectivos y su explicación. La ampliación fue validada manualmente
e integrada en `main`; aún conviene repetir la prueba con más cámaras y personas.

La aplicación mantiene una línea base local e independiente por cédula.
Después de tres sesiones válidas de al menos 60 segundos de medición ocular, compara los parpadeos
por minuto, PERCLOS y atención de cada sesión con el comportamiento personal. El resumen final,
el historial y la hoja **Comparación personal** del Excel explican las diferencias y señalan cambios
anormales. El perfil sobrevive al reinicio de la aplicación y se elimina al borrar el historial.

Para evitar aprender de datos poco confiables, solo actualiza la línea base con calidad mínima 70/100,
cobertura ocular mínima del 60 % y sin alarmas, microsueños ni cierres anómalos. Una vez calibrado,
un cambio anormal se muestra pero no modifica el perfil. Los umbrales adaptativos nunca retrasan los tiempos
configurados y no constituyen un diagnóstico médico. Todavía requiere validación manual con varias
personas, cámaras e iluminación. Consulta [Personalización por conductor](docs/personalizacion_conductor.md)
para conocer las reglas y las pruebas recomendadas. La base se integró mediante el PR #17 y la ampliación
contextual mediante el PR #24.

## Confiabilidad explicable de los eventos

La aplicación asigna a cada evento nuevo una confiabilidad de 0 a 100
y la clasifica como alta, media o baja. La estimación combina la persistencia temporal con las métricas
que corresponden al detector: calidad y cobertura ocular para cierres y alarmas, cobertura facial para
cabeceos, y continuidad de captura y estabilidad de FPS para problemas de entorno o cámara.

El resumen final explica qué redujo la confiabilidad general. El historial, la línea temporal y Excel
muestran el puntaje y las advertencias de cada evento. Las sesiones anteriores permanecen como
«sin medición» y los detectores futuros pueden conservar una evaluación propia. El puntaje describe la
calidad de los datos disponibles; no es la probabilidad médica de que exista fatiga. Consulta
[Confiabilidad de eventos](docs/confiabilidad_eventos.md). La función está integrada en `main` mediante el PR #18.

## Salida de audio de la alarma

En **Configuración → Salida de audio de la alarma**, elige una salida de Windows, como parlantes, audífonos o Bluetooth. **Probar** reproduce la alarma durante dos segundos en la salida elegida. **Actualizar** vuelve a consultar los dispositivos y **Guardar cambios** conserva la selección. Si la salida guardada falla durante el monitoreo, el programa intenta reproducir el sonido en la salida predeterminada de Windows.

## Alertas progresivas

Desde el PR #25, la aplicación incorpora tres niveles experimentales: **preventivo**, **alto** y
**crítico**. Combina el estado ocular con el puntaje de atención y, cuando están disponibles, PERCLOS y
distracción. La interfaz cambia el color, el título, la recomendación y las causas observadas. El sonido
usa aproximadamente 35 %, 65 % y 100 % de volumen; los avisos pueden repetirse como máximo cada 30, 15
y 5 segundos, respectivamente.

El nivel sube inmediatamente, pero no baja por una sola lectura favorable. Exige ojos abiertos, cabeza
frontal, calidad mínima 50/100 y atención mínima 70/100 durante 3 segundos para salir del nivel crítico,
5 segundos para bajar del alto y 8 segundos para abandonar el preventivo. Una pausa o pérdida de medición
interrumpe esa confirmación. Cada intervalo se registra en la línea temporal y Excel. La alarma ocular
tradicional sigue contando por separado. Consulta [la guía de prueba](docs/alertas_progresivas.md).
La función fue validada manualmente e integrada en `main`; sus umbrales siguen siendo experimentales.

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

El análisis histórico avanzado, integrado en `main` mediante el PR #22, añade pestañas para ordenar las sesiones con atención y calidad medibles de menor a mayor riesgo, consultar la tendencia cronológica por cédula, comparar vehículo, hora de inicio y día semanal, ver un mapa de calor día/hora y comparar dos o más sesiones marcadas. Las alarmas por hora se dividen por tiempo activo observado; las medias de PERCLOS y parpadeos se ponderan por tiempo ocular válido. El riesgo es un índice experimental `100 − atención` y las sesiones sin datos suficientes quedan fuera de ese orden. Consulta [la guía de pruebas](docs/analisis_historico_avanzado.md).

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

## Cabeza, encuadre y boca

La aplicación añade un análisis heurístico de regiones de ojos, boca y frente.
Después de dos segundos puede mostrar **posibles gafas oscuras**, **posible mascarilla** o **posible gorra u
obstrucción superior**. También presenta porcentajes de visibilidad y confianza facial. Si la visibilidad
ocular o los puntos faciales no son suficientes, esa muestra no alimenta las métricas de fatiga y se registra
un episodio de medición no confiable. Los resultados dependen de iluminación, tono, diseño del accesorio y
cámara: son advertencias de obstrucción, no una clasificación cierta. Consulta
[Detección de accesorios](docs/deteccion_accesorios.md). Los perfiles biométricos y el historial se leen
mediante la protección de datos integrada.

Al calibrar, mira al frente con la cabeza erguida. El historial y Excel registran inicio, fin y duración de cada cabeceo y de los giros a izquierda, derecha, arriba o abajo. El resumen recoge el tiempo fuera de la zona frontal, el episodio más largo y la proporción respecto al tiempo activo. También indica cuánto tiempo tuvo una pose medible.

La dirección estimada de mirada se basa en la orientación de la cabeza. El modelo actual no mide pupilas. La postura encorvada se estima con el descenso del rostro y la inclinación de la cabeza respecto a la posición frontal inicial. La distancia es una aproximación en centímetros con una focal y escala facial supuestas. El aviso de encuadre se activa al detectar un rostro cerca del borde.

Un posible bostezo es una apertura pronunciada de la boca que dura entre 0,8 y 8 segundos y termina con el cierre. La tasa por hora usa el tiempo observado de la boca. Hablar u otros movimientos también pueden producir episodios. Las pausas y pérdidas de señal no se cuentan como tiempo observado.
