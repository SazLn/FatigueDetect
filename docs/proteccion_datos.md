# Protección de datos

Estado: disponible en `descarga/proteccion-datos`, pendiente de validación en Windows y decisión de integración.

## Alcance

- AES-GCM autentica y cifra perfiles biométricos, cédula, nombre, apellidos, vehículo, placa y documentos
  JSON de atención, postura, comparación y confiabilidad.
- `clave_datos.bin` contiene la clave de datos protegida con DPAPI para el usuario actual de Windows.
- Los perfiles biométricos JSON anteriores se migran al abrirlos. El historial anterior continúa legible;
  las sesiones nuevas se guardan cifradas.
- La retención automática elimina sesiones y perfiles biométricos anteriores al plazo configurado. Si elimina
  sesiones, reinicia las líneas base personales para evitar perfiles derivados de datos ya borrados.
- El respaldo `.fdbak` usa PBKDF2-SHA256 con 200 000 iteraciones y AES-GCM. Incluye una clave portátil solo
  dentro del contenedor cifrado. La base se incorpora mediante una instantánea consistente aunque existan
  conexiones internas abiertas. Antes de restaurar se validan autenticidad, nombres de archivo e integridad SQLite.

La contraseña del respaldo no puede recuperarse. No borres `clave_datos.bin` ni reemplaces archivos de datos
manualmente. La aplicación no cifra `historial.csv` legado porque se conserva como archivo externo de versiones
anteriores; elimínalo o protégelo por separado si contiene información personal.

## Prueba en Windows

1. Descarga la rama y guarda una copia de `Documentos\FatigueDetect` antes de comenzar.
2. Ejecuta la aplicación, registra un rostro y termina una sesión. Cierra FatigueDetect.
3. Abre `perfiles_biometricos.json` con un editor: debe comenzar por `FDENC1:` y no mostrar nombres o cédulas.
4. Abre `historial.sqlite` con un visor SQLite y consulta `Cedula`, `Nombre` y `Placa`: deben contener valores
   `FDENC1:`. La aplicación, en cambio, debe mostrar los valores originales normalmente.
5. Activa retención, usa plazos de prueba y pulsa **Guardar política**. No uses datos importantes para probar
   eliminaciones; comprueba el recuento indicado.
6. Pulsa **Crear respaldo**, usa una contraseña de al menos 10 caracteres y guarda el `.fdbak` fuera de la carpeta.
7. Intenta restaurarlo con una contraseña incorrecta: debe rechazarse sin reemplazar datos.
8. Crea una sesión temporal o elimina un perfil, restaura con la contraseña correcta y reinicia la aplicación.
   Verifica historial, perfiles, configuración y cámara preferida.
9. Copia el respaldo a otra cuenta o equipo Windows y comprueba que puede restaurarse con la contraseña.

Repite las pruebas con cierres inesperados y conserva siempre un respaldo externo antes de validar restauración.
