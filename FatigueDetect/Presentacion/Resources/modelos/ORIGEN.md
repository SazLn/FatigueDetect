# Modelo facial incluido

`face-landmark.onnx` es una conversión del modelo base de 468 puntos **MediaPipe Face Mesh**, de Google.
Se distribuye con la licencia Apache 2.0 incluida en `LICENSE-MediaPipe.txt`.

- [Catálogo oficial de modelos de MediaPipe v0.10.9](https://github.com/google-ai-edge/mediapipe/blob/v0.10.9/docs/solutions/models.md#face-mesh)
- [Ficha del modelo de 468 puntos y su licencia](https://drive.google.com/file/d/1QvwWNfFoweGVjsXF3DXzcrCnz-mx-Lha/view)
- [Modelo TFLite original, generación 1676323214951585](https://storage.googleapis.com/mediapipe-assets/face_landmark.tflite?generation=1676323214951585)
- [Rostro canónico de MediaPipe](https://github.com/google-ai-edge/mediapipe/blob/v0.10.9/mediapipe/modules/face_geometry/data/canonical_face_model.obj), fuente de los puntos tridimensionales para la estimación de pose.

Se convirtió el formato TFLite a ONNX (opset 13) con TensorFlow CPU 2.16.2, tf2onnx 1.16.1 y ONNX 1.17.0.
La app usa ONNX Runtime en CPU. Las dimensiones de entrada y salida se conservan en la conversión.

```text
SHA-256 del original: 1055cb9d4a9ca8b8c688902a3a5194311138ba256bcc94e336d8373a5f30c814
SHA-256 del ONNX incluido: 415d7e6aa2fa84273c16f6625d2354ad326c36fc28a442668b17a4b5b89c1c4b
```

```sh
python -m tf2onnx.convert --tflite face_landmark.tflite --opset 13 --output face-landmark.onnx
```
