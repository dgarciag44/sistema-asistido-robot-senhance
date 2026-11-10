
import os
import subprocess
import sys
import threading
import re
import json
import shutil
import tkinter as tk

from tkinter import ttk, messagebox, filedialog
from pathlib import Path
from datetime import datetime
from generar_informe import generar_informe, buscar_informe

import pydicom
import fitz
from PIL import Image, ImageTk

# ============================================================
# SIMULATORSENHANCE - MINI PACS
# ============================================================

BASE = Path(__file__).resolve().parent

ARCHIVO_DICOM = BASE / "Archivo_DICOM"
RESULTADOS = BASE / "Resultados"
INFORMES = BASE / "Informes"
INDICE_CAPTURAS = BASE / "indice_capturas.json"
CAPTURAS_UNITY = Path(r"C:\Users\diego\AppData\LocalLow\DefaultCompany\SimulatorSenhance\CapturasLaparoscopicas")
proceso_activo = False

FONDO = "#F3F6FA"
BLANCO = "#FFFFFF"
AZUL = "#17365D"
TEXTO = "#263445"
GRIS = "#64748B"

estudios = []
filtrados = []


# ============================================================
# FUNCIONES GENERALES
# ============================================================

def fecha_legible(valor):
    try:
        return datetime.strptime(
            str(valor), "%Y%m%d"
        ).strftime("%d/%m/%Y")
    except (ValueError, TypeError):
        return "No disponible"


def nombre_estudio(carpeta):
    coincidencia = re.match(
        r"Estudio_(\d+)", carpeta.name
    )

    if coincidencia:
        return f"Estudio {int(coincidencia.group(1)):03d}"

    return carpeta.name


def archivos_ordenados(carpeta, extensiones):
    if not carpeta.exists():
        return []

    return sorted(
        (
            archivo
            for archivo in carpeta.rglob("*")
            if archivo.is_file()
            and archivo.suffix.lower() in extensiones
        ),
        key=lambda archivo: archivo.stat().st_mtime,
        reverse=True
    )


def estudio_seleccionado():
    seleccion = tabla.selection()

    if not seleccion:
        messagebox.showwarning(
            "Seleccion requerida",
            "Selecciona un estudio DICOM."
        )
        return None

    return filtrados[int(seleccion[0])]


# ============================================================
# ARCHIVO DICOM
# ============================================================

def buscar_estudios():
    encontrados = []

    if not ARCHIVO_DICOM.exists():
        return encontrados

    for paciente in ARCHIVO_DICOM.iterdir():
        if not paciente.is_dir():
            continue

        for carpeta in paciente.iterdir():
            if not carpeta.is_dir():
                continue

            archivos = sorted(carpeta.rglob("*.dcm"))

            if not archivos:
                continue

            try:
                ds = pydicom.dcmread(
                    str(archivos[0]),
                    stop_before_pixels=True
                )

                fecha = str(ds.get("StudyDate", ""))
                hora = str(ds.get("StudyTime", ""))

                encontrados.append({
                    "nombre_estudio": nombre_estudio(carpeta),
                    "paciente": str(
                        ds.get("PatientID", paciente.name)
                    ),
                    "nombre": str(
                        ds.get("PatientName", "")
                    ).replace("^", " ").strip(),
                    "fecha": fecha_legible(fecha),
                    "orden": fecha + hora,
                    "uid": str(
                        ds.get("StudyInstanceUID", "")
                    ),
                    "archivos": archivos,
                    "cantidad": len(archivos),
                    "modificado": archivos[0].stat().st_mtime
                })

            except Exception as error:
                print("Error DICOM:", carpeta, error)

    encontrados.sort(
        key=lambda e: (e["orden"], e["modificado"]),
        reverse=True
    )

    return encontrados


def actualizar_tabla():
    global filtrados

    busqueda = entrada_busqueda.get().strip().lower()
    paciente = combo_filtro.get()

    filtrados = []

    tabla.delete(*tabla.get_children())

    for estudio in estudios:
        texto = " ".join([
            estudio["nombre_estudio"],
            estudio["paciente"],
            estudio["nombre"],
            estudio["fecha"],
            estudio["uid"]
        ]).lower()

        if busqueda not in texto:
            continue

        if paciente != "Todos" and estudio["paciente"] != paciente:
            continue

        filtrados.append(estudio)

    for indice, estudio in enumerate(filtrados):
        tabla.insert(
            "",
            "end",
            iid=str(indice),
            values=(
                estudio["nombre_estudio"],
                estudio["paciente"],
                estudio["nombre"] or "No disponible",
                estudio["fecha"],
                estudio["cantidad"]
            )
        )

    etiqueta_resultados.config(
        text=f"{len(filtrados)} estudios encontrados"
    )


