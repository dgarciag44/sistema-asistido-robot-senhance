
using UnityEngine;
using UnityEngine.InputSystem;

public class RobotArmController : MonoBehaviour
{
    [Header("Articulaciones")]
    public Transform verticalLift;
    public Transform jointJ1;
    public Transform jointJ2;
    public Transform jointJ3;
    public Transform jointJ4;
    public Transform instrumentMount;

    [Header("Consola quirurgica")]
    public SurgicalConsoleController surgicalConsole;

    [Header("Referencias visuales (compatibilidad)")]
    [Tooltip("Referencia conservada por compatibilidad. El control manual ya no depende de la camara.")]
    public Transform laparoscopicCamera;

    [Tooltip("Referencia conservada por compatibilidad; no se usa para mapear los joysticks.")]
    public Transform endEffector;

    [Header("Inversion manual de ejes")]
    [InspectorName("Invertir J2 (hacia paciente)")]
    [Tooltip("Invierte el sentido de J2 del brazo seleccionado.")]
    public bool invertCameraHorizontal = false;

    [InspectorName("Invertir J1 (alejar/acercar)")]
    [Tooltip("Invierte el sentido de J1 del brazo seleccionado.")]
    public bool invertCameraDepth = false;

    [Header("Identificacion del brazo")]
    public bool isLeftArm = true;

    [Header("Velocidades")]
    public float rotationSpeed = 45f;
    public float linearSpeed = 0.15f;
    public float homeSpeed = 30f;
    public float homeLinearSpeed = 0.15f;

    [Header("Control de precision")]
    [Range(0.05f, 1f)]
    public float precisionMultiplier = 0.2f;

    [Header("Limites de elevacion (metros desde Home)")]
    public float liftMin = 0f;
    public float liftMax = 0.30f;

    [Header("Limites J1 (grados)")]
    public float j1Min = -90f;
    public float j1Max = 90f;

    [Header("Limites J2 telescopico (metros desde Home)")]
    public float j2Min = 0f;
    public float j2Max = 0.25f;

    [Header("Limites J3 axial (grados)")]
    public float j3Min = -180f;
    public float j3Max = 180f;

    [Header("Limites J4 inclinacion (grados)")]
    public float j4Min = -60f;
    public float j4Max = 60f;

    [Header("Insercion del instrumento (metros desde Home)")]
    public float insertionMin = 0f;
    public float insertionMax = 0.15f;

    [Header("Ejes locales")]
    public Vector3 j3Axis = Vector3.up;
    public Vector3 j4Axis = Vector3.right;
    public Vector3 instrumentAxis = Vector3.forward;

    // Control manual exclusivamente: sin IK ni referencias a IK_Target.

    private Vector3 liftHome;
    private Vector3 j2Home;
    private Vector3 instrumentHome;

    private Quaternion j1Home;
    private Quaternion j3Home;
    private Quaternion j4Home;

    private float liftOffset;
    private float j1Angle;
    private float j2Extension;
    private float j3Angle;
    private float j4Angle;
    private float insertion;

    private bool emergencyStop;
    private bool returningHome;

    public bool IsEmergencyStopped => emergencyStop;
    // Compatibilidad temporal con scripts IK antiguos desactivados.
    public bool IsIKMode => false;

    void Start()
    {
        // Guardar exactamente la postura inicial de Unity.
        if (verticalLift != null)
            liftHome = verticalLift.localPosition;

        if (jointJ1 != null)
            j1Home = jointJ1.localRotation;

        if (jointJ2 != null)
            j2Home = jointJ2.localPosition;

        if (jointJ3 != null)
            j3Home = jointJ3.localRotation;

        if (jointJ4 != null)
            j4Home = jointJ4.localRotation;

        if (instrumentMount != null)
            instrumentHome = instrumentMount.localPosition;

        ApplyMovements();
    }

    // Se conserva para compatibilidad con otros scripts.
    public void SynchronizeAngles()
    {
        // El nuevo controlador mantiene sus valores internos
        // sincronizados al aplicar cada movimiento.
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;

        // Parada de emergencia.
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            emergencyStop = !emergencyStop;
            returningHome = false;

            Debug.Log(emergencyStop
                ? "PARO VIRTUAL ACTIVADO"
                : "PARO VIRTUAL LIBERADO");
        }

        if (emergencyStop)
            return;

        // L3, R3 y Tab no cambian de modo: IK eliminado.

