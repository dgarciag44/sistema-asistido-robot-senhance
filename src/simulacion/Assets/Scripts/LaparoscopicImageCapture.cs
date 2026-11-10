using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

// Sustituye el contenido de LaparoscopicImageCapture.cs por este codigo.
public class LaparoscopicImageCapture : MonoBehaviour
{
    [Header("Camara laparoscopica")]
    public Camera laparoscopicCamera;

    [Header("Resolucion de captura")]
    [Min(64)] public int imageWidth = 1280;
    [Min(64)] public int imageHeight = 720;

    [Header("Exportacion DICOM")]
    public bool saveDicom = true;

    [Header("Estado")]
    [SerializeField] private string lastSavedPath;
    [SerializeField] private string lastDicomPath;

    private bool capturing;
    private bool previousLeftCombo;
    private bool previousRightCombo;
    private int sequence;

    private void Update()
    {
        Gamepad pad = Gamepad.current;
        if (pad == null)
        {
            previousLeftCombo = false;
            previousRightCombo = false;
            return;
        }

        bool leftCombo = pad.leftShoulder.isPressed && pad.leftStickButton.isPressed;
        bool rightCombo = pad.rightShoulder.isPressed && pad.rightStickButton.isPressed;
        bool requested = (leftCombo && !previousLeftCombo) ||
                         (rightCombo && !previousRightCombo);

        previousLeftCombo = leftCombo;
        previousRightCombo = rightCombo;

        if (requested && !capturing)
            StartCoroutine(CaptureImage());
    }

    private IEnumerator CaptureImage()
    {
        capturing = true;
        if (laparoscopicCamera == null)
        {
            Debug.LogError("CAPTURA: Asigna la camara laparoscopica en el Inspector.");
            capturing = false;
            yield break;
        }

        yield return new WaitForEndOfFrame();

        RenderTexture target = null;
        Texture2D texture = null;
        RenderTexture oldActive = RenderTexture.active;
        RenderTexture oldTarget = laparoscopicCamera.targetTexture;

        try
        {
            int width = Mathf.Max(64, imageWidth);
            int height = Mathf.Max(64, imageHeight);
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            target.Create();

            laparoscopicCamera.targetTexture = target;
            laparoscopicCamera.Render();
            RenderTexture.active = target;

            texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();

            string folder = Path.Combine(Application.persistentDataPath, "CapturasLaparoscopicas");
            Directory.CreateDirectory(folder);
            string baseName = "Laparoscopia_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") +
                              "_" + (++sequence).ToString("D3");
            lastSavedPath = Path.Combine(folder, baseName + ".png");
            File.WriteAllBytes(lastSavedPath, texture.EncodeToPNG());
            Debug.Log("CAPTURA LAPAROSCOPICA GUARDADA: " + lastSavedPath);

            if (saveDicom)
            {
                try
                {
                    lastDicomPath = Path.Combine(folder, baseName + ".dcm");
                    DicomImageExporter.Save(texture, lastDicomPath);
                    Debug.Log("DICOM GUARDADO: " + lastDicomPath);
                }
                catch (Exception dicomException)
                {
                    Debug.LogError("ERROR AL EXPORTAR DICOM (PNG CONSERVADO): " + dicomException);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("ERROR AL GUARDAR CAPTURA: " + ex);
        }
        finally
        {
            laparoscopicCamera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            if (target != null)
            {
                target.Release();
                Destroy(target);
            }
            if (texture != null) Destroy(texture);
            capturing = false;
        }
    }
}