def ejecutar_en_segundo_plano(argumentos, al_terminar, titulo):
    global proceso_activo
    if proceso_activo:
        messagebox.showinfo("Proceso en curso", "Espera a que termine la operacion anterior.")
        return
    proceso_activo = True
    etiqueta_resultados.config(text=f"{titulo}... espera un momento")
    boton_actualizar.config(state="disabled")
    boton_ia.config(state="disabled")

    def trabajador():
        try:
            resultado = subprocess.run(
                [sys.executable, *map(str, argumentos)],
                cwd=str(BASE), capture_output=True, text=True,
                errors="replace", stdin=subprocess.DEVNULL,
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0)
            )
            error = None if resultado.returncode == 0 else (
                resultado.stderr.strip() or resultado.stdout.strip() or
                f"Proceso terminado con codigo {resultado.returncode}"
            )
        except Exception as exc:
            error = str(exc)

        def finalizar():
            global proceso_activo
            proceso_activo = False
            boton_actualizar.config(state="normal")
            boton_ia.config(state="normal")
            if error:
                messagebox.showerror(titulo, error[-3000:])
            al_terminar(error is None)
        ventana.after(0, finalizar)

    threading.Thread(target=trabajador, daemon=True).start()


def actualizar_y_archivar():
    archivador = BASE / "archivo_dicom.py"
    if not archivador.is_file():
        messagebox.showerror("Actualizar", "No existe archivo_dicom.py")
        return
    ejecutar_en_segundo_plano([archivador], lambda ok: recargar(), "Archivando DICOM")


def recargar():
    global estudios

    estudios = buscar_estudios()

    pacientes = sorted({
        e["paciente"] for e in estudios
    })

    combo_filtro["values"] = ["Todos", *pacientes]

    if combo_filtro.get() not in combo_filtro["values"]:
        combo_filtro.set("Todos")

    etiqueta_pacientes.config(text=str(len(pacientes)))
    etiqueta_estudios.config(text=str(len(estudios)))
    etiqueta_imagenes.config(
        text=str(sum(e["cantidad"] for e in estudios))
    )

    actualizar_tabla()


def abrir_weasis():
    estudio = estudio_seleccionado()

    if estudio is None:
        return

    try:
        os.startfile(str(estudio["archivos"][0]))
    except Exception as error:
        messagebox.showerror("Error Weasis", str(error))


# ============================================================
# METADATOS DICOM
# ============================================================

