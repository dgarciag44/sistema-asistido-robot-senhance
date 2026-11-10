using UnityEngine;

// Prototipo educativo: dos contactos de la punta (uno por borde)
// completan cada una de las cuatro suturas geométricas.
public class SurgicalNeedleController : MonoBehaviour
{
    [Header("Referencias")]
    public Transform needleTip;
    public AbdominalTissueMesh abdominalTissue;

    [Header("Deteccion")]
    [Min(0.001f)] public float contactDistance = 0.012f;
    [Min(1f)] public float rearmMultiplier = 1.5f;
    public bool requireNeedleHeld = true;

    [Header("Cierre")]
    [Range(0f, 1f)] public float sutureClosure = 1f;
    public bool showGizmos = true;

    private readonly float[] positions = { 0.2f, 0.4f, 0.6f, 0.8f };
    private readonly bool[,] contacted = new bool[4, 2];
    private readonly bool[] completed = new bool[4];
    private bool armed = true;
    private Transform originalParent;

    private void Awake()
    {
        originalParent = transform.parent;
    }

    private void Update()
    {
        if (needleTip == null || abdominalTissue == null)
            return;

        // SurgicalGrasperController cambia el padre de la aguja
        // al graspPoint mientras esta sujetada.
        if (requireNeedleHeld && transform.parent == originalParent)
        {
            armed = false;
            return;
        }

        float nearestDistance = float.PositiveInfinity;
        int nearestSlot = -1;
        int nearestSide = -1;

        for (int slot = 0; slot < positions.Length; slot++)
        {
            if (completed[slot])
                continue;

            for (int side = 0; side < 2; side++)
            {
                if (contacted[slot, side])
                    continue;

                Vector3 target;
                if (!TryGetEdgePosition(positions[slot], side, out target))
                    continue;

                float distance = Vector3.Distance(needleTip.position, target);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestSlot = slot;
                    nearestSide = side;
                }
            }
        }

        // La aguja debe retirarse antes de registrar otro contacto.
        if (!armed)
        {
            if (nearestDistance > contactDistance * rearmMultiplier)
                armed = true;
            return;
        }

        if (nearestSlot < 0 || nearestDistance > contactDistance)
            return;

        contacted[nearestSlot, nearestSide] = true;
        armed = false;

        Debug.Log($"AGUJA: punto {nearestSlot + 1}, borde " +
                  (nearestSide == 0 ? "izquierdo" : "derecho") + " detectado");

        if (contacted[nearestSlot, 0] && contacted[nearestSlot, 1])
        {
            completed[nearestSlot] = true;
            abdominalTissue.AddSuturePoint(positions[nearestSlot], sutureClosure);
            Debug.Log($"SUTURA {nearestSlot + 1}/4 COMPLETADA");
        }
    }

    // Obtiene la posicion real del borde en la malla deformable.
    private bool TryGetEdgePosition(float position, int side, out Vector3 world)
    {
        world = Vector3.zero;
        MeshFilter filter = abdominalTissue.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            return false;

        int subdivisions = abdominalTissue.subdivisions;
        if (subdivisions < 1)
            return false;

        int count = subdivisions + 1;
        int perSide = count * count;
        int row = Mathf.Clamp(Mathf.RoundToInt(position * subdivisions), 0, subdivisions);
        int index = side == 0
            ? row * count + subdivisions
            : perSide + row * count;

        Vector3[] vertices = filter.sharedMesh.vertices;
        if (index < 0 || index >= vertices.Length)
            return false;

        world = abdominalTissue.transform.TransformPoint(vertices[index]);
        return true;
    }

    [ContextMenu("Aguja: reiniciar contactos")]
    public void ResetContacts()
    {
        for (int i = 0; i < positions.Length; i++)
        {
            completed[i] = false;
            contacted[i, 0] = false;
            contacted[i, 1] = false;
        }
        armed = true;
        // Para reabrir la herida, retirar los puntos del componente de tejido.
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || abdominalTissue == null)
            return;

        float[] slots = { 0.2f, 0.4f, 0.6f, 0.8f };
        for (int i = 0; i < slots.Length; i++)
        {
            for (int side = 0; side < 2; side++)
            {
                Vector3 target;
                if (!TryGetEdgePosition(slots[i], side, out target))
                    continue;

                bool done = Application.isPlaying && completed[i];
                bool touched = Application.isPlaying && contacted[i, side];
                Gizmos.color = done ? Color.green : (touched ? Color.yellow : Color.cyan);
                Gizmos.DrawWireSphere(target, contactDistance);
            }
        }
    }
}
