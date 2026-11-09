# Guía de Usuario – SimulatorSenhance

**Proyecto:** Sistema Asistido por Robot Quirúrgico Senhance  
**Desarrollador:** Diego Alejandro García García  
**Año:** 2026

## 1. Introducción

SimulatorSenhance es un entorno virtual desarrollado en Unity que permite simular la manipulación de brazos robóticos quirúrgicos mediante un control inalámbrico DualShock 4.

Esta guía describe las principales funciones del simulador, los controles utilizados y el procedimiento para obtener y analizar imágenes del entorno quirúrgico.

## 2. Requisitos

- Computadora compatible con Unity.
- Proyecto SimulatorSenhance.
- Control DualShock 4 (PS4).
- Conexión Bluetooth o USB compatible.
- Python con OpenCV y MediaPipe para ejecutar los módulos experimentales de visión artificial.

## 3. Inicio del simulador

1. Abrir el proyecto SimulatorSenhance desde Unity Hub.
2. Esperar a que Unity cargue los recursos y la escena principal.
3. Conectar el control DualShock 4.
4. Verificar que el control sea reconocido por el sistema.
5. Presionar Play en Unity para iniciar la simulación.

Una vez iniciada la escena, el usuario puede manipular los brazos robóticos y visualizar el entorno quirúrgico.

## 4. Controles del sistema

El simulador utiliza un DualShock 4 para controlar los brazos quirúrgicos y la cámara laparoscópica.

| Control | Función |
|---|---|
| Joystick izquierdo | Desplazamiento del brazo izquierdo |
| Joystick derecho | Desplazamiento del brazo derecho |
| L1 (mantener) | Habilitar desplazamiento vertical del brazo izquierdo |
| R1 (mantener) | Habilitar desplazamiento vertical del brazo derecho |
| L2 | Control de la pinza izquierda en modo brazo |
| R2 | Control de la pinza derecha en modo brazo |
| Cruceta abajo | Alternar modo precisión del brazo izquierdo |
| Botón X | Alternar modo precisión del brazo derecho |
| Cruceta izquierda | Alternar entre brazo izquierdo y cámara |
| Botón Triángulo | Alternar entre brazo derecho y cámara |
| Touchpad | Retorno a posición HOME |

El sistema también incorpora funciones de paro de emergencia y control mediante cinemática inversa.

## 5. Funcionamiento de los brazos robóticos

Los brazos quirúrgicos permiten realizar movimientos controlados dentro del entorno virtual.

El usuario puede manipular ambos brazos mediante los joysticks del DualShock 4, respetando las restricciones de movimiento establecidas en la simulación.

El modo de precisión permite reducir la velocidad de desplazamiento para realizar movimientos más controlados.

## 6. Control de la cámara laparoscópica

El simulador incorpora una cámara virtual que permite observar el entorno quirúrgico.

Al activar el modo cámara, el joystick correspondiente deja de controlar el brazo y pasa a controlar la cámara.

Durante este modo, los gatillos correspondientes se destinan al control de iluminación, mientras que el brazo y su pinza permanecen sin recibir órdenes de movimiento de ese lado.

El otro lado del control puede continuar manipulando su brazo.

## 7. Captura de imágenes

El simulador permite generar capturas del entorno quirúrgico desde Unity.

Las imágenes se almacenan en la carpeta:

```text
AppData/LocalLow/DefaultCompany/
SimulatorSenhance/CapturasLaparoscopicas/
```

Estas capturas pueden utilizarse para pruebas de procesamiento digital de imágenes.

## 8. Procesamiento de imágenes

Se desarrollaron scripts en Python que permiten analizar las imágenes obtenidas desde Unity.

Entre las funciones experimentales se incluyen:

- Localización aproximada de regiones abdominales.
- Identificación de zonas oscuras y rojizas.
- Detección de contornos de incisiones.
- Cálculo de centros y dimensiones en píxeles.
- Estimación de orientación de la incisión.
- Reconocimiento de puntos corporales mediante MediaPipe.

Para ejecutar los módulos, se debe disponer de las dependencias correspondientes y configurar las rutas de las imágenes.

Ejemplo de ejecución:

```bash
python detectar_pose.py
```

El reconocimiento corporal mediante MediaPipe fue probado experimentalmente, obteniendo puntos de referencia de hombros y caderas.

## 9. Capturas de pantalla

En esta sección se incorporarán evidencias visuales del funcionamiento del simulador:

- Entorno quirúrgico tridimensional.
- Brazos robóticos y paciente.
- Control de cámara laparoscópica.
- Capturas generadas desde Unity.
- Resultados del procesamiento de imágenes.
- Detección corporal mediante MediaPipe.

Las imágenes se almacenarán en `docs/img/`.

## 10. Limitaciones

El proyecto constituye un prototipo académico.

Las funciones de procesamiento de imágenes e inteligencia artificial se encuentran en fase experimental y pueden presentar variaciones dependiendo de la posición de la cámara, la iluminación y las características de la escena.

No debe utilizarse para procedimientos quirúrgicos reales, diagnóstico ni decisiones clínicas.