def ver_metadatos():
    estudio = estudio_seleccionado()

    if estudio is None:
        return

    try:
        ds = pydicom.dcmread(
            str(estudio["archivos"][0]),
            stop_before_pixels=True
        )
    except Exception as error:
        messagebox.showerror("Error DICOM", str(error))
        return

    ventana_meta = tk.Toplevel(ventana)
    ventana_meta.title(
        f"Metadatos - {estudio['nombre_estudio']}"
    )
    ventana_meta.geometry("1000x650")

    ttk.Label(
        ventana_meta,
        text=f"Metadatos DICOM - {estudio['nombre_estudio']}",
        font=("Segoe UI", 15, "bold")
    ).pack(pady=15)

    pestanas = ttk.Notebook(ventana_meta)
    pestanas.pack(
        fill="both", expand=True, padx=15, pady=10
    )

    resumen = ttk.Frame(pestanas)
    etiquetas = ttk.Frame(pestanas)

    pestanas.add(resumen, text="Resumen")
    pestanas.add(etiquetas, text="Etiquetas DICOM")

    def crear_tabla(parent, columnas, anchos):
        marco = ttk.Frame(parent)
        marco.pack(fill="both", expand=True, padx=10, pady=10)

        arbol = ttk.Treeview(
            marco, columns=columnas, show="headings"
        )

        for columna, ancho in zip(columnas, anchos):
            arbol.heading(columna, text=columna)
            arbol.column(columna, width=ancho)

        scroll_y = ttk.Scrollbar(
            marco, orient="vertical", command=arbol.yview
        )
        scroll_x = ttk.Scrollbar(
            marco, orient="horizontal", command=arbol.xview
        )

        arbol.configure(
            yscrollcommand=scroll_y.set,
            xscrollcommand=scroll_x.set
        )

        scroll_y.pack(side="right", fill="y")
        scroll_x.pack(side="bottom", fill="x")
        arbol.pack(fill="both", expand=True)

        return arbol

    tabla_resumen = crear_tabla(
        resumen, ("Campo", "Valor"), (260, 680)
    )

    campos = [
        ("Paciente", ds.get("PatientName", "")),
        ("ID del paciente", ds.get("PatientID", "")),
        ("Fecha", estudio["fecha"]),
        ("Hora", ds.get("StudyTime", "")),
        ("Modalidad", ds.get("Modality", "")),
        ("Descripcion", ds.get("StudyDescription", "")),
        ("Serie", ds.get("SeriesDescription", "")),
        ("Study UID", ds.get("StudyInstanceUID", "")),
        ("Series UID", ds.get("SeriesInstanceUID", "")),
        ("SOP UID", ds.get("SOPInstanceUID", "")),
        ("Filas", ds.get("Rows", "")),
        ("Columnas", ds.get("Columns", "")),
        ("Imagenes", estudio["cantidad"])
    ]

    for campo, valor in campos:
        tabla_resumen.insert(
            "", "end",
            values=(campo, str(valor) or "No disponible")
        )

    tabla_tags = crear_tabla(
        etiquetas,
        ("Tag", "VR", "Nombre", "Valor"),
        (120, 60, 250, 520)
    )

    for elemento in ds.iterall():
        if elemento.tag == (0x7FE0, 0x0010):
            continue

        tag = (
            f"({elemento.tag.group:04X},"
            f"{elemento.tag.element:04X})"
        )

        tabla_tags.insert(
            "", "end",
            values=(
                tag,
                elemento.VR,
                elemento.name,
                str(elemento.value)
            )
        )

    ttk.Button(
        ventana_meta,
        text="Cerrar",
        command=ventana_meta.destroy
    ).pack(pady=10)


# ============================================================
# VINCULAR CAPTURAS A ESTUDIO POR SOP INSTANCE UID
# ============================================================

def capturas_del_estudio(estudio):
    if not INDICE_CAPTURAS.is_file():
        raise FileNotFoundError(
            "No existe indice_capturas.json. Ejecuta archivo_dicom.py."
        )
    with INDICE_CAPTURAS.open("r", encoding="utf-8") as f:
        indice = json.load(f)
    if not isinstance(indice, dict):
        raise ValueError("El indice de capturas no es valido.")
    capturas = []
    for dicom in estudio["archivos"]:
        registro = indice.get(dicom.stem, {})
        if (isinstance(registro, dict)
                and registro.get("estudio_uid") == estudio["uid"]):
            captura = registro.get("captura", "")
            if captura and captura not in capturas:
                capturas.append(captura)
    return sorted(capturas)


def elegir_captura(estudio, capturas, al_elegir):
    if not capturas:
        messagebox.showinfo(
            "Sin captura asociada",
            "No hay capturas indexadas para este estudio."
        )
        return
    if len(capturas) == 1:
        al_elegir(capturas[0])
        return

    dialogo = tk.Toplevel(ventana)
    dialogo.title("Seleccionar captura del estudio")
    dialogo.geometry("660x170")
    dialogo.resizable(False, False)
    dialogo.transient(ventana)
    dialogo.grab_set()
    ttk.Label(dialogo, text="Selecciona la captura que deseas consultar:").pack(
        anchor="w", padx=18, pady=(18, 8)
    )
    selector = ttk.Combobox(dialogo, values=capturas, state="readonly", width=80)
    selector.pack(fill="x", padx=18)
    selector.current(0)
    def aceptar():
        captura = selector.get()
        dialogo.destroy()
        al_elegir(captura)
    ttk.Button(dialogo, text="Continuar", command=aceptar).pack(pady=14)


# ============================================================
# VISOR DE IMAGENES IA
# ============================================================


# ============================================================
# VISOR IA - IMAGENES DEL ESTUDIO SELECCIONADO
# ============================================================

