
import pydicom
from pathlib import Path

# Carpeta donde Unity guarda los archivos DICOM
CARPETA = Path(
    r"C:\Users\diego\AppData\LocalLow\DefaultCompany"
    r"\SimulatorSenhance\CapturasLaparoscopicas"
)

archivos = sorted(
    CARPETA.glob("*.dcm"),
    key=lambda archivo: archivo.stat().st_mtime,
    reverse=True
)

if not archivos:
    raise SystemExit("No se encontraron archivos DICOM.")

print("\nSIMULATORSENHANCE - VERIFICACION DICOM")
print("=" * 55)

# Revisar los tres archivos mas recientes
for archivo in archivos[:3]:

    ds = pydicom.dcmread(
        str(archivo),
        stop_before_pixels=True
    )

    print("\nArchivo:", archivo.name)
    print("Paciente:", ds.get("PatientName", "No definido"))
    print("PatientID:", ds.get("PatientID", "No definido"))
    print("StudyDate:", ds.get("StudyDate", "No definido"))
    print("StudyInstanceUID:", ds.get("StudyInstanceUID", "No definido"))
    print("SeriesInstanceUID:", ds.get("SeriesInstanceUID", "No definido"))
    print("SOPInstanceUID:", ds.get("SOPInstanceUID", "No definido"))

    print("-" * 55)
