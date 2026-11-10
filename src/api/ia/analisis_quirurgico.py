
import cv2
import mediapipe as mp
import numpy as np
from pathlib import Path

# ============================================================
# SIMULATORSENHANCE - ANALISIS QUIRURGICO AUTOMATICO
# MediaPipe + OpenCV
# ============================================================

BASE = Path(__file__).resolve().parent

CARPETA_CAPTURAS = Path(
    r"C:\Users\diego\AppData\LocalLow\DefaultCompany"
    r"\SimulatorSenhance\CapturasLaparoscopicas"
)

MODELO = BASE / "pose_landmarker_recuperado.task"
RESULTADOS = BASE / "Resultados"

RESULTADOS.mkdir(exist_ok=True)

# ============================================================
# 1. SELECCIONAR CAPTURA
# ============================================================

capturas = sorted(
    CARPETA_CAPTURAS.glob("Laparoscopia_*.png"),
    key=lambda archivo: archivo.stat().st_mtime,
    reverse=True
)

if not capturas:
    raise SystemExit("ERROR: No se encontraron capturas de Unity.")

print("\nCAPTURAS DISPONIBLES:\n")

for i, archivo in enumerate(capturas[:10], start=1):
    print(f"{i}. {archivo.name}")

seleccion = input(
    "\nSelecciona una captura (Enter = mas reciente): "
).strip()

if seleccion == "":
    indice = 0
else:
    try:
        indice = int(seleccion) - 1
    except ValueError:
        raise SystemExit("ERROR: Debes ingresar un numero.")

if not 0 <= indice < min(10, len(capturas)):
    raise SystemExit("ERROR: Seleccion fuera de rango.")

ruta = capturas[indice]

imagen = cv2.imread(str(ruta))

if imagen is None:
    raise SystemExit("ERROR: No se pudo abrir la imagen.")

if not MODELO.is_file():
    raise SystemExit("ERROR: No se encontro el modelo MediaPipe.")

alto, ancho = imagen.shape[:2]

print(f"\nAnalizando: {ruta.name}")

# ============================================================
# 2. RECONOCIMIENTO CORPORAL CON MEDIAPIPE
# ============================================================

opciones = mp.tasks.vision.PoseLandmarkerOptions(
    base_options=mp.tasks.BaseOptions(
        model_asset_path=str(MODELO)
    ),
    running_mode=mp.tasks.vision.RunningMode.IMAGE,
    num_poses=1
)

rgb = cv2.cvtColor(imagen, cv2.COLOR_BGR2RGB)

imagen_mp = mp.Image(
    image_format=mp.ImageFormat.SRGB,
    data=rgb
)

with mp.tasks.vision.PoseLandmarker.create_from_options(
    opciones
) as detector:
    deteccion = detector.detect(imagen_mp)

if not deteccion.pose_landmarks:
    raise SystemExit("No se detecto al paciente.")

puntos = deteccion.pose_landmarks[0]

def coordenada(indice):
    punto = puntos[indice]
    return np.array(
        [punto.x * ancho, punto.y * alto],
        dtype=np.float32
    )

hombro_izq = coordenada(11)
hombro_der = coordenada(12)
cadera_izq = coordenada(23)
cadera_der = coordenada(24)

# ============================================================
# 3. DELIMITAR REGION ABDOMINAL
# ============================================================

factor_superior = 0.15
factor_inferior = 0.95
margen = 0.15

superior_izq = hombro_izq + factor_superior * (
    cadera_izq - hombro_izq
)

superior_der = hombro_der + factor_superior * (
    cadera_der - hombro_der
)

inferior_izq = hombro_izq + factor_inferior * (
    cadera_izq - hombro_izq
)

inferior_der = hombro_der + factor_inferior * (
    cadera_der - hombro_der
)

ancho_superior = superior_der - superior_izq
ancho_inferior = inferior_der - inferior_izq

superior_izq -= ancho_superior * margen
superior_der += ancho_superior * margen

inferior_izq -= ancho_inferior * margen
inferior_der += ancho_inferior * margen

poligono = np.array(
    [
        superior_izq,
        superior_der,
        inferior_der,
        inferior_izq
    ],
    dtype=np.int32
)