def ver_imagenes_ia():

    estudio = estudio_seleccionado()

    if estudio is None:
        return

    try:
        capturas = capturas_del_estudio(estudio)
    except Exception as error:
        messagebox.showerror("Error de asociacion", str(error))
        return

    def mostrar_captura(captura):
        imagenes = []
        abdomen = RESULTADOS / f"{captura}_abdomen.png"
        incision = RESULTADOS / f"{captura}_incision.png"
        if abdomen.is_file():
            imagenes.append((abdomen, "1. Mapeo anatomico: hombros, cadera y abdomen"))
        if incision.is_file():
            imagenes.append((incision, "2. Deteccion de la incision quirurgica"))
        if len(imagenes) == 2:
            abrir_visor_imagenes(estudio, captura, imagenes)
            return
        if not messagebox.askyesno(
            "Analisis IA pendiente",
            "Esta captura aun no tiene los dos resultados IA.\n\n"
            "¿Deseas analizarlos ahora automaticamente?"
        ):
            return
        analizador = BASE / "analisis_quirurgico.py"
        if not analizador.is_file():
            messagebox.showerror("Analisis IA", "No existe analisis_quirurgico.py")
            return
        if not (CAPTURAS_UNITY / f"{captura}.png").is_file():
            messagebox.showerror("Analisis IA", "No se encuentra el PNG original de Unity.")
            return

        def terminado(ok):
            if not ok:
                recargar()
                return
            recargar()
            nuevas = []
            if abdomen.is_file():
                nuevas.append((abdomen, "1. Mapeo anatomico: hombros, cadera y abdomen"))
            if incision.is_file():
                nuevas.append((incision, "2. Deteccion de la incision quirurgica"))
            if nuevas:
                abrir_visor_imagenes(estudio, captura, nuevas)
            else:
                messagebox.showwarning("Analisis IA", "El proceso finalizo sin generar imagenes.")

        ejecutar_en_segundo_plano(
            [analizador, "--captura", captura, "--sin-ventanas"],
            terminado, "Analizando captura con IA"
        )

    elegir_captura(estudio, capturas, mostrar_captura)


def abrir_visor_imagenes(estudio, captura, imagenes):
    # ========================================================
    # VENTANA
    # ========================================================

    visor = tk.Toplevel(ventana)

    visor.title(
        f"SimulatorSenhance - IA - "
        f"{estudio['nombre_estudio']}"
    )

    visor.geometry("1050x750")
    visor.minsize(650, 450)
    visor.configure(bg=FONDO)

    indice_actual = [0]
    referencia = [None]

    ttk.Label(
        visor,
        text="ANALISIS QUIRURGICO POR IA",
        font=("Segoe UI", 15, "bold")
    ).pack(pady=(15, 5))

    ttk.Label(
        visor,
        text=(
            f"{estudio['nombre_estudio']} | "
            f"Captura: {captura}"
        )
    ).pack(pady=(0, 10))

    imagen_label = tk.Label(
        visor,
        bg="#172334",
        fg=BLANCO
    )

    imagen_label.pack(
        fill="both",
        expand=True,
        padx=20,
        pady=10
    )

    descripcion = ttk.Label(
        visor,
        text="",
        font=("Segoe UI", 10, "bold")
    )
    descripcion.pack(pady=(5, 2))

    informacion = ttk.Label(visor, text="")
    informacion.pack(pady=(0, 8))

    def mostrar():

        archivo, etapa = imagenes[indice_actual[0]]

        try:
            with Image.open(archivo) as original:
                imagen = original.copy()

            ancho = max(
                imagen_label.winfo_width() - 30,
                300
            )

            alto = max(
                imagen_label.winfo_height() - 30,
                200
            )

            imagen.thumbnail(
                (ancho, alto),
                Image.Resampling.LANCZOS
            )

            foto = ImageTk.PhotoImage(imagen)
            referencia[0] = foto

            imagen_label.configure(
                image=foto,
                text=""
            )

            descripcion.configure(text=etapa)

            informacion.configure(
                text=(
                    f"{archivo.name} | "
                    f"{indice_actual[0] + 1} "
                    f"de {len(imagenes)}"
                )
            )

        except Exception as error:
            messagebox.showerror(
                "Error de imagen",
                str(error),
                parent=visor
            )

    def cambiar(direccion):

        indice_actual[0] = (
            indice_actual[0] + direccion
        ) % len(imagenes)

        mostrar()

    controles = ttk.Frame(visor)
    controles.pack(pady=12)

    ttk.Button(
        controles,
        text="Anterior",
        command=lambda: cambiar(-1)
    ).pack(side="left", padx=8)

    ttk.Button(
        controles,
        text="Siguiente",
        command=lambda: cambiar(1)
    ).pack(side="left", padx=8)

    ttk.Button(
        controles,
        text="Cerrar",
        command=visor.destroy
    ).pack(side="left", padx=8)

    visor.after(100, mostrar
)



