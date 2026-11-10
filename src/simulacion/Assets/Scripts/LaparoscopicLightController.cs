
using UnityEngine;

public class LaparoscopicLightController : MonoBehaviour
{
    [Header("Referencias")]
    public SurgicalConsoleController surgicalConsole;
    public Light laparoscopicLight;

    void Start()
    {
        UpdateLight();
    }

    void Update()
    {
        UpdateLight();
    }

    void UpdateLight()
    {
        if (surgicalConsole == null ||
            laparoscopicLight == null)
            return;

        laparoscopicLight.enabled =
            surgicalConsole.cameraLightEnabled;
    }
}
