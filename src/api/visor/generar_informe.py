
import argparse
import json
import shutil
import pydicom
from pathlib import Path
from datetime import datetime
from xml.sax.saxutils import escape

from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer,
    Table, TableStyle, Image, KeepTogether
)
from reportlab.lib import colors
from reportlab.lib.styles import (
    getSampleStyleSheet, ParagraphStyle
)
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import cm
from reportlab.lib.utils import ImageReader

# ============================================================
# SIMULATORSENHANCE - GENERADOR DE INFORMES
# ============================================================

BASE = Path(__file__).resolve().parent
RESULTADOS = BASE / "Resultados"
INFORMES = BASE / "Informes"

CAPTURAS = Path(
    r"C:\Users\diego\AppData\LocalLow\DefaultCompany"
    r"\SimulatorSenhance\CapturasLaparoscopicas"
)

INFORMES.mkdir(parents=True, exist_ok=True)

PACIENTE = "SIMULADO PACIENTE"
IDENTIFICADOR = "SIM-001"
PROCEDIMIENTO = "Extracción del Riñón"
OBSERVACIONES = "Sin observaciones adicionales"


# ============================================================
# BUSCAR INFORME EXISTENTE
# ============================================================

def buscar_informe(nombre_base):
    coincidencias = sorted(
        INFORMES.glob(f"Informe_{nombre_base}_*.pdf"),
        key=lambda archivo: archivo.stat().st_mtime,
        reverse=True
    )

    for archivo in coincidencias:
        if archivo.is_file() and archivo.stat().st_size > 0:
            return archivo

    # Compatibilidad con nombre fijo
    archivo_fijo = INFORMES / f"Informe_{nombre_base}.pdf"

    if archivo_fijo.is_file() and archivo_fijo.stat().st_size > 0:
        return archivo_fijo

    return None


# ============================================================
# SELECCIONAR CAPTURA
# ============================================================

def ultima_captura_analizada():
    analisis = sorted(
        RESULTADOS.glob("*_incision.png"),
        key=lambda archivo: archivo.stat().st_mtime,
        reverse=True
    )

    if not analisis:
        raise FileNotFoundError(
            "No hay analisis de incision disponibles."
        )

    return analisis[0].name.removesuffix("_incision.png")


# ============================================================
# GENERAR INFORME ESPECIFICO
# ============================================================

def datos_dicom(nombre_base):
    dicom = CAPTURAS / f"{nombre_base}.dcm"
    if not dicom.is_file():
        raise FileNotFoundError(f"No se encontro el DICOM de la captura: {dicom}")
    ds = pydicom.dcmread(str(dicom), stop_before_pixels=True)
    return (
        str(ds.get("PatientName", "No disponible")).replace("^", " ").strip(),
        str(ds.get("PatientID", "No disponible")).strip(),
    )