# ============================================================
# VISOR PDF INTEGRADO
# ============================================================

def ver_informes(informe_especifico=None):
    if informe_especifico is not None:
        informes = [Path(informe_especifico)]
    else:
        informes = archivos_ordenados(INFORMES, {".pdf"})

    if not informes:
        messagebox.showinfo(
            "Informes",
            "No se encontraron informes PDF."
        )
        return

    visor = tk.Toplevel(ventana)
    visor.title("SimulatorSenhance - Informes PDF")
    visor.geometry("1100x800")
    visor.minsize(750, 550)
    visor.configure(bg=FONDO)

    indice_informe = [0]
    pagina_actual = [0]
    documento = [None]
    referencia = [None]

    # Barra superior
    superior = ttk.Frame(visor)
    superior.pack(fill="x", padx=15, pady=12)

    ttk.Label(
        superior,
        text="VISOR DE INFORMES",
        font=("Segoe UI", 14, "bold")
    ).pack(side="left", padx=(0, 15))

    nombres = [archivo.name for archivo in informes]

    selector = ttk.Combobox(
        superior,
        values=nombres,
        state="readonly",
        width=55
    )
    selector.pack(
        side="left",
        fill="x",
        expand=True,
        padx=8
    )

    selector.current(0)

    # Area del PDF
    area = tk.Frame(visor, bg="#DCE3EC")
    area.pack(
        fill="both",
        expand=True,
        padx=15,
        pady=5
    )

    lienzo = tk.Canvas(
        area,
        bg="#DCE3EC",
        highlightthickness=0
    )

    scroll_y = ttk.Scrollbar(
        area,
        orient="vertical",
        command=lienzo.yview
    )

    scroll_x = ttk.Scrollbar(
        area,
        orient="horizontal",
        command=lienzo.xview
    )

    lienzo.configure(
        yscrollcommand=scroll_y.set,
        xscrollcommand=scroll_x.set
    )

    scroll_y.pack(side="right", fill="y")
    scroll_x.pack(side="bottom", fill="x")
    lienzo.pack(fill="both", expand=True)

    estado = ttk.Label(visor, text="")
    estado.pack(pady=5)

    def mostrar_pagina():
        doc = documento[0]

        if doc is None:
            return

        try:
            pagina = doc.load_page(pagina_actual[0])

            ancho_disponible = max(
                lienzo.winfo_width() - 45, 400
            )

            escala = min(
                ancho_disponible / pagina.rect.width,
                2.0
            )

            pix = pagina.get_pixmap(
                matrix=fitz.Matrix(escala, escala),
                alpha=False
            )

            imagen = Image.frombytes(
                "RGB",
                (pix.width, pix.height),
                pix.samples
            )

            foto = ImageTk.PhotoImage(imagen)
            referencia[0] = foto

            lienzo.delete("all")
            lienzo.create_image(
                15, 15,
                image=foto,
                anchor="nw"
            )

            lienzo.configure(
                scrollregion=(
                    0, 0,
                    pix.width + 30,
                    pix.height + 30
                )
            )

            lienzo.xview_moveto(0)
            lienzo.yview_moveto(0)

            estado.configure(
                text=(
                    f"Pagina {pagina_actual[0] + 1} "
                    f"de {len(doc)}"
                )
            )

        except Exception as error:
            messagebox.showerror(
                "Error de visualizacion PDF",
                str(error)
            )

    def cargar_informe(evento=None):
        if documento[0] is not None:
            documento[0].close()
            documento[0] = None

        indice_informe[0] = selector.current()

        archivo = informes[indice_informe[0]]

        try:
            documento[0] = fitz.open(str(archivo))

            if len(documento[0]) == 0:
                raise ValueError("El PDF no contiene paginas.")

            pagina_actual[0] = 0
            visor.after(50, mostrar_pagina)

        except Exception as error:
            messagebox.showerror(
                "Error al abrir informe",
                str(error)
            )

    def cambiar_pagina(direccion):
        doc = documento[0]

        if doc is None:
            return

        nueva = pagina_actual[0] + direccion

        if 0 <= nueva < len(doc):
            pagina_actual[0] = nueva
            mostrar_pagina()

    def descargar_informe():
        origen = informes[indice_informe[0]]

        destino = filedialog.asksaveasfilename(
            parent=visor,
            title="Guardar copia del informe",
            initialfile=origen.name,
            defaultextension=".pdf",
            filetypes=[("Documento PDF", "*.pdf")]
        )

        if not destino:
            return

        try:
            if Path(destino).resolve() == origen.resolve():
                messagebox.showinfo(
                    "Informe",
                    "El archivo ya se encuentra guardado "
                    "en esa ubicacion.",
                    parent=visor
                )
                return

            shutil.copy2(origen, destino)

            messagebox.showinfo(
                "Informe guardado",
                "Se guardo una copia del informe.",
                parent=visor
            )

        except Exception as error:
            messagebox.showerror(
                "Error al guardar",
                str(error),
                parent=visor
            )

    def cerrar():
        if documento[0] is not None:
            documento[0].close()

        visor.destroy()

    selector.bind(
        "<<ComboboxSelected>>",
        cargar_informe
    )

    controles = ttk.Frame(visor)
    controles.pack(pady=12)

    ttk.Button(
        controles,
        text="Pagina anterior",
        command=lambda: cambiar_pagina(-1)
    ).pack(side="left", padx=6)

    ttk.Button(
        controles,
        text="Pagina siguiente",
        command=lambda: cambiar_pagina(1)
    ).pack(side="left", padx=6)

    ttk.Button(
        controles,
        text="Descargar PDF",
        command=descargar_informe
    ).pack(side="left", padx=6)

    ttk.Button(
        controles,
        text="Cerrar",
        command=cerrar
    ).pack(side="left", padx=6)

    visor.protocol("WM_DELETE_WINDOW", cerrar)
    visor.after(100, cargar_informe)


