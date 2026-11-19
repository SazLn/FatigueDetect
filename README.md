# Sistema de Detección de Fatiga al Volante - 4 proyectos (capas) con interfaz nueva

Abrir `FatigueDetect.sln` en Visual Studio 2022 (.NET 8 SDK) y ejecutar con F5.
Proyecto de inicio: `Presentacion`.

| Proyecto     | Tipo                     | Referencias              | Paquetes NuGet                        |
|--------------|--------------------------|--------------------------|---------------------------------------|
| Entidades    | Biblioteca de clases     | -                        | -                                     |
| Datos        | Biblioteca de clases     | Entidades                | OpenCvSharp4, OpenCvSharp4.Extensions |
| Logica       | Biblioteca de clases     | Entidades, Datos         | OpenCvSharp4                          |
| Presentacion | Aplicacion Windows Forms | Logica, Datos, Entidades | OpenCvSharp4.runtime.win              |

## Pantallas (menu lateral)
- Registro / Persona: cedula, nombre, apellidos, tipo de carro (editable) y placa (libre).
- Inicio: camara en vivo, estado del conductor, grafica, nivel de atencion y alertas de la sesion.
- Historial de alertas: sesiones guardadas, detalle con la hora de cada alarma, eliminar historial.
- Configuracion: segundos para somnolencia y alarma, sonido de la alarma.

## Archivos guardados en Documentos\FatigueDetect\
- historial.csv  (Cedula;Nombre;Apellidos;TipoCarro;Placa;Inicio;Fin;Alarmas;HorasAlarmas)
- tipos_carro.txt
- perfiles_biometricos.json (datos del conductor y descriptores numéricos del rostro; no contiene fotografías)

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
