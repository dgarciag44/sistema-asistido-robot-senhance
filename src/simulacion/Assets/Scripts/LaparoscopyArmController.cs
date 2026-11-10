using UnityEngine;
using UnityEngine.InputSystem;

// Control manual exclusivo del tercer brazo, sin IK.
// Requiere SurgicalConsoleController de la Etapa 2.
public class LaparoscopyArmController : MonoBehaviour
{
    [Header("Articulaciones del brazo laparoscopico")]
    public Transform verticalLift;
    public Transform jointJ1;
    public Transform jointJ2;
    public Transform jointJ3;
    public Transform jointJ4;
    public Transform instrumentMount;

    [Header("Consola quirurgica")]
    public SurgicalConsoleController surgicalConsole;

    [Header("Velocidades")]
    public float rotationSpeed = 30f;
    public float linearSpeed = 0.15f;
    public float homeSpeed = 30f;
    public float homeLinearSpeed = 0.15f;

    [Header("Precision")]
    [Range(0.05f, 1f)] public float precisionMultiplier = 0.2f;

    [Header("Limites desde HOME")]
    public float liftMin = -0.05f;
    public float liftMax = 0.35f;
    public float j1Min = -90f;
    public float j1Max = 90f;
    public float j2Min = -0.05f;
    public float j2Max = 0.40f;
    public float j3Min = -75f;
    public float j3Max = 75f;
    public float j4Min = -20f;
    public float j4Max = 50f;
    public float insertionMin = -0.08f;
    public float insertionMax = 0.07f;

    [Header("Ejes locales")]
    public Vector3 j4Axis = Vector3.right;
    public Vector3 instrumentAxis = Vector3.forward;

    private Vector3 liftHome, j2Home, instrumentHome;
    private Quaternion j1Home, j3Home, j4Home;
    private float liftOffset, j1Angle, j2Extension, j3Angle, j4Angle, insertion;
    private bool returningHome;
    private bool emergencyStop;

    void Start()
    {
        if (verticalLift != null) liftHome = verticalLift.localPosition;
        if (jointJ1 != null) j1Home = jointJ1.localRotation;
        if (jointJ2 != null) j2Home = jointJ2.localPosition;
        if (jointJ3 != null) j3Home = jointJ3.localRotation;
        if (jointJ4 != null) j4Home = jointJ4.localRotation;
        if (instrumentMount != null) instrumentHome = instrumentMount.localPosition;
        ApplyMovements();
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            emergencyStop = !emergencyStop;
            returningHome = false;
        }
        if (emergencyStop) return;

        // HOME compartido con los brazos quirurgicos.
        if (keyboard != null && keyboard.hKey.wasPressedThisFrame)
            returningHome = true;

        if (returningHome)
        {
            ReturnHome();
            ApplyMovements();
            return;
        }

        if (surgicalConsole == null || Gamepad.current == null ||
            !surgicalConsole.IsCameraMode)
            return;

        bool fromLeft = surgicalConsole.IsLeftCameraControl;
        Gamepad pad = Gamepad.current;
        Vector2 stick = fromLeft ? pad.leftStick.ReadValue() : pad.rightStick.ReadValue();
        if (stick.magnitude < surgicalConsole.deadZone) stick = Vector2.zero;

        // La consola ya neutraliza CameraMovement tras cambiar de modo.
        Vector3 routed = surgicalConsole.CameraMovement;
        stick = new Vector2(routed.x, routed.y != 0f ? routed.y : routed.z);
        bool shoulder = fromLeft ? surgicalConsole.LeftShoulderHeld : surgicalConsole.RightShoulderHeld;
        float factor = surgicalConsole.cameraPrecision ? precisionMultiplier : 1f;
        float angularStep = rotationSpeed * factor * Time.deltaTime;
        float linearStep = linearSpeed * factor * Time.deltaTime;

        if (!shoulder)
        {
            j1Angle = Mathf.Clamp(j1Angle + stick.x * angularStep, j1Min, j1Max);
            j2Extension = Mathf.Clamp(j2Extension + stick.y * linearStep, j2Min, j2Max);
        }
        else
        {
            j3Angle = Mathf.Clamp(j3Angle + stick.x * angularStep, j3Min, j3Max);
            // Arriba = arriba, igual que los brazos quirurgicos ya validados.
            j4Angle = Mathf.Clamp(j4Angle - stick.y * angularStep, j4Min, j4Max);
        }

        // Elevacion e insercion con los botones del lado seleccionado.
        float liftInput = 0f;
        float insertionInput = 0f;
        if (fromLeft)
        {
            if (pad.dpad.up.isPressed) liftInput = shoulder ? -1f : 1f;
            if (pad.dpad.right.isPressed) insertionInput = shoulder ? -1f : 1f;
        }
        else
        {
            if (pad.buttonNorth.isPressed) liftInput = shoulder ? -1f : 1f;
            if (pad.buttonWest.isPressed) insertionInput = shoulder ? -1f : 1f;
        }
        liftOffset = Mathf.Clamp(liftOffset + liftInput * linearStep, liftMin, liftMax);
        insertion = Mathf.Clamp(insertion + insertionInput * linearStep, insertionMin, insertionMax);
        ApplyMovements();
    }

    void ReturnHome()
    {
        float a = homeSpeed * Time.deltaTime;
        float l = homeLinearSpeed * Time.deltaTime;
        liftOffset = Mathf.MoveTowards(liftOffset, 0f, l);
        j1Angle = Mathf.MoveTowards(j1Angle, 0f, a);
        j2Extension = Mathf.MoveTowards(j2Extension, 0f, l);
        j3Angle = Mathf.MoveTowards(j3Angle, 0f, a);
        j4Angle = Mathf.MoveTowards(j4Angle, 0f, a);
        insertion = Mathf.MoveTowards(insertion, 0f, l);
        returningHome = Mathf.Abs(liftOffset) > 0.0001f ||
            Mathf.Abs(j1Angle) > 0.001f || Mathf.Abs(j2Extension) > 0.0001f ||
            Mathf.Abs(j3Angle) > 0.001f || Mathf.Abs(j4Angle) > 0.001f ||
            Mathf.Abs(insertion) > 0.0001f;
    }

    void ApplyMovements()
    {
        if (verticalLift != null) verticalLift.localPosition = liftHome + Vector3.up * liftOffset;
        if (jointJ1 != null) jointJ1.localRotation = j1Home * Quaternion.AngleAxis(j1Angle, Vector3.up);
        if (jointJ2 != null) jointJ2.localPosition = j2Home + Vector3.forward * j2Extension;
        if (jointJ3 != null) jointJ3.localRotation = Quaternion.AngleAxis(j3Angle, Vector3.up) * j3Home;
        if (jointJ4 != null && j4Axis.sqrMagnitude > 0f)
            jointJ4.localRotation = j4Home * Quaternion.AngleAxis(j4Angle, j4Axis.normalized);
        if (instrumentMount != null && instrumentAxis.sqrMagnitude > 0f)
            instrumentMount.localPosition = instrumentHome + instrumentAxis.normalized * insertion;
    }
}
