# Sistema de Detección de Fatiga al Volante - 4 proyectos (capas) con interfaz nueva

Abrir `FatigueDetect.sln` en Visual Studio 2022 (.NET 8 SDK) y ejecutar con F5.
Proyecto de inicio: `Presentacion`.

| Proyecto     | Tipo                     | Referencias              | Paquetes NuGet                        |
|--------------|--------------------------|--------------------------|---------------------------------------|
| Entidades    | Biblioteca de clases     | -                        | -                                     |
| Datos        | Biblioteca de clases     | Entidades                | OpenCvSharp4, OpenCvSharp4.Extensions, Microsoft.Data.Sqlite |
| Logica       | Biblioteca de clases     | Entidades, Datos         | OpenCvSharp4                          |
| Presentacion | Aplicacion Windows Forms | Logica, Datos, Entidades | OpenCvSharp4.runtime.win              |

## Pantallas (menu lateral)
- Registro / Persona: cedula, nombre, apellidos, tipo de carro (editable) y placa (libre).
- Inicio: cámara en vivo, estado del conductor, gráfica, nivel de atención y avisos de iluminación.
- Historial de sesiones: sesiones guardadas, cronología de eventos y opción para eliminar el historial.
- Configuración: sensibilidad de detección, sonido de la alarma y selector de cámara con vista previa.

## Archivos guardados en Documentos\FatigueDetect\
- historial.sqlite (sesiones y eventos guardados localmente)
- historial.csv (historial de versiones anteriores, si existe)
- tipos_carro.txt
- camara_preferida.txt (selección de cámara para este equipo)
- perfiles_biometricos.json (datos del conductor y descriptores numéricos del rostro; no contiene fotografías)

## Elegir la cámara y revisar la iluminación

En **Configuración → Cámara**, elige la cámara por el nombre que muestra Windows o selecciona **Automática**.
Pulsa **Actualizar lista** después de conectar una cámara externa y **Ver vista previa** para comprobar
el encuadre y la iluminación. La selección se guarda al cambiarla y se usa tanto para la captura
facial como para el monitoreo. Si la cámara guardada se desconecta, vuelve a conectarla o selecciona otra.
Puedes cambiar la cámara entre sesiones.

La vista previa indica si la imagen está oscura, sobreexpuesta o bien iluminada. En **Inicio**,
el aviso aparece debajo del video cuando la imagen permanece demasiado oscura o sobreexpuesta
durante al menos dos segundos. Desaparece al corregir la iluminación. La estimación usa el brillo
promedio de la imagen y el episodio se guarda en el historial al terminar la sesión.

## Historial y registro de eventos

Al detener el monitoreo o cerrar la aplicación normalmente, se guardan la sesión y sus
eventos en una sola operación de SQLite. En **Historial de sesiones**, selecciona una fila
para ver los eventos en orden, con su hora de inicio y duración. SQLite conserva el inicio
y el fin en UTC con precisión de milisegundos y la interfaz muestra la hora local.

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
Al usar **Eliminar historial**, se borran las sesiones y sus eventos en SQLite. El CSV
se conserva y la importación no vuelve a ejecutarse.

Consulta el [plan de características](docs/plan_caracteristicas_fatiguedetect.md)
y el [registro de cambios](CHANGELOG.md).

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
