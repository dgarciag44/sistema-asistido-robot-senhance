using UnityEngine;

// Representacion visual de cuatro suturas independientes.
// Lee los puntos ya registrados por AbdominalTissueMesh;
// no modifica el cierre ni la deteccion de la aguja.
[DefaultExecutionOrder(1000)]
public class SurgicalSutureThread : MonoBehaviour
{
    [Header("Referencia")]
    public AbdominalTissueMesh abdominalTissue;

    [Header("Aspecto del hilo")]
    [Min(0.0001f)] public float threadWidth = 0.002f;
    [Min(0f)] public float surfaceOffset = 0.003f;
    [Min(0f)] public float arcHeight = 0.004f;
    public Color threadColor = new Color(0.06f, 0.08f, 0.12f, 1f);

    private readonly float[] positions = { 0.2f, 0.4f, 0.6f, 0.8f };
    private readonly LineRenderer[] threads = new LineRenderer[4];
    private Material threadMaterial;

    private void Start()
    {
        if (abdominalTissue == null)
        {
            Debug.LogWarning("SurgicalSutureThread: asigna AbdominalMesh.", this);
            enabled = false;
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogError("SurgicalSutureThread: no se encontro un shader compatible.", this);
            enabled = false;
            return;
        }

        threadMaterial = new Material(shader);
        threadMaterial.color = threadColor;

        for (int i = 0; i < threads.Length; i++)
        {
            GameObject obj = new GameObject("Hilo_Sutura_" + (i + 1));
            obj.transform.SetParent(transform, false);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 3;
            line.startWidth = threadWidth;
            line.endWidth = threadWidth;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.material = threadMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            threads[i] = line;
        }
    }

    private void LateUpdate()
    {
        if (abdominalTissue == null) return;

        MeshFilter filter = abdominalTissue.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return;

        Vector3[] vertices = filter.sharedMesh.vertices;
        int subdivisions = abdominalTissue.subdivisions;
        if (subdivisions < 1) return;
        int count = subdivisions + 1;
        int perSide = count * count;
        if (vertices.Length < 2 * perSide) return;

        Vector3 normal = abdominalTissue.transform.up.normalized;

        for (int i = 0; i < positions.Length; i++)
        {
            LineRenderer line = threads[i];
            if (line == null) continue;

            bool completed = false;
            if (abdominalTissue.suturePoints != null)
            {
                foreach (var point in abdominalTissue.suturePoints)
                {
                    if (point != null && point.closure > 0f &&
                        Mathf.Abs(point.position - positions[i]) < 0.015f)
                    {
                        completed = true;
                        break;
                    }
                }
            }

            line.enabled = completed;
            if (!completed) continue;

            float row = positions[i] * subdivisions;
            int lower = Mathf.Clamp(Mathf.FloorToInt(row), 0, subdivisions);
            int upper = Mathf.Clamp(lower + 1, 0, subdivisions);
            float t = row - lower;

            Vector3 left = Vector3.Lerp(
                vertices[lower * count + subdivisions],
                vertices[upper * count + subdivisions], t);
            Vector3 right = Vector3.Lerp(
                vertices[perSide + lower * count],
                vertices[perSide + upper * count], t);

            left = abdominalTissue.transform.TransformPoint(left) + normal * surfaceOffset;
            right = abdominalTissue.transform.TransformPoint(right) + normal * surfaceOffset;
            Vector3 middle = (left + right) * 0.5f + normal * arcHeight;

            line.SetPosition(0, left);
            line.SetPosition(1, middle);
            line.SetPosition(2, right);
        }
    }

    private void OnDestroy()
    {
        if (threadMaterial != null) Destroy(threadMaterial);
    }
}
