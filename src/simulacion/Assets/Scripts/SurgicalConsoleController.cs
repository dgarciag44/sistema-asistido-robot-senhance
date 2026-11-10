using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-200)]
public class SurgicalConsoleController : MonoBehaviour
{
    public enum CameraControlSide { None, Left, Right }

    [Header("Control de camara laparoscopica")]
    public CameraControlSide cameraSide = CameraControlSide.None;

    [Header("Precision")]
    public bool leftPrecision = false;
    public bool rightPrecision = false;
    public bool cameraPrecision = false;

    [Header("Configuracion")]
    [Range(0f, 0.5f)] public float deadZone = 0.15f;
    [Range(0f, 1f)] public float triggerThreshold = 0.5f;

    [Header("Luz laparoscopica")]
    public bool cameraLightEnabled = false;

    public Vector3 LeftMovement { get; private set; }
    public Vector3 RightMovement { get; private set; }
    public Vector3 CameraMovement { get; private set; }
    public Vector2 LeftStick { get; private set; }
    public Vector2 RightStick { get; private set; }
    public bool LeftShoulderHeld { get; private set; }
    public bool RightShoulderHeld { get; private set; }
    public float LeftLiftInput { get; private set; }
    public float RightLiftInput { get; private set; }
    public float LeftInsertionInput { get; private set; }
    public float RightInsertionInput { get; private set; }
    public bool LeftGripperOpen { get; private set; }
    public bool RightGripperOpen { get; private set; }

    public bool IsCameraMode => cameraSide != CameraControlSide.None;
    public bool IsLeftCameraControl => cameraSide == CameraControlSide.Left;
    public bool IsRightCameraControl => cameraSide == CameraControlSide.Right;

    private bool leftWaitingForNeutral;
    private bool rightWaitingForNeutral;
    private bool leftLightTriggerHeld;
    private bool rightLightTriggerHeld;

    void Update()
    {
        Gamepad gamepad = Gamepad.current;
        if (gamepad == null)
        {
            ResetInputs();
            return;
        }

        if (gamepad.dpad.left.wasPressedThisFrame)
            SelectCamera(CameraControlSide.Left);
        if (gamepad.buttonEast.wasPressedThisFrame)
            SelectCamera(CameraControlSide.Right);

        if (gamepad.dpad.down.wasPressedThisFrame)
        {
            if (IsLeftCameraControl) cameraPrecision = !cameraPrecision;
            else leftPrecision = !leftPrecision;
        }
        if (gamepad.buttonSouth.wasPressedThisFrame)
        {
            if (IsRightCameraControl) cameraPrecision = !cameraPrecision;
            else rightPrecision = !rightPrecision;
        }

        Vector2 leftStick = gamepad.leftStick.ReadValue();
        Vector2 rightStick = gamepad.rightStick.ReadValue();
        if (leftStick.magnitude < deadZone) leftStick = Vector2.zero;
        if (rightStick.magnitude < deadZone) rightStick = Vector2.zero;

        if (leftWaitingForNeutral)
        {
            if (leftStick == Vector2.zero) leftWaitingForNeutral = false;
            leftStick = Vector2.zero;
        }
        if (rightWaitingForNeutral)
        {
            if (rightStick == Vector2.zero) rightWaitingForNeutral = false;
            rightStick = Vector2.zero;
        }

        LeftShoulderHeld = gamepad.leftShoulder.isPressed;
        RightShoulderHeld = gamepad.rightShoulder.isPressed;

        Vector3 leftMovement = new Vector3(leftStick.x,
            LeftShoulderHeld ? leftStick.y : 0f,
            LeftShoulderHeld ? 0f : leftStick.y);
        Vector3 rightMovement = new Vector3(rightStick.x,
            RightShoulderHeld ? rightStick.y : 0f,
            RightShoulderHeld ? 0f : rightStick.y);

        LeftMovement = Vector3.zero;
        RightMovement = Vector3.zero;
        CameraMovement = Vector3.zero;
        LeftStick = Vector2.zero;
        RightStick = Vector2.zero;
        LeftLiftInput = RightLiftInput = 0f;
        LeftInsertionInput = RightInsertionInput = 0f;

        // Distribuir cada lado por separado. El brazo libre siempre responde.
        if (IsLeftCameraControl)
        {
            CameraMovement = leftMovement;
        }
        else
        {
            LeftMovement = leftMovement;
            LeftStick = leftStick;
            if (gamepad.dpad.up.isPressed)
                LeftLiftInput = LeftShoulderHeld ? -1f : 1f;
            if (gamepad.dpad.right.isPressed)
                LeftInsertionInput = LeftShoulderHeld ? -1f : 1f;
        }

        if (IsRightCameraControl)
        {
            CameraMovement = rightMovement;
        }
        else
        {
            RightMovement = rightMovement;
            RightStick = rightStick;
            if (gamepad.buttonNorth.isPressed)
                RightLiftInput = RightShoulderHeld ? -1f : 1f;
            if (gamepad.buttonWest.isPressed)
                RightInsertionInput = RightShoulderHeld ? -1f : 1f;
        }

        bool leftTrigger = gamepad.leftTrigger.ReadValue() >= triggerThreshold;
        bool rightTrigger = gamepad.rightTrigger.ReadValue() >= triggerThreshold;

        // Solo el gatillo del lado asignado a camara controla su luz.
        bool cameraTrigger = IsLeftCameraControl ? leftTrigger :
                             IsRightCameraControl && rightTrigger;
        bool cameraTriggerWasHeld = IsLeftCameraControl ? leftLightTriggerHeld :
                                    IsRightCameraControl && rightLightTriggerHeld;
        if (IsCameraMode && cameraTrigger && !cameraTriggerWasHeld)
        {
            cameraLightEnabled = !cameraLightEnabled;
            Debug.Log("LUZ LAPAROSCOPICA: " + (cameraLightEnabled ? "ON" : "OFF"));
        }

        LeftGripperOpen = !IsLeftCameraControl && leftTrigger;
        RightGripperOpen = !IsRightCameraControl && rightTrigger;
        leftLightTriggerHeld = leftTrigger;
        rightLightTriggerHeld = rightTrigger;
    }

    void SelectCamera(CameraControlSide requestedSide)
    {
        CameraControlSide previousSide = cameraSide;
        cameraSide = cameraSide == requestedSide ? CameraControlSide.None : requestedSide;

        // Neutralizar solo los lados que cambian de destino.
        if (previousSide == CameraControlSide.Left || cameraSide == CameraControlSide.Left)
            leftWaitingForNeutral = true;
        if (previousSide == CameraControlSide.Right || cameraSide == CameraControlSide.Right)
            rightWaitingForNeutral = true;

        Debug.Log("CONTROL ACTIVO: " + cameraSide);
    }

    void ResetInputs()
    {
        LeftMovement = RightMovement = CameraMovement = Vector3.zero;
        LeftStick = RightStick = Vector2.zero;
        LeftShoulderHeld = RightShoulderHeld = false;
        LeftLiftInput = RightLiftInput = 0f;
        LeftInsertionInput = RightInsertionInput = 0f;
        LeftGripperOpen = RightGripperOpen = false;
        leftLightTriggerHeld = rightLightTriggerHeld = false;
    }
}
