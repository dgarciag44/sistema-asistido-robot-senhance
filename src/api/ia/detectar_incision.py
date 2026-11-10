
import cv2
import numpy as np

# ============================================================
# SIMULATORSENHANCE
# DETECCION DE INCISION UTILIZANDO MASCARA ABDOMINAL
# ============================================================

# 1. Cargar resultados de MediaPipe
imagen = cv2.imread("abdomen_aislado.png")

mascara_abdomen = cv2.imread(
    "mascara_abdomen.png",
    cv2.IMREAD_GRAYSCALE
)

if imagen is None or mascara_abdomen is None:
    raise SystemExit(
        "ERROR: Ejecuta primero detectar_pose.py"
    )

if imagen.shape[:2] != mascara_abdomen.shape:
    raise SystemExit(
        "ERROR: La imagen y la mascara tienen dimensiones diferentes."
    )

# 2. Convertir a HSV
hsv = cv2.cvtColor(
    imagen,
    cv2.COLOR_BGR2HSV
)

# 3. Detectar regiones oscuras
mascara_oscura = cv2.inRange(
    hsv,
    np.array([0, 0, 0]),
    np.array([179, 255, 110])
)

# 4. Detectar regiones rojizas
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

# 5. Combinar colores
mascara = cv2.bitwise_or(
    mascara_oscura,
    cv2.bitwise_or(
        mascara_roja1,
        mascara_roja2
    )
)

# 6. Aplicar la mascara abdominal
# Elimina detecciones fuera del abdomen
mascara = cv2.bitwise_and(
    mascara,
    mascara_abdomen
)

# 7. Procesamiento morfologico
kernel = np.ones((9, 9), np.uint8)

mascara = cv2.morphologyEx(
    mascara,
    cv2.MORPH_CLOSE,
    kernel
)

mascara = cv2.morphologyEx(
    mascara,
    cv2.MORPH_OPEN,
    np.ones((3, 3), np.uint8)
)

# Volver a limitar la mascara al abdomen
mascara = cv2.bitwise_and(
    mascara,
    mascara_abdomen
)

# 8. Buscar contornos
contornos, _ = cv2.findContours(
    mascara,
    cv2.RETR_EXTERNAL,
    cv2.CHAIN_APPROX_SIMPLE
)

# Recuperar la imagen original de Unity
imagen_original = cv2.imread(
    r"C:\Users\diego\AppData\LocalLow\DefaultCompany"
    r"\SimulatorSenhance\CapturasLaparoscopicas"
    r"\Laparoscopia_20261109_151803_162_003.png"
)

if imagen_original is None:
    raise SystemExit("ERROR: No se encontro la captura original.")

if imagen_original.shape[:2] != imagen.shape[:2]:
    raise SystemExit("ERROR: Las dimensiones no coinciden.")

resultado = imagen_original.copy()

# 9. Seleccionar candidato principal
contornos_validos = [
    contorno
    for contorno in contornos
    if cv2.contourArea(contorno) > 100
]

if contornos_validos:

    contorno_principal = max(
        contornos_validos,
        key=cv2.contourArea
    )

    cv2.drawContours(
        resultado,
        [contorno_principal],
        -1,
        (0, 255, 0),
        2
    )

    x, y, ancho, alto = cv2.boundingRect(
        contorno_principal
    )

    cv2.rectangle(
        resultado,
        (x, y),
        (x + ancho, y + alto),
        (255, 0, 0),
        2
    )

    centro_x = x + ancho // 2
    centro_y = y + alto // 2

    cv2.circle(
        resultado,
        (centro_x, centro_y),
        5,
        (0, 0, 255),
        -1
    )

    print("Candidato de incision localizado.")
    print("Centro:", centro_x, centro_y)
    print("Ancho:", ancho)
    print("Alto:", alto)
    print(
        "Area:",
        cv2.contourArea(contorno_principal)
    )

else:
    print("No se encontraron candidatos de incision.")

# 10. Guardar resultados
cv2.imwrite(
    "incision_detectada.png",
    resultado
)

cv2.imwrite(
    "mascara_incision.png",
    mascara
)

cv2.imshow(
    "Incision detectada - Senhance",
    resultado
)

cv2.imshow(
    "Mascara de incision - Senhance",
    mascara
)

cv2.waitKey(0)
cv2.destroyAllWindows()
