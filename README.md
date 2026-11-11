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