# ============================================================
# INFORME PDF DEL ESTUDIO SELECCIONADO
# ============================================================

def ver_informe_estudio():
    estudio = estudio_seleccionado()
    if estudio is None:
        return
    try:
        capturas = capturas_del_estudio(estudio)
    except Exception as error:
        messagebox.showerror("Error de asociacion", str(error))
        return

    def abrir_o_generar(captura):
        informe = buscar_informe(captura)
        if informe is None:
            if not messagebox.askyesno(
                "Informe no encontrado",
                "No existe un informe para esta captura.\n\n"
                "¿Deseas generarlo usando sus resultados de IA?"
            ):
                return
            try:
                informe = generar_informe(captura)
            except Exception as error:
                messagebox.showerror("No se pudo generar", str(error))
                return
        ver_informes(informe_especifico=informe)

    elegir_captura(estudio, capturas, abrir_o_generar)


# ============================================================
# INTERFAZ PRINCIPAL
# ============================================================

ventana = tk.Tk()
ventana.title("SimulatorSenhance - Mini PACS")
ventana.geometry("1150x700")
ventana.minsize(900, 550)
ventana.configure(bg=FONDO)

estilo = ttk.Style()
estilo.theme_use("clam")

estilo.configure(
    "Treeview",
    background=BLANCO,
    foreground=TEXTO,
    fieldbackground=BLANCO,
    rowheight=35,
    font=("Segoe UI", 10)
)

estilo.configure(
    "Treeview.Heading",
    background="#E7EDF5",
    foreground=AZUL,
    font=("Segoe UI", 10, "bold"),
    padding=10
)

estilo.map(
    "Treeview",
    background=[("selected", "#DCEBFA")],
    foreground=[("selected", AZUL)]
)

# Encabezado
encabezado = tk.Frame(
    ventana,
    bg=BLANCO,
    padx=25,
    pady=18
)
encabezado.pack(fill="x")

tk.Label(
    encabezado,
    text="SIMULATORSENHANCE",
    font=("Segoe UI", 18, "bold"),
    fg=AZUL,
    bg=BLANCO
).pack(anchor="w")

