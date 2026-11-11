# Documento Técnico – SimulatorSenhance

**Proyecto:** Sistema Asistido por Robot Quirúrgico Senhance  
**Autor:** Diego Alejandro García García  
**Miniproyecto:** #3 – Ingeniería Biomédica  
**Año:** 2026

---

## 1. Introducción

## 1. Introducción

SimulatorSenhance es un prototipo académico de simulación quirúrgica desarrollado en Unity, inspirado en el sistema robótico Senhance de Asensus Surgical.

El proyecto integra un entorno quirúrgico tridimensional, brazos robóticos articulados, instrumentos laparoscópicos y un sistema de control mediante DualShock 4, permitiendo simular movimientos y procedimientos quirúrgicos.

Adicionalmente, se desarrolló un sistema complementario en Python que permite gestionar las imágenes obtenidas desde Unity, organizar estudios en formato DICOM y realizar análisis experimentales mediante inteligencia artificial y procesamiento digital de imágenes.

Para visualizar los resultados, se implementó un Mini-PACS local que permite consultar estudios, visualizar imágenes mediante Weasis, revisar los resultados del análisis quirúrgico y acceder a informes generados automáticamente en formato PDF.

El propósito del proyecto es integrar conocimientos de robótica, programación, simulación médica y visión artificial en una plataforma educativa que represente diferentes etapas de un procedimiento quirúrgico asistido por robot.

**SimulatorSenhance tiene fines exclusivamente académicos y no está diseñado para uso clínico real.**

### 2.1 Entorno virtual

Desarrollado en Unity, incluye la representación del quirófano, paciente, camilla, brazos robóticos y cámara laparoscópica.

Los modelos tridimensionales representan los componentes necesarios para realizar las interacciones del simulador.

### 2.2 Control robótico

Los brazos se controlan mediante un DualShock 4 conectado a la computadora.

La programación en C# permite interpretar las entradas del control y ejecutar movimientos, incluyendo cinemática inversa, restricciones articulares y manipulación de instrumentos.

### 2.3 Captura de imágenes

La cámara virtual permite obtener imágenes del entorno quirúrgico.

Las capturas se almacenan localmente para su posterior procesamiento mediante Python.

### 2.4 Procesamiento de imágenes

Se implementaron algoritmos experimentales mediante OpenCV y MediaPipe.

OpenCV permite identificar regiones mediante color, brillo y contornos, mientras que MediaPipe se utiliza para estimar puntos anatómicos del paciente.

La detección automática del abdomen y la incisión se encuentra en fase experimental.

## 3. Tecnologías utilizadas

| Tecnología | Función |
|---|---|
| Unity | Simulación tridimensional |
| C# | Control de los sistemas robóticos |
| DualShock 4 | Interfaz de control |
| Python | Procesamiento de imágenes |
| OpenCV | Segmentación y detección de contornos |
| NumPy | Operaciones matriciales |
| MediaPipe | Estimación de puntos corporales |
| GitHub | Control de versiones y documentación |

## 4. Requisitos del sistema

### Hardware

- Computadora capaz de ejecutar el proyecto Unity.
- Control DualShock 4.
- Adaptador Bluetooth o conexión USB compatible.
- Espacio de almacenamiento para el proyecto y sus capturas.

### Software

- Unity Hub.
- Versión de Unity compatible con el proyecto.
- Python con las bibliotecas requeridas.
- Editor de código compatible con C# y Python.
- Git para clonar y administrar el repositorio.

Las versiones exactas de Unity y Python deben comprobarse en el entorno original antes de reproducir el proyecto.

## 5. Instalación y configuración

### 5.1 Obtener el repositorio

Clonar el proyecto desde GitHub:

```bash
git clone https://github.com/dgarciag44/sistema-asistido-robot-senhance.git
```

### 5.2 Abrir el simulador

1. Instalar Unity Hub.
2. Agregar el proyecto ubicado en `src/simulacion/`.
3. Instalar la versión de Unity indicada por el proyecto.
4. Abrir la escena `OperatingRoom_Main`.
5. Conectar el control DualShock 4.
6. Ejecutar la simulación mediante Play.

Este procedimiento requiere que los archivos del proyecto Unity hayan sido incorporados previamente al repositorio.

### 5.3 Configurar Python

Desde la carpeta correspondiente a los módulos Python, crear un entorno virtual:

```bash
python -m venv .venv
```

Activarlo en Windows:

```powershell
.venv\Scripts\activate
```

Instalar las dependencias una vez que se encuentre disponible `requirements.txt`:

```bash
pip install -r requirements.txt
```

Las dependencias principales utilizadas en las pruebas son OpenCV, NumPy y MediaPipe.

