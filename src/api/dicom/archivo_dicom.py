import json
import shutil
from pathlib import Path
import pydicom

BASE = Path(__file__).resolve().parent
ORIGEN = Path(r"C:\Users\diego\AppData\LocalLow\DefaultCompany\SimulatorSenhance\CapturasLaparoscopicas")
DESTINO = BASE / "Archivo_DICOM"
INDICE = BASE / "indice_capturas.json"


def organizar_dicom():
    if not ORIGEN.is_dir():
        print("No existe la carpeta de capturas:", ORIGEN)
        return
    DESTINO.mkdir(parents=True, exist_ok=True)
    if INDICE.exists():
        try:
            indice = json.loads(INDICE.read_text(encoding="utf-8"))
            if not isinstance(indice, dict):
                raise ValueError("Indice invalido")
        except (OSError, ValueError) as error:
            print("No se modifico el indice:", error)
            return
    else:
        indice = {}

    archivos = sorted(ORIGEN.glob("*.dcm"))
    estudios = {}
    copiados = existentes = 0
    for archivo in archivos:
        try:
            ds = pydicom.dcmread(str(archivo), stop_before_pixels=True)
            paciente = str(ds.get("PatientID", "SIN_ID")).strip() or "SIN_ID"
            estudio_uid = str(ds.get("StudyInstanceUID", "")).strip()
            serie_uid = str(ds.get("SeriesInstanceUID", "")).strip()
            sop_uid = str(ds.get("SOPInstanceUID", "")).strip()
            if not all((estudio_uid, serie_uid, sop_uid)):
                print("DICOM sin UID completo:", archivo.name)
                continue
            clave = (paciente, estudio_uid)
            if clave not in estudios:
                estudios[clave] = sum(k[0] == paciente for k in estudios) + 1
            numero = estudios[clave]
            carpeta = DESTINO / paciente / f"Estudio_{numero:03d}_{estudio_uid}" / f"Serie_{serie_uid}"
            carpeta.mkdir(parents=True, exist_ok=True)
            destino = carpeta / f"{sop_uid}.dcm"
            if destino.exists():
                existentes += 1
            else:
                shutil.copy2(archivo, destino)
                copiados += 1
            indice[sop_uid] = {
                "captura": archivo.stem,
                "paciente": paciente,
                "estudio_uid": estudio_uid,
                "serie_uid": serie_uid
            }
        except Exception as error:
            print("Error procesando", archivo.name, error)

    temporal = INDICE.with_name(INDICE.name + ".tmp")
    temporal.write_text(json.dumps(indice, indent=2, ensure_ascii=False), encoding="utf-8")
    temporal.replace(INDICE)
    print(f"Nuevos: {copiados}; existentes: {existentes}; indexados: {len(indice)}")
    print("Indice:", INDICE)


if __name__ == "__main__":
    organizar_dicom()
