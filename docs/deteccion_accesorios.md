# Detección experimental de accesorios

Estado: disponible en `descarga/deteccion-accesorios`, pendiente de validación en Windows y decisión de integración.

## Alcance

El detector compara brillo y variación local en las regiones aproximadas de ojos, boca y frente dentro del
rectángulo facial. Combina esos indicios con la disponibilidad de puntos faciales para calcular visibilidad de
ojos, boca y confianza total. Después de dos segundos persistentes puede registrar:

- Posibles gafas oscuras u obstrucción oscura sobre los ojos.
- Posible mascarilla o cobertura uniforme de la región inferior.
- Posible gorra u obstrucción oscura en la frente.
- Medición facial no confiable.

Una muestra no confiable se excluye de la apertura ocular, pose y atención para evitar inventar datos. Perder
el rostro interrumpe el episodio y el detector normal de ausencia sigue funcionando por separado.

No se usa un modelo especializado de accesorios. Sombras, piel, cabello, maquillaje, contraluz y diseños de
ropa pueden generar falsos positivos o negativos. La interfaz usa siempre la palabra «posible».

## Prueba con cámara

Con el vehículo detenido:

1. Descarga la rama, ejecuta Release y comienza una sesión con luz frontal uniforme.
2. Sin accesorios, espera varios segundos: la confianza debería permanecer alta y no debe aparecer un aviso.
3. Ponte gafas transparentes. La medición debería continuar; anota cualquier falso positivo.
4. Ponte gafas muy oscuras durante más de dos segundos. Comprueba el aviso, la reducción de visibilidad ocular
   y que la apertura ocular quede sin medición mientras la advertencia esté activa.
5. Repite con mascarilla y luego con gorra baja. Verifica que el texto diga «posible» y que desaparezca al retirar
   el accesorio durante una observación estable.
6. Tapa parcialmente ojos o boca con una mano y prueba luz lateral o contraluz. Registra casos que clasifique mal.
7. Pausa o sal del encuadre durante un episodio. No debe prolongar artificialmente su duración.
8. Detén la sesión y revisa historial, resumen de eventos, línea temporal y Excel.

Repite a varias distancias, con distintos tonos de piel, gafas, mascarillas, gorras y cámaras antes de considerar
los umbrales suficientemente calibrados.