        if (keyboard != null && keyboard.hKey.wasPressedThisFrame)
        {
            returningHome = true;
            Debug.Log("Regresando a HOME");
        }

        if (returningHome)
        {
            ReturnHome();
        }
        else
        {
            // Solo se bloquea el brazo cuyo lado controla laparoscopia.
            bool cameraActive = surgicalConsole != null &&
                (isLeftArm ? surgicalConsole.IsLeftCameraControl
                           : surgicalConsole.IsRightCameraControl);

            if (!cameraActive)
            {
                if (keyboard != null)
                    ReadManualInput(keyboard);
                ReadGamepadInput();
            }
        }

        ApplyMovements();
    }

    void ReadManualInput(Keyboard keyboard)
    {
        bool precision =
            keyboard.leftShiftKey.isPressed ||
            keyboard.rightShiftKey.isPressed;

        float factor = precision ? precisionMultiplier : 1f;

        float angularStep =
            rotationSpeed * factor * Time.deltaTime;

        float linearStep =
            linearSpeed * factor * Time.deltaTime;

        // VerticalLift: T / G
        float liftInput =
            GetAxis(keyboard.tKey.isPressed,
                    keyboard.gKey.isPressed);

        // J1: A / D
        float j1Input =
            GetAxis(keyboard.dKey.isPressed,
                    keyboard.aKey.isPressed);

        // J2 telescopico: S / W
        float j2Input =
            GetAxis(keyboard.sKey.isPressed,
                    keyboard.wKey.isPressed);

        // J3 rotacion axial: E / Q
        float j3Input =
            GetAxis(keyboard.eKey.isPressed,
                    keyboard.qKey.isPressed);

        // J4 inclinacion: U / J
        float j4Input =
            GetAxis(keyboard.uKey.isPressed,
                    keyboard.jKey.isPressed);

        // Instrumento: I / K
        float instrumentInput =
            GetAxis(keyboard.iKey.isPressed,
                    keyboard.kKey.isPressed);

        liftOffset = Mathf.Clamp(
            liftOffset + liftInput * linearStep,
            liftMin, liftMax);

        j1Angle = Mathf.Clamp(
            j1Angle + j1Input * angularStep,
            j1Min, j1Max);

        j2Extension = Mathf.Clamp(
            j2Extension + j2Input * linearStep,
            j2Min, j2Max);

        j3Angle = Mathf.Clamp(
            j3Angle + j3Input * angularStep,
            j3Min, j3Max);

        j4Angle = Mathf.Clamp(
            j4Angle + j4Input * angularStep,
            j4Min, j4Max);

        insertion = Mathf.Clamp(
            insertion + instrumentInput * linearStep,
            insertionMin, insertionMax);
    }

    float GetAxis(bool positive, bool negative)
    {
        return (positive ? 1f : 0f) -
               (negative ? 1f : 0f);
    }

    void ReturnHome()
    {
        float angularStep = homeSpeed * Time.deltaTime;
        float linearStep = homeLinearSpeed * Time.deltaTime;

        liftOffset = Mathf.MoveTowards(
            liftOffset, 0f, linearStep);

        j1Angle = Mathf.MoveTowards(
            j1Angle, 0f, angularStep);

        j2Extension = Mathf.MoveTowards(
            j2Extension, 0f, linearStep);

        j3Angle = Mathf.MoveTowards(
            j3Angle, 0f, angularStep);

        j4Angle = Mathf.MoveTowards(
            j4Angle, 0f, angularStep);

        insertion = Mathf.MoveTowards(
            insertion, 0f, linearStep);

        if (Mathf.Approximately(liftOffset, 0f) &&
            Mathf.Approximately(j1Angle, 0f) &&
            Mathf.Approximately(j2Extension, 0f) &&
            Mathf.Approximately(j3Angle, 0f) &&
            Mathf.Approximately(j4Angle, 0f) &&
            Mathf.Approximately(insertion, 0f))
        {
            returningHome = false;
            Debug.Log("HOME alcanzado");
        }
    }


    void ReadGamepadInput()
    {
        if (surgicalConsole == null)
            return;

        if (Gamepad.current == null)
            return;

        if (isLeftArm ? surgicalConsole.IsLeftCameraControl
                      : surgicalConsole.IsRightCameraControl)
            return;

        // Seleccionar el lado correspondiente.
        Vector2 stick = isLeftArm
            ? surgicalConsole.LeftStick
            : surgicalConsole.RightStick;

        bool shoulderHeld = isLeftArm
            ? surgicalConsole.LeftShoulderHeld
            : surgicalConsole.RightShoulderHeld;

        bool precision = isLeftArm
            ? surgicalConsole.leftPrecision
            : surgicalConsole.rightPrecision;

        float factor = precision
            ? precisionMultiplier
            : 1f;

        float angularStep =
            rotationSpeed * factor * Time.deltaTime;

        float linearStep =
            linearSpeed * factor * Time.deltaTime;

        // ==================================
        // JOYSTICK SIN L1 / R1
        // ==================================

        if (!shoulderHeld)
        {
            // Distribucion mecanica FIJA, sin IK ni calculos de camara.
            // Stick vertical: J1. Arriba = alejar; abajo = acercar.
            // Stick horizontal: J2. Hacia el paciente:
            //   izquierdo = derecha; derecho = izquierda.
            // Los signos finales pueden invertirse en el Inspector
            // si la orientacion inicial de algun modelo es distinta.

            float j1Direction = invertCameraDepth ? -1f : 1f;
            float j2Direction = (isLeftArm ? 1f : -1f) *
                                (invertCameraHorizontal ? -1f : 1f);

            j1Angle = Mathf.Clamp(
                j1Angle + stick.y * j1Direction * angularStep,
                j1Min, j1Max);

            j2Extension = Mathf.Clamp(
                j2Extension + stick.x * j2Direction * linearStep,
                j2Min, j2Max);
        }
        else
        {
            // ==================================
            // L1 / R1 + JOYSTICK
            // ==================================

            // Horizontal: J3 lateral
            j3Angle = Mathf.Clamp(
                j3Angle + stick.x * angularStep,
                j3Min,
                j3Max);

            // Vertical: J4 inclinacion
            j4Angle = Mathf.Clamp(
                j4Angle - stick.y * angularStep,
                j4Min,
                j4Max);
        }

        // ==================================
        // ELEVACION DE COLUMNA
        // ==================================

        float liftInput = isLeftArm
            ? surgicalConsole.LeftLiftInput
            : surgicalConsole.RightLiftInput;

        if (Mathf.Abs(liftInput) > 0.01f)
        {
            Debug.Log("ELEVACION RECIBIDA: " + liftInput);
        }

        liftOffset = Mathf.Clamp(
            liftOffset + liftInput * linearStep,
            liftMin,
            liftMax);

        // ==================================
        // INSERCION DEL INSTRUMENTO
        // ==================================

        float insertionInput = isLeftArm
            ? surgicalConsole.LeftInsertionInput
            : surgicalConsole.RightInsertionInput;

        if (Mathf.Abs(insertionInput) > 0.01f)
        {
            Debug.Log("INSERCION RECIBIDA: " + insertionInput);
        }

        insertion = Mathf.Clamp(
            insertion + insertionInput * linearStep,
            insertionMin,
            insertionMax);
    }


    void ApplyMovements()
    {
        // Elevacion vertical.
        if (verticalLift != null)
        {
            verticalLift.localPosition =
                liftHome + Vector3.up * liftOffset;
        }

        // J1: giro horizontal.
        if (jointJ1 != null)
        {
            jointJ1.localRotation =
                j1Home *
                Quaternion.AngleAxis(j1Angle, Vector3.up);
        }

        // J2: desplazamiento telescopico.
        if (jointJ2 != null)
        {
            jointJ2.localPosition =
                j2Home + Vector3.forward * j2Extension;
        }

        // J3: giro lateral sobre el eje Y del padre.
        if (jointJ3 != null)
        {
            jointJ3.localRotation =
                Quaternion.AngleAxis(
                    j3Angle,
                    Vector3.up
                ) * j3Home;
        }

        // J4: inclinacion del extremo.
        if (jointJ4 != null && j4Axis.sqrMagnitude > 0f)
        {
            jointJ4.localRotation =
                j4Home *
                Quaternion.AngleAxis(
                    j4Angle, j4Axis.normalized);
        }

        // Avance y retroceso del instrumento completo.
        if (instrumentMount != null &&
            instrumentAxis.sqrMagnitude > 0f)
        {
            instrumentMount.localPosition =
                instrumentHome +
                instrumentAxis.normalized * insertion;
        }
    }
}