def generar_informe(nombre_base=None, actualizar=False):
    if nombre_base is None:
        nombre_base = ultima_captura_analizada()

    # Impedir rutas arbitrarias
    if (
        not nombre_base
        or Path(nombre_base).name != nombre_base
        or "/" in nombre_base
        or "\\" in nombre_base
    ):
        raise ValueError("Identificador de captura invalido.")

    existente = buscar_informe(nombre_base)

    if existente and not actualizar:
        print("Informe existente:", existente)
        return existente

    paciente_dicom, id_dicom = datos_dicom(nombre_base)
    json_analisis = RESULTADOS / f"{nombre_base}_analisis.json"
    mediciones = None
    if json_analisis.is_file():
        mediciones = json.loads(json_analisis.read_text(encoding="utf-8"))
        if mediciones.get("captura") != nombre_base:
            raise ValueError("El JSON no corresponde a esta captura")

    imagen_original = CAPTURAS / f"{nombre_base}.png"
    imagen_abdomen = RESULTADOS / f"{nombre_base}_abdomen.png"
    imagen_incision = RESULTADOS / f"{nombre_base}_incision.png"

    if not imagen_original.is_file():
        raise FileNotFoundError(
            f"No existe la captura original: {imagen_original}"
        )

    if not imagen_abdomen.is_file():
        raise FileNotFoundError(
            f"No existe el mapeo anatomico: {imagen_abdomen}"
        )

    if not imagen_incision.is_file():
        raise FileNotFoundError(
            f"No existe el analisis de incision: {imagen_incision}"
        )

    # Guardar el informe anterior antes de reemplazarlo.
    if existente and actualizar:
        respaldo = INFORMES / "Respaldos"
        respaldo.mkdir(exist_ok=True)
        sello = datetime.now().strftime("%Y%m%d_%H%M%S_%f")
        shutil.copy2(existente, respaldo / f"{existente.stem}_{sello}.pdf")

    fecha = datetime.now()

    # Nombre estable: evita duplicados
    ruta_pdf = INFORMES / f"Informe_{nombre_base}.pdf"

    AZUL = colors.HexColor("#17365D")
    CELESTE = colors.HexColor("#EAF2F8")
    GRIS = colors.HexColor("#555F6B")

    documento = SimpleDocTemplate(
        str(ruta_pdf),
        pagesize=A4,
        rightMargin=1.7 * cm,
        leftMargin=1.7 * cm,
        topMargin=1.7 * cm,
        bottomMargin=1.7 * cm
    )

    estilos = getSampleStyleSheet()

    estilos.add(ParagraphStyle(
        name="TituloSenhance",
        fontName="Helvetica-Bold",
        fontSize=17,
        leading=21,
        textColor=AZUL,
        alignment=TA_CENTER,
        spaceAfter=7
    ))

    estilos.add(ParagraphStyle(
        name="SubtituloSenhance",
        fontName="Helvetica",
        fontSize=10,
        leading=14,
        textColor=GRIS,
        alignment=TA_CENTER,
        spaceAfter=15
    ))

    estilos.add(ParagraphStyle(
        name="SeccionSenhance",
        fontName="Helvetica-Bold",
        fontSize=10,
        leading=14,
        textColor=AZUL,
        spaceBefore=12,
        spaceAfter=7
    ))

    estilos.add(ParagraphStyle(
        name="TextoSenhance",
        fontName="Helvetica",
        fontSize=9,
        leading=13,
        alignment=TA_LEFT,
        spaceAfter=7
    ))

    estilos.add(ParagraphStyle(
        name="NotaSenhance",
        fontName="Helvetica-Oblique",
        fontSize=8,
        leading=11,
        textColor=GRIS,
        spaceBefore=10
    ))

    contenido = []

    def agregar_seccion(titulo):
        contenido.append(
            Paragraph(titulo, estilos["SeccionSenhance"])
        )

    def imagen_ajustada(ruta, ancho_max, alto_max):
        ancho, alto = ImageReader(str(ruta)).getSize()
        escala = min(ancho_max / ancho, alto_max / alto)

        return Image(
            str(ruta),
            width=ancho * escala,
            height=alto * escala
        )

    def agregar_imagen(titulo, ruta):
        contenido.append(KeepTogether([
            Paragraph(
                f"<b>{escape(titulo)}</b>",
                estilos["TextoSenhance"]
            ),
            imagen_ajustada(
                ruta, 15.5 * cm, 6.2 * cm
            )
        ]))
        contenido.append(Spacer(1, 0.25 * cm))

    # ENCABEZADO
    contenido.append(Paragraph(
        "SIMULATORSENHANCE",
        estilos["TituloSenhance"]
    ))

    contenido.append(Paragraph(
        "Informe de analisis quirurgico asistido "
        "por vision artificial",
        estilos["SubtituloSenhance"]
    ))

    # INFORMACION GENERAL
    agregar_seccion("1. INFORMACION GENERAL")

    datos = [
        ["Paciente", paciente_dicom],
        ["Identificador", id_dicom],
        ["Procedimiento", PROCEDIMIENTO],
        ["Fecha del informe", fecha.strftime("%d/%m/%Y %H:%M")],
        ["Captura analizada", f"{nombre_base}.png"],
        ["Sistema", "SimulatorSenhance - Unity"]
    ]

    tabla = Table(
        [
            [
                Paragraph(
                    escape(str(celda)),
                    estilos["TextoSenhance"]
                )
                for celda in fila
            ]
            for fila in datos
        ],
        colWidths=[4.2 * cm, 12 * cm],
        hAlign="LEFT"
    )

    tabla.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (0, -1), CELESTE),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("LINEBELOW", (0, 0), (-1, -1), 0.3,
         colors.HexColor("#D9E2EA"))
    ]))

    contenido.append(tabla)

    # METODOLOGIA
    agregar_seccion("2. METODOLOGIA")

    contenido.append(Paragraph(
        "Se realizo el procesamiento de una captura del "
        "entorno quirurgico virtual SimulatorSenhance. "
        "MediaPipe se utilizo para identificar referencias "
        "anatomicas y delimitar una region abdominal aproximada. "
        "Posteriormente, OpenCV aplico segmentacion por color, "
        "operaciones morfologicas y analisis de contornos "
        "para localizar un candidato de incision.",
        estilos["TextoSenhance"]
    ))

    # EVIDENCIA VISUAL
    agregar_seccion("3. EVIDENCIA VISUAL")

    agregar_imagen(
        "Imagen original del simulador",
        imagen_original
    )

    agregar_imagen(
        "Mapeo anatomico: hombros, caderas y abdomen",
        imagen_abdomen
    )

    agregar_imagen(
        "Resultado del analisis de incision",
        imagen_incision
    )

    # RESULTADOS
    agregar_seccion("4. RESULTADOS DEL ANALISIS")

    contenido.append(Paragraph(
        "La imagen de mapeo anatomico muestra las referencias "
        "identificadas por el algoritmo. En el analisis de "
        "incision, cuando existe un candidato, el contorno "
        "verde representa la segmentacion, el rectangulo "
        "azul delimita la region detectada y el punto rojo "
        "indica su centro aproximado.",
        estilos["TextoSenhance"]
    ))

    if mediciones is not None:
        if mediciones.get("incision_detectada"):
            centro = mediciones.get("centro_px", [])
            ancho_px = mediciones.get("ancho_px", "No disponible")
            alto_px = mediciones.get("alto_px", "No disponible")
            area_px2 = mediciones.get("area_px2", "No disponible")
            valores = [
                ["Centro (px)", str(tuple(centro))],
                ["Ancho (px)", str(ancho_px)],
                ["Alto (px)", str(alto_px)],
                ["Area del contorno (px²)", str(area_px2)],
            ]
            tabla_mediciones = Table(valores, colWidths=[6 * cm, 10 * cm])
            tabla_mediciones.setStyle(TableStyle([
                ("BACKGROUND", (0, 0), (0, -1), CELESTE),
                ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
                ("TOPPADDING", (0, 0), (-1, -1), 6),
            ]))
            contenido.append(tabla_mediciones)
        else:
            contenido.append(Paragraph(
                "No se identificaron candidatos de incision con los criterios actuales.",
                estilos["TextoSenhance"]
            ))
    else:
        contenido.append(Paragraph(
            "No se dispone de mediciones numericas exportadas para esta captura.",
            estilos["TextoSenhance"]
        ))

    contenido.append(Paragraph(
        "Mediciones en pixeles de una imagen simulada; "
        "no equivalen a medidas anatomicas calibradas ni "
        "constituyen una validacion clinica.",
        estilos["NotaSenhance"]
    ))

    # OBSERVACIONES
    agregar_seccion("5. OBSERVACIONES")

    contenido.append(Paragraph(
        escape(OBSERVACIONES),
        estilos["TextoSenhance"]
    ))

    # CONCLUSION
    agregar_seccion("6. CONCLUSION")

    contenido.append(Paragraph(
        "Se documento el procesamiento de una captura "
        "quirurgica simulada mediante herramientas de vision "
        "artificial. Las imagenes adjuntas permiten revisar "
        "visualmente la delimitacion anatomica y el "
        "resultado del algoritmo.",
        estilos["TextoSenhance"]
    ))

    contenido.append(Paragraph(
        "Documento generado con fines academicos y de "
        "simulacion. No constituye un diagnostico medico "
        "ni un informe clinico validado.",
        estilos["NotaSenhance"]
    ))

    # PIE DE PAGINA
    def pie_pagina(canvas, doc):
        canvas.saveState()

        canvas.setStrokeColor(
            colors.HexColor("#D9E2EA")
        )

        canvas.line(
            1.7 * cm,
            1.3 * cm,
            A4[0] - 1.7 * cm,
            1.3 * cm
        )

        canvas.setFont("Helvetica", 8)
        canvas.setFillColor(GRIS)

        canvas.drawString(
            1.7 * cm,
            0.95 * cm,
            "SimulatorSenhance | Simulacion academica"
        )

        canvas.drawRightString(
            A4[0] - 1.7 * cm,
            0.95 * cm,
            f"Pagina {doc.page}"
        )

        canvas.restoreState()

    documento.build(
        contenido,
        onFirstPage=pie_pagina,
        onLaterPages=pie_pagina
    )

    print("INFORME GENERADO:", ruta_pdf)
    return ruta_pdf


# ============================================================
# EJECUCION DESDE TERMINAL O DESDE EL ANALISIS IA
# ============================================================

if __name__ == "__main__":
    parser = argparse.ArgumentParser()

    parser.add_argument(
        "--captura",
        default=None,
        help="Nombre base de la captura a documentar"
    )

    parser.add_argument(
        "--actualizar", action="store_true",
        help="Regenera el informe y respalda la version anterior"
    )
    argumentos = parser.parse_args()

    try:
        generar_informe(argumentos.captura, actualizar=argumentos.actualizar)
    except Exception as error:
        print("ERROR:", error)
        raise SystemExit(1)