mascara_abdomen = np.zeros(
    (alto, ancho),
    dtype=np.uint8
)

cv2.fillPoly(
    mascara_abdomen,
    [poligono],
    255
)

abdomen_aislado = cv2.bitwise_and(
    imagen,
    imagen,
    mask=mascara_abdomen
)

# ============================================================
# 4. DETECTAR INCISION CON OPENCV
# ============================================================

hsv = cv2.cvtColor(
    abdomen_aislado,
    cv2.COLOR_BGR2HSV
)

mascara_oscura = cv2.inRange(
    hsv,
    np.array([0, 0, 0]),
    np.array([179, 255, 110])
)

mascara_roja1 = cv2.inRange(
    hsv,
    np.array([0, 65, 35]),
    np.array([15, 255, 210])
)

mascara_roja2 = cv2.inRange(
    hsv,
    np.array([170, 65, 35]),
    np.array([179, 255, 210])
)

mascara_incision = cv2.bitwise_or(
    mascara_oscura,
    cv2.bitwise_or(
        mascara_roja1,
        mascara_roja2
    )
)

mascara_incision = cv2.bitwise_and(
    mascara_incision,
    mascara_abdomen
)

mascara_incision = cv2.morphologyEx(
    mascara_incision,
    cv2.MORPH_CLOSE,
    np.ones((9, 9), np.uint8)
)

mascara_incision = cv2.morphologyEx(
    mascara_incision,
    cv2.MORPH_OPEN,
    np.ones((3, 3), np.uint8)
)

mascara_incision = cv2.bitwise_and(
    mascara_incision,
    mascara_abdomen
)

contornos, _ = cv2.findContours(
    mascara_incision,
    cv2.RETR_EXTERNAL,
    cv2.CHAIN_APPROX_SIMPLE
)

contornos_validos = [
    c for c in contornos
    if cv2.contourArea(c) > 100
]

# ============================================================
# 5. DIBUJAR RESULTADOS
# ============================================================

resultado = imagen.copy()
resultado_abdomen = imagen.copy()

cv2.polylines(
    resultado_abdomen,
    [poligono],
    True,
    (0, 255, 255),
    3
)

for indice in [11, 12, 23, 24]:
    x, y = coordenada(indice).astype(int)
    cv2.circle(
        resultado_abdomen,
        (int(x), int(y)),
        7,
        (0, 255, 0),
        -1
    )

if contornos_validos:

    contorno = max(
        contornos_validos,
        key=cv2.contourArea
    )

    cv2.drawContours(
        resultado,
        [contorno],
        -1,
        (0, 255, 0),
        2
    )

    x, y, w, h = cv2.boundingRect(contorno)

    cv2.rectangle(
        resultado,
        (x, y),
        (x + w, y + h),
        (255, 0, 0),
        2
    )

    centro_x = x + w // 2
    centro_y = y + h // 2

    cv2.circle(
        resultado,
        (centro_x, centro_y),
        5,
        (0, 0, 255),
        -1
    )

    print("\nCANDIDATO DE INCISION DETECTADO")
    print("Centro:", centro_x, centro_y)
    print("Ancho:", w, "pixeles")
    print("Alto:", h, "pixeles")
    print("Area:", cv2.contourArea(contorno))

else:
    print("\nNo se encontraron candidatos de incision.")

# ============================================================
# 6. GUARDAR RESULTADOS
# ============================================================

nombre = ruta.stem

cv2.imwrite(
    str(RESULTADOS / f"{nombre}_abdomen.png"),
    resultado_abdomen
)

cv2.imwrite(
    str(RESULTADOS / f"{nombre}_incision.png"),
    resultado
)

cv2.imwrite(
    str(RESULTADOS / f"{nombre}_mascara.png"),
    mascara_incision
)

print("\nResultados guardados en:", RESULTADOS)

cv2.imshow(
    "SimulatorSenhance - Abdomen",
    resultado_abdomen
)

cv2.imshow(
    "SimulatorSenhance - Incision",
    resultado
)

cv2.imshow(
    "SimulatorSenhance - Mascara",
    mascara_incision
)

cv2.waitKey(0)
cv2.destroyAllWindows()
