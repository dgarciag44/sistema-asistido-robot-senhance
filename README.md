# Sistema Asistido por Robot Quirúrgico Senhance

**Miniproyecto #3 – Ingeniería Biomédica**

**Desarrollado por:** Diego Alejandro García García  
**Año:** 2026  
**Plataforma principal:** Unity

## 1. Descripción del proyecto

SimulatorSenhance es un simulador virtual de cirugía asistida por robot, desarrollado en Unity y basado en el sistema quirúrgico Senhance de Asensus Surgical.

El proyecto busca representar el funcionamiento de un sistema robótico quirúrgico mediante la manipulación de brazos articulados, instrumentos quirúrgicos y una cámara laparoscópica dentro de un entorno tridimensional.

Adicionalmente, se desarrollaron módulos en Python para el procesamiento de imágenes obtenidas del simulador y la integración experimental de inteligencia artificial.

El proyecto tiene fines exclusivamente educativos y de simulación.

## 2. Objetivo general

Desarrollar un entorno virtual de simulación quirúrgica que permita representar y controlar los movimientos de un sistema robótico, integrando herramientas de adquisición de imágenes y procesamiento digital.

## 3. Tecnologías utilizadas

| Tecnología | Aplicación |
|---|---|
| Unity | Desarrollo del entorno virtual tridimensional |
| C# | Programación del sistema robótico |
| DualShock 4 | Control inalámbrico de los brazos |
| Python | Procesamiento de imágenes |
| OpenCV | Detección de regiones e incisiones |
| MediaPipe | Reconocimiento experimental de puntos corporales |
| GitHub | Almacenamiento y documentación del proyecto |

## 4. Funcionalidades desarrolladas

### Simulación robótica

- Representación tridimensional del entorno quirúrgico.
- Manipulación de brazos robóticos articulados.
- Control de movimientos mediante DualShock 4.
- Implementación de cinemática inversa (IK).
- Restricciones de movimiento y alcance.
- Control de apertura y cierre de instrumentos quirúrgicos.
- Funciones de seguridad y retorno a posición inicial.

### Sistema de visualización

- Cámara laparoscópica virtual.
- Alternancia entre control de brazos y cámara.
- Captura de imágenes desde Unity.

### Procesamiento de imágenes e IA

- Identificación de regiones abdominales.
- Segmentación de regiones oscuras y rojizas.
- Detección experimental de incisiones.
- Estimación de contornos, dimensiones y orientación.
- Reconocimiento corporal mediante MediaPipe.

**Estado de la IA:** implementación experimental con resultados parciales.

## 5. Organización del repositorio

```text
src/
  simulacion/
  modelos/
  api/

datos/
  pacientes/
  dicom_ejemplo/

docs/
  img/
  documento_tecnico.md

README.md
GUIA_USUARIO.md
LICENSE.md
.gitignore
```

## 6. Documentación

- [Guía de usuario](GUIA_USUARIO.md)
- [Documento técnico](docs/documento_tecnico.md)
- [Licencia](LICENSE.md)

## 7. Video demostrativo

**Video del funcionamiento:** pendiente de incorporar enlace de YouTube.

El video demostrará el funcionamiento del entorno virtual, el control robótico y los módulos desarrollados.

## 8. Alcance del proyecto

Este simulador es un prototipo académico y no constituye un dispositivo médico certificado.

No está diseñado para procedimientos clínicos reales, diagnóstico médico ni tratamiento de pacientes.

Los resultados obtenidos mediante procesamiento de imágenes e inteligencia artificial son exclusivamente experimentales.