tk.Label(
    encabezado,
    text="Mini-PACS | Gestion local de estudios DICOM",
    font=("Segoe UI", 10),
    fg=GRIS,
    bg=BLANCO
).pack(anchor="w")

# Contenido
contenido = tk.Frame(
    ventana,
    bg=FONDO,
    padx=25,
    pady=20
)
contenido.pack(fill="both", expand=True)

# Estadisticas
estadisticas = tk.Frame(
    contenido,
    bg=FONDO
)
estadisticas.pack(fill="x", pady=(0, 20))


def crear_tarjeta(titulo, columna):
    tarjeta = tk.Frame(
        estadisticas,
        bg=BLANCO,
        padx=20,
        pady=15
    )
    tarjeta.grid(
        row=0, column=columna,
        padx=6, sticky="ew"
    )

    tk.Label(
        tarjeta,
        text=titulo,
        font=("Segoe UI", 10),
        fg=GRIS,
        bg=BLANCO
    ).pack(anchor="w")

    valor = tk.Label(
        tarjeta,
        text="0",
        font=("Segoe UI", 23, "bold"),
        fg=AZUL,
        bg=BLANCO
    )
    valor.pack(anchor="w")

    return valor


for i in range(3):
    estadisticas.grid_columnconfigure(i, weight=1)

etiqueta_pacientes = crear_tarjeta("Pacientes", 0)
etiqueta_estudios = crear_tarjeta("Estudios DICOM", 1)
etiqueta_imagenes = crear_tarjeta("Imagenes archivadas", 2)

# Busqueda
barra = ttk.Frame(contenido)
barra.pack(fill="x", pady=(0, 15))

entrada_busqueda = ttk.Entry(
    barra,
    font=("Segoe UI", 11)
)
entrada_busqueda.pack(
    side="left",
    fill="x",
    expand=True,
    padx=(0, 10),
    ipady=5
)

entrada_busqueda.bind(
    "<KeyRelease>",
    lambda evento: actualizar_tabla()
)

combo_filtro = ttk.Combobox(
    barra,
    state="readonly",
    width=18
)
combo_filtro.pack(side="left", padx=(0, 10))
combo_filtro.set("Todos")

combo_filtro.bind(
    "<<ComboboxSelected>>",
    lambda evento: actualizar_tabla()
)

boton_actualizar = ttk.Button(
    barra,
    text="Actualizar",
    command=actualizar_y_archivar
)
boton_actualizar.pack(side="left")

# Tabla
marco_tabla = ttk.Frame(contenido)
marco_tabla.pack(fill="both", expand=True)

columnas = (
    "Estudio",
    "ID Paciente",
    "Paciente",
    "Fecha",
    "Imagenes"
)

tabla = ttk.Treeview(
    marco_tabla,
    columns=columnas,
    show="headings",
    selectmode="browse"
)

for columna in columnas:
    tabla.heading(columna, text=columna)

tabla.column("Estudio", width=140)
tabla.column("ID Paciente", width=140)
tabla.column("Paciente", width=260)
tabla.column("Fecha", width=140)
tabla.column("Imagenes", width=110, anchor="center")

scroll = ttk.Scrollbar(
    marco_tabla,
    orient="vertical",
    command=tabla.yview
)
tabla.configure(yscrollcommand=scroll.set)

scroll.pack(side="right", fill="y")
tabla.pack(fill="both", expand=True)

tabla.bind(
    "<Double-1>",
    lambda evento: abrir_weasis()
)

# Barra inferior
inferior = ttk.Frame(contenido)
inferior.pack(fill="x", pady=(15, 0))

etiqueta_resultados = ttk.Label(
    inferior,
    text="0 estudios encontrados"
)
etiqueta_resultados.pack(side="left")

acciones = ttk.Frame(inferior)
acciones.pack(side="right")

ttk.Button(
    acciones,
    text="Ver metadatos",
    command=ver_metadatos
).pack(side="left", padx=4)

ttk.Button(
    acciones,
    text="Visualizar en Weasis",
    command=abrir_weasis
).pack(side="left", padx=4)

boton_ia = ttk.Button(
    acciones,
    text="Ver imagenes IA",
    command=ver_imagenes_ia
)
boton_ia.pack(side="left", padx=4)

ttk.Button(
    acciones,
    text="Ver informe del estudio",
    command=ver_informe_estudio
).pack(side="left", padx=4)

# Iniciar
recargar()
ventana.mainloop()
