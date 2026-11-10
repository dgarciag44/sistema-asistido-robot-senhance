import argparse
import cv2
import json
import mediapipe as mp
import numpy as np
import subprocess
import sys
from datetime import datetime
from pathlib import Path

BASE = Path(__file__).resolve().parent
CARPETA_CAPTURAS = Path(r"C:\Users\diego\AppData\LocalLow\DefaultCompany\SimulatorSenhance\CapturasLaparoscopicas")
MODELO = BASE / "pose_landmarker_recuperado.task"
RESULTADOS = BASE / "Resultados"
RESULTADOS.mkdir(exist_ok=True)


def analizar(captura=None, sin_ventanas=False):
    capturas = sorted(CARPETA_CAPTURAS.glob("Laparoscopia_*.png"), key=lambda a: a.stat().st_mtime, reverse=True)
    if not capturas:
        raise SystemExit("ERROR: No se encontraron capturas de Unity.")
    if captura is not None:
        if Path(captura).name != captura or not captura.startswith("Laparoscopia_"):
            raise SystemExit("ERROR: Nombre de captura invalido.")
        ruta = CARPETA_CAPTURAS / f"{captura}.png"
        if not ruta.is_file():
            raise SystemExit(f"ERROR: Captura no encontrada: {ruta}")
    else:
        print("\nCAPTURAS DISPONIBLES:\n")
        for i, archivo in enumerate(capturas[:10], 1):
            print(f"{i}. {archivo.name}")
        seleccion = input("\nSelecciona una captura (Enter = mas reciente): ").strip()
        try:
            indice = int(seleccion) - 1 if seleccion else 0
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
    print("\nAnalizando:", ruta.name)
    opciones = mp.tasks.vision.PoseLandmarkerOptions(
        base_options=mp.tasks.BaseOptions(model_asset_path=str(MODELO)),
        running_mode=mp.tasks.vision.RunningMode.IMAGE,
        num_poses=1
    )
    rgb = cv2.cvtColor(imagen, cv2.COLOR_BGR2RGB)
    imagen_mp = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)
    with mp.tasks.vision.PoseLandmarker.create_from_options(opciones) as detector:
        deteccion = detector.detect(imagen_mp)
    if not deteccion.pose_landmarks:
        raise SystemExit("No se detecto al paciente.")
    puntos = deteccion.pose_landmarks[0]

    def coordenada(i):
        p = puntos[i]
        return np.array([p.x * ancho, p.y * alto], dtype=np.float32)

    hi, hd, ci, cd = [coordenada(i) for i in (11, 12, 23, 24)]
    fs, fi, margen = 0.15, 0.95, 0.15
    si = hi + fs * (ci - hi)
    sd = hd + fs * (cd - hd)
    ii = hi + fi * (ci - hi)
    ider = hd + fi * (cd - hd)
    superior = sd - si
    inferior = ider - ii
    si -= superior * margen
    sd += superior * margen
    ii -= inferior * margen
    ider += inferior * margen
    poligono = np.array([si, sd, ider, ii], dtype=np.int32)
    mascara_abdomen = np.zeros((alto, ancho), dtype=np.uint8)
    cv2.fillPoly(mascara_abdomen, [poligono], 255)
    abdomen_aislado = cv2.bitwise_and(imagen, imagen, mask=mascara_abdomen)
    hsv = cv2.cvtColor(abdomen_aislado, cv2.COLOR_BGR2HSV)
    mascara_oscura = cv2.inRange(hsv, np.array([0, 0, 0]), np.array([179, 255, 110]))
    mascara_roja1 = cv2.inRange(hsv, np.array([0, 65, 35]), np.array([15, 255, 210]))
    mascara_roja2 = cv2.inRange(hsv, np.array([170, 65, 35]), np.array([179, 255, 210]))
    mascara_incision = cv2.bitwise_or(mascara_oscura, cv2.bitwise_or(mascara_roja1, mascara_roja2))
    mascara_incision = cv2.bitwise_and(mascara_incision, mascara_abdomen)
    mascara_incision = cv2.morphologyEx(mascara_incision, cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8))
    mascara_incision = cv2.morphologyEx(mascara_incision, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    mascara_incision = cv2.bitwise_and(mascara_incision, mascara_abdomen)
    contornos, _ = cv2.findContours(mascara_incision, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    validos = [c for c in contornos if cv2.contourArea(c) > 100]
    resultado = imagen.copy()
    resultado_abdomen = imagen.copy()
    cv2.polylines(resultado_abdomen, [poligono], True, (0, 255, 255), 3)
    for i in (11, 12, 23, 24):
        x, y = coordenada(i).astype(int)
        cv2.circle(resultado_abdomen, (int(x), int(y)), 7, (0, 255, 0), -1)
    datos = {
        "captura": ruta.stem,
        "fecha_analisis": datetime.now().isoformat(timespec="seconds"),
        "ancho_imagen_px": int(ancho),
        "alto_imagen_px": int(alto),
        "incision_detectada": bool(validos),
        "centro_px": None,
        "ancho_px": None,
        "alto_px": None,
        "area_px2": None,
        "metodo": "MediaPipe Pose Landmarker + OpenCV; candidato por color y contornos",
        "uso": "Simulacion academica, no validacion clinica"
    }
    if validos:
        contorno = max(validos, key=cv2.contourArea)
        cv2.drawContours(resultado, [contorno], -1, (0, 255, 0), 2)
        x, y, w, h = cv2.boundingRect(contorno)
        cv2.rectangle(resultado, (x, y), (x + w, y + h), (255, 0, 0), 2)
        cx, cy = x + w // 2, y + h // 2
        cv2.circle(resultado, (cx, cy), 5, (0, 0, 255), -1)
        datos.update(centro_px=[int(cx), int(cy)], ancho_px=int(w), alto_px=int(h), area_px2=float(cv2.contourArea(contorno)))
        print("\nCANDIDATO DE INCISION DETECTADO")
        print("Centro:", cx, cy)
        print("Ancho:", w, "pixeles")
        print("Alto:", h, "pixeles")
        print("Area:", datos["area_px2"])
    else:
        print("\nNo se encontraron candidatos de incision.")
    nombre = ruta.stem
    for sufijo, salida in (("abdomen", resultado_abdomen), ("incision", resultado), ("mascara", mascara_incision)):
        destino = RESULTADOS / f"{nombre}_{sufijo}.png"
        if not cv2.imwrite(str(destino), salida):
            raise OSError(f"No se pudo guardar: {destino}")
    json_destino = RESULTADOS / f"{nombre}_analisis.json"
    temporal = json_destino.with_name(json_destino.name + ".tmp")
    temporal.write_text(json.dumps(datos, ensure_ascii=False, indent=2), encoding="utf-8")
    temporal.replace(json_destino)
    print("\nResultados guardados en:", RESULTADOS)
    print("Mediciones guardadas en:", json_destino)

    generador = BASE / "generar_informe.py"
    if generador.is_file():
        print("\nGenerando o actualizando informe de esta captura...")
        try:
            subprocess.run([sys.executable, str(generador), "--captura", nombre, "--actualizar"], cwd=str(BASE), check=True)
        except subprocess.CalledProcessError as error:
            print("ERROR al generar el informe:", error)
    else:
        print("ERROR: No se encontro generar_informe.py")

    archivador = BASE / "archivo_dicom.py"
    if archivador.is_file():
        print("\nOrganizando estudios DICOM automaticamente...")
        try:
            subprocess.run([sys.executable, str(archivador)], cwd=str(BASE), check=True)
        except subprocess.CalledProcessError as error:
            print("ERROR al archivar los estudios DICOM:", error)
    else:
        print("ERROR: No se encontro archivo_dicom.py")

    if not sin_ventanas:
        cv2.imshow("SimulatorSenhance - Abdomen", resultado_abdomen)
        cv2.imshow("SimulatorSenhance - Incision", resultado)
        cv2.imshow("SimulatorSenhance - Mascara", mascara_incision)
        cv2.waitKey(0)
        cv2.destroyAllWindows()


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--captura", help="Nombre base de la captura PNG")
    parser.add_argument("--sin-ventanas", action="store_true")
    args = parser.parse_args()
    analizar(captura=args.captura, sin_ventanas=args.sin_ventanas)
