
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class CameraViewSwitcher : MonoBehaviour
{
    [Header("Camaras del simulador")]
    public Camera medCamera;
    public Camera consoleCamera;
    public Camera laparoscopicCamera;
    public Camera mainCamera;

    [Header("Configuracion")]
    public bool startWithConsoleCamera = true;

    private enum ViewMode
    {
        Med,
        Console,
        Laparoscopic,
        Main
    }

    void Start()
    {
        ChangeView(
            startWithConsoleCamera
                ? ViewMode.Console
                : ViewMode.Laparoscopic
        );
    }

    void Update()
    {
        Gamepad pad = Gamepad.current;

        if (pad != null)
        {
            // SHARE - Vista del medico
            if (pad.selectButton.wasPressedThisFrame)
            {
                ChangeView(ViewMode.Med);
            }

            // OPTIONS - Vista de consola
            if (pad.startButton.wasPressedThisFrame)
            {
                ChangeView(ViewMode.Console);
            }

            // TOUCHPAD - Vista laparoscopica
            // L1 o R1 + TOUCHPAD - Main Camera
            if (TouchpadPressed(pad))
            {
                if (pad.leftShoulder.isPressed ||
                    pad.rightShoulder.isPressed)
                {
                    ChangeView(ViewMode.Main);
                }
                else
                {
                    ChangeView(ViewMode.Laparoscopic);
                }
            }
        }

        // Controles de respaldo con teclado
        if (Keyboard.current != null)
        {
            if (Keyboard.current.f1Key.wasPressedThisFrame)
                ChangeView(ViewMode.Med);

            if (Keyboard.current.f2Key.wasPressedThisFrame)
                ChangeView(ViewMode.Console);

            if (Keyboard.current.f3Key.wasPressedThisFrame)
                ChangeView(ViewMode.Laparoscopic);

            if (Keyboard.current.f4Key.wasPressedThisFrame)
                ChangeView(ViewMode.Main);
        }
    }

    bool TouchpadPressed(Gamepad pad)
    {
        // Compatible con controles cuyo touchpad
        // aparece como un boton adicional.
        foreach (var control in pad.allControls)
        {
            if (control is ButtonControl button &&
                (control.name == "touchpadButton" ||
                 control.name == "touchpad") &&
                button.wasPressedThisFrame)
            {
                return true;
            }
        }

        return false;
    }

    void ChangeView(ViewMode mode)
    {
        if (medCamera == null ||
            consoleCamera == null ||
            laparoscopicCamera == null ||
            mainCamera == null)
        {
            Debug.LogWarning(
                "CameraViewSwitcher: faltan referencias de camaras."
            );
            return;
        }

        medCamera.enabled = mode == ViewMode.Med;
        consoleCamera.enabled = mode == ViewMode.Console;
        laparoscopicCamera.enabled =
            mode == ViewMode.Laparoscopic;
        mainCamera.enabled = mode == ViewMode.Main;

        SetAudioListener(
            medCamera, mode == ViewMode.Med);

        SetAudioListener(
            consoleCamera, mode == ViewMode.Console);

        SetAudioListener(
            laparoscopicCamera,
            mode == ViewMode.Laparoscopic);

        SetAudioListener(
            mainCamera, mode == ViewMode.Main);

        Debug.Log("CAMARA ACTIVA: " + mode);
    }

    void SetAudioListener(Camera cam, bool active)
    {
        AudioListener listener =
            cam.GetComponent<AudioListener>();

        if (listener != null)
        {
            listener.enabled = active;
        }
    }
}
