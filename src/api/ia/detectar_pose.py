
import cv2
import mediapipe as mp
import numpy as np
import os

# ============================================================
# SIMULATORSENHANCE
# DETECCION CORPORAL Y LOCALIZACION AUTOMATICA DEL ABDOMEN
# ============================================================

ruta = (
    r"C:\Users\diego\AppData\LocalLow\DefaultCompany"
    r"\SimulatorSenhance\CapturasLaparoscopicas"
    r"\Laparoscopia_20261109_151803_162_003.png"
)

modelo = "pose_landmarker_recuperado.task"

imagen = cv2.imread(ruta)

if imagen is None:
    raise SystemExit("ERROR: No se encontro la imagen.")

if not os.path.exists(modelo):
    raise SystemExit("ERROR: No se encontro el modelo.")

# ============================================================
# 1. CONFIGURAR MEDIAPIPE
# ============================================================

BaseOptions = mp.tasks.BaseOptions
PoseLandmarker = mp.tasks.vision.PoseLandmarker
PoseLandmarkerOptions = mp.tasks.vision.PoseLandmarkerOptions
RunningMode = mp.tasks.vision.RunningMode

opciones = PoseLandmarkerOptions(
    base_options=BaseOptions(
        model_asset_path=modelo
    ),
    running_mode=RunningMode.IMAGE,
    num_poses=1
)

resultado = imagen.copy()
alto, ancho = imagen.shape[:2]

# ============================================================
# 2. DETECTAR PUNTOS ANATOMICOS
# ============================================================

with PoseLandmarker.create_from_options(opciones) as detector:

    rgb = cv2.cvtColor(imagen, cv2.COLOR_BGR2RGB)

    imagen_mp = mp.Image(
        image_format=mp.ImageFormat.SRGB,
        data=rgb
    )

    deteccion = detector.detect(imagen_mp)

    if deteccion.pose_landmarks:

        puntos = deteccion.pose_landmarks[0]

        indices = {
            11: "Hombro izquierdo",
            12: "Hombro derecho",
            23: "Cadera izquierda",
            24: "Cadera derecha"
        }

        coordenadas = {}

        for indice, nombre in indices.items():

            punto = puntos[indice]

            x = int(punto.x * ancho)
            y = int(punto.y * alto)

            coordenadas[indice] = np.array(
                [x, y],
                dtype=np.float32
            )

            cv2.circle(
                resultado,
                (x, y),
                7,
                (0, 255, 0),
                -1
            )

            cv2.putText(
                resultado,
                nombre,
                (x + 10, y),
                cv2.FONT_HERSHEY_SIMPLEX,
                0.5,
                (0, 0, 255),
                2
            )

            print(nombre, ":", x, y)

        print("Paciente detectado.")

        # ====================================================
        # 3. CALCULAR REGION ABDOMINAL
        # ====================================================

        hombro_izq = coordenadas[11]
        hombro_der = coordenadas[12]

        cadera_izq = coordenadas[23]
        cadera_der = coordenadas[24]

        # Inicio y final estimados de la zona abdominal
        factor_superior = 0.15
        factor_inferior = 0.95

        superior_izq = (
            hombro_izq
            + factor_superior * (cadera_izq - hombro_izq)
        )

        superior_der = (
            hombro_der
            + factor_superior * (cadera_der - hombro_der)
        )

        inferior_izq = (
            hombro_izq
            + factor_inferior * (cadera_izq - hombro_izq)
        )

        inferior_der = (
            hombro_der
            + factor_inferior * (cadera_der - hombro_der)
        )

        
        # ====================================================
        # AMPLIAR REGION ABDOMINAL LATERALMENTE
        # ====================================================

        margen = 0.15

        ancho_superior = superior_der - superior_izq
        ancho_inferior = inferior_der - inferior_izq

        superior_izq = superior_izq - ancho_superior * margen
        superior_der = superior_der + ancho_superior * margen

        inferior_izq = inferior_izq - ancho_inferior * margen
        inferior_der = inferior_der + ancho_inferior * margen


        puntos_abdomen = np.array(
            [
                superior_izq,
                superior_der,
                inferior_der,
                inferior_izq
            ],
            dtype=np.int32
        )

        # ====================================================
        # 4. DIBUJAR DELIMITACION ABDOMINAL
        # ====================================================

        cv2.polylines(
            resultado,
            [puntos_abdomen],
            True,
            (0, 255, 255),
            3
        )

        centro = np.mean(
            puntos_abdomen,
            axis=0
        ).astype(int)

        cv2.circle(
            resultado,
            tuple(centro),
            6,
            (255, 0, 0),
            -1
        )

        cv2.putText(
            resultado,
            "ABDOMEN DETECTADO",
            (int(centro[0]) - 90, int(centro[1]) - 15),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.6,
            (0, 255, 255),
            2
        )

        # ====================================================
        # 5. CREAR MASCARA ABDOMINAL
        # ====================================================

        mascara_abdomen = np.zeros(
            (alto, ancho),
            dtype=np.uint8
        )

        cv2.fillPoly(
            mascara_abdomen,
            [puntos_abdomen],
            255
        )

        abdomen_aislado = cv2.bitwise_and(
            imagen,
            imagen,
            mask=mascara_abdomen
        )

        cv2.imshow(
            "Mascara abdominal - Senhance",
            mascara_abdomen
        )

        cv2.imshow(
            "Abdomen aislado - Senhance",
            abdomen_aislado
        )

        cv2.imwrite(
            "mascara_abdomen.png",
            mascara_abdomen
        )

        cv2.imwrite(
            "abdomen_aislado.png",
            abdomen_aislado
        )

        print("Region abdominal estimada correctamente.")

    else:
        print("MediaPipe no detecto al paciente.")

# ============================================================
# 6. MOSTRAR RESULTADO
# ============================================================

cv2.imshow(
    "MediaPipe Pose - Senhance",
    resultado
)

cv2.imwrite(
    "pose_abdomen_detectado.png",
    resultado
)

cv2.waitKey(0)
cv2.destroyAllWindows()
