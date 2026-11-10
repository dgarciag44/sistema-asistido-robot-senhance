using UnityEngine;
using UnityEngine.InputSystem;

// Prototipo: coloca puntos de cierre geometrico desde la punta de la pinza.
// No simula aguja, hilo ni penetracion de tejido.
[RequireComponent(typeof(SurgicalGrasperController))]
public class SurgicalSutureInteractor : MonoBehaviour
{
    [Header("Referencias")]
    public AbdominalTissueMesh abdominalTissue;

    [Header("Prueba de sutura")]
    [Min(0.001f)] public float maximumTipDistance = 0.035f;
    [Range(0f, 1f)] public float closure = 1f;

    private SurgicalGrasperController grasper;
    private RobotArmController robot;

    private void Awake()
    {
        grasper = GetComponent<SurgicalGrasperController>();
        robot = GetComponentInParent<RobotArmController>();
    }

    private void Update()
    {
        if (Keyboard.current == null || grasper == null || abdominalTissue == null || grasper.graspPoint == null)
            return;

        bool pressed = grasper.isLeftArm
            ? Keyboard.current.mKey.wasPressedThisFrame
            : Keyboard.current.nKey.wasPressedThisFrame;
        if (!pressed)
            return;

        if (robot != null && robot.IsEmergencyStopped)
            return;

        if (grasper.surgicalConsole != null)
        {
            var cameraSide = grasper.surgicalConsole.cameraSide;
            if ((grasper.isLeftArm && cameraSide == SurgicalConsoleController.CameraControlSide.Left) ||
                (!grasper.isLeftArm && cameraSide == SurgicalConsoleController.CameraControlSide.Right))
                return;
        }

        TryPlaceSuture();
    }

    private void TryPlaceSuture()
    {
        MeshFilter filter = abdominalTissue.GetComponent<MeshFilter>();
        Mesh mesh = filter != null ? filter.mesh : null;
        if (mesh == null)
            return;

        int subdivisions = abdominalTissue.subdivisions;
        int count = subdivisions + 1;
        int perSide = count * count;
        Vector3[] vertices = mesh.vertices;
        if (vertices.Length < perSide * 2)
            return;

        // Busca el vertice mas cercano de cualquiera de los dos labios de la incision.
        Vector3 tip = grasper.graspPoint.position;
        float nearestSqr = float.PositiveInfinity;
        int nearestRow = -1;
        for (int z = 0; z < count; z++)
        {
            int leftIndex = z * count + subdivisions;
            int rightIndex = perSide + z * count;
            Vector3 left = abdominalTissue.transform.TransformPoint(vertices[leftIndex]);
            Vector3 right = abdominalTissue.transform.TransformPoint(vertices[rightIndex]);
            float leftSqr = (tip - left).sqrMagnitude;
            float rightSqr = (tip - right).sqrMagnitude;
            float sqr = Mathf.Min(leftSqr, rightSqr);
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearestRow = z;
            }
        }

        if (nearestRow < 0 || nearestSqr > maximumTipDistance * maximumTipDistance)
        {
            Debug.Log("SUTURA: acerque la punta de la pinza al borde de la incision.");
            return;
        }

        float position = (float)nearestRow / subdivisions;
        abdominalTissue.AddSuturePoint(position, closure);
        Debug.Log($"SUTURA DE PRUEBA COLOCADA: {gameObject.name}, posicion {position:F2}");
    }
}