### 5.4 Configurar el modelo MediaPipe

El módulo experimental de reconocimiento corporal utiliza un modelo Pose Landmarker compatible con MediaPipe Tasks.

El archivo del modelo debe colocarse en la ubicación configurada en el script de detección corporal.

## 6. Control y cinemática del robot

El simulador implementa un sistema de control para los brazos quirúrgicos.

### 6.1 Movimiento manual

Los movimientos se realizan mediante los joysticks del DualShock 4, permitiendo controlar los brazos izquierdo y derecho.

### 6.2 Cinemática inversa

Se implementó un sistema de cinemática inversa para orientar el brazo hacia un objetivo virtual, considerando restricciones de movimiento.

### 6.3 Restricciones y seguridad

Se desarrollaron mecanismos de control que incluyen:

- Límites de movimiento de articulaciones.
- Restricción de alcance.
- Modo de precisión.
- Paro de emergencia virtual.
- Retorno a posición HOME.

### 6.4 Instrumentos quirúrgicos

Los instrumentos incorporan mecanismos de apertura y cierre de pinzas para interactuar con objetos del entorno.

## 7. Captura y procesamiento de imágenes

### 7.1 Adquisición

Las imágenes se obtienen desde el entorno virtual y se almacenan en el directorio de capturas de Unity.

### 7.2 Segmentación con OpenCV

El procesamiento experimental incluye:

1. Lectura de imágenes.
2. Conversión a espacios de color HSV.
3. Identificación de regiones oscuras y rojizas.
4. Aplicación de operaciones morfológicas.
5. Extracción de contornos.
6. Evaluación de regiones candidatas.
7. Estimación de centro, dimensiones y orientación.

### 7.3 Reconocimiento corporal

Mediante MediaPipe Pose Landmarker se realizaron pruebas para identificar hombros y caderas del paciente virtual.

A partir de estos puntos se desarrolló una aproximación geométrica para localizar la región abdominal.

El procedimiento requiere validación adicional ante diferentes perspectivas de cámara.

## 8. Módulos de datos médicos

La estructura del repositorio contempla:

- `datos/pacientes/`: expedientes ficticios.
- `datos/dicom_ejemplo/`: archivos DICOM de prueba.
- `src/api/`: módulos complementarios de procesamiento e integración.

La generación, validez y visualización integrada de los archivos DICOM deben verificarse antes de considerarse funcionalidades completadas.

## 9. Pruebas realizadas

| Prueba | Resultado |
|---|---|
| Conexión de DualShock 4 | Funcional |
| Movimiento de brazos | Funcional |
| Cinemática inversa | Funcional en las pruebas realizadas |
| Manipulación de pinzas | Funcional en las pruebas realizadas |
| Paro de emergencia virtual | Funcional |
| Retorno HOME | Funcional |
| Captura de imágenes desde Unity | Funcional |
| Segmentación de incisión con OpenCV | Parcialmente funcional |
| Reconocimiento corporal MediaPipe | Probado experimentalmente |
| Localización abdominal en distintas perspectivas | Pendiente de validación completa |
| Integración DICOM y expedientes | Pendiente de verificación |

## 10. Limitaciones

El simulador constituye un prototipo académico y presenta limitaciones relacionadas con la fidelidad física, la precisión anatómica y la integración de módulos complementarios.

La detección de incisiones depende de las condiciones visuales de la escena y no garantiza resultados correctos en todas las perspectivas.

Los algoritmos de visión artificial no están validados para uso clínico.

## 11. Trabajo futuro

Entre las posibles mejoras del proyecto se encuentran:

- Optimización de la detección automática del abdomen.
- Mayor precisión en la segmentación de incisiones.
- Integración completa del procesamiento de imágenes.
- Validación de la generación de archivos DICOM.
- Integración de expedientes médicos ficticios.
- Mejoras en la interacción con tejidos y suturas.

## 12. Conclusiones

El desarrollo de SimulatorSenhance permitió integrar conocimientos de programación, robótica, cinemática, modelado tridimensional y procesamiento digital de imágenes en un entorno virtual.

Se logró implementar un sistema de manipulación robótica mediante un control inalámbrico, incorporando funciones de movimiento, interacción y seguridad.

Asimismo, se exploró el uso de herramientas de visión artificial para identificar estructuras anatómicas e incisiones en imágenes generadas por el simulador.

El proyecto constituye una base funcional y experimental para continuar desarrollando herramientas educativas de simulación quirúrgica.

---

**Nota:** SimulatorSenhance es un prototipo exclusivamente educativo. No está destinado a procedimientos médicos reales.
