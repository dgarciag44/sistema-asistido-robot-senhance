
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class AbdominalTissueMesh : MonoBehaviour
{
    [Header("Dimensiones locales")]
    public float width = 10f;
    public float length = 10f;

    [Header("Resolucion")]
    [Range(4, 40)]
    public int subdivisions = 30;

    [Header("Incision abdominal")]
    [Range(0.01f, 0.8f)]
    public float incisionWidthRatio = 0.24f;

    [Range(0.1f, 1f)]
    public float incisionLengthRatio = 0.85f;

    [Header("Adaptacion al paciente")]
    public MeshCollider patientCollider;

    [Min(0.01f)]
    public float projectionDistance = 0.15f;

    [Min(0f)]
    public float skinOffset = 0.002f;

    [Header("Propiedades del tejido")]
    [Min(0.01f)] public float deformationRadius = 2f;
    [Min(0f)] public float deformationStrength = 0.5f;
    [Min(0.01f)] public float recoverySpeed = 0.5f;

    [Header("Elevacion suave y anclaje")]
    [Min(1f)] public float liftSpreadMultiplier = 2.5f;
    [Range(0f, 0.8f)] public float liftPlateau = 0.35f;
    [Range(0.05f, 0.9f)] public float outerAnchorTransition = 0.65f;
    [Range(0.05f, 0.5f)] public float endAnchorTransition = 0.25f;

    [Header("Manipulacion quirurgica")]
    [Min(0f)] public float maxDisplacement = 0.04f;
    public bool enableTest = false;

    [Header("Prueba de sutura (prototipo)")]
    [Range(0f, 1f)] public float sutureClosure = 0f;
    [Range(0.02f, 0.5f)] public float sutureZoneLength = 0.18f;
    [Range(0.1f, 1f)] public float sutureLateralFalloff = 0.7f;

    [Header("Sutura por puntos independientes")]
    [Range(0.02f, 0.25f)] public float individualSutureZoneLength = 0.11f;
    [Range(0f, 1f)] public float newSuturePosition = 0.5f;
    [Range(0f, 1f)] public float newSutureClosure = 1f;

    [System.Serializable]
    public class SuturePoint
    {
        [Range(0f, 1f)] public float position;
        [Range(0f, 1f)] public float closure = 1f;
    }

    public List<SuturePoint> suturePoints = new List<SuturePoint>();

    private Mesh tissueMesh;
    private MeshCollider tissueCollider;

    private Vector3[] originalVertices;
    private Vector3[] currentVertices;
    private bool[] anchoredVertices;

    private class TissueGrab
    {
        public Transform target;
        public Vector3 localPoint;
        public Vector3 initialTargetPosition;
    }

    private readonly Dictionary<Transform, TissueGrab> activeGrabs =
        new Dictionary<Transform, TissueGrab>();

    private readonly List<Transform> invalidTargets =
        new List<Transform>();

    private void Awake()
    {
        tissueCollider = GetComponent<MeshCollider>();

        GenerateMesh();
    }

    private void Start()
    {
        if (enableTest)
            ApplyDeformation(transform.position, deformationStrength);
    }

    private void LateUpdate()
    {
        if (tissueMesh == null)
            return;

        invalidTargets.Clear();

        foreach (var pair in activeGrabs)
        {
            if (pair.Key == null)
                invalidTargets.Add(pair.Key);
        }

        foreach (Transform target in invalidTargets)
            activeGrabs.Remove(target);

        if (activeGrabs.Count > 0)
            UpdateGrabDeformation();
        else
            RecoverTissue();
    }

    private void GenerateMesh()
    {
        int rows = subdivisions + 1;
        int cols = subdivisions + 1;

        int verticesPerSide = rows * cols;
        int totalVertices = verticesPerSide * 2;

        Vector3[] vertices = new Vector3[totalVertices];
        Vector2[] uv = new Vector2[totalVertices];

        anchoredVertices = new bool[totalVertices];

        List<int> triangles = new List<int>();

        float halfWidth = width * 0.5f;
        float halfLength = length * 0.5f;

        float gapHalfWidth =
            width * incisionWidthRatio * 0.5f;

        float incisionHalfLength =
            length * incisionLengthRatio * 0.5f;

        for (int side = 0; side < 2; side++)
        {
            int offset = side * verticesPerSide;

            for (int z = 0; z < rows; z++)
            {
                float v = (float)z / subdivisions;

                float localZ = Mathf.Lerp(
                    -halfLength,
                    halfLength,
                    v
                );

                float longitudinalFactor = Mathf.Clamp01(
                    1f - Mathf.Abs(localZ) /
                    Mathf.Max(0.0001f, incisionHalfLength)
                );

                longitudinalFactor = Mathf.SmoothStep(
                    0f, 1f, longitudinalFactor
                );

                float gap =
                    gapHalfWidth * longitudinalFactor;

                for (int x = 0; x < cols; x++)
                {
                    float u = (float)x / subdivisions;

                    float localX = side == 0
                        ? Mathf.Lerp(-halfWidth, -gap, u)
                        : Mathf.Lerp(gap, halfWidth, u);

                    int index =
                        offset + z * cols + x;

                    Vector3 localVertex = new Vector3(
                        localX,
                        0f,
                        localZ
                    );

                    vertices[index] =
                        ProjectVertex(localVertex);

                    uv[index] = new Vector2(
                        localX / width + 0.5f,
                        v
                    );

                    bool outerSide = side == 0
                        ? x == 0
                        : x == subdivisions;

                    bool endEdge =
                        z == 0 || z == subdivisions;

                    anchoredVertices[index] =
                        outerSide || endEdge;
                }
            }

            for (int z = 0; z < subdivisions; z++)
            {
                for (int x = 0; x < subdivisions; x++)
                {
                    int i =
                        offset + z * cols + x;

                    triangles.Add(i);
                    triangles.Add(i + cols);
                    triangles.Add(i + 1);

                    triangles.Add(i + 1);
                    triangles.Add(i + cols);
                    triangles.Add(i + cols + 1);
                }
            }
        }

        tissueMesh = new Mesh();
        tissueMesh.name = "Abdominal_Incision_Curved_Mesh";
        tissueMesh.MarkDynamic();

        tissueMesh.vertices = vertices;
        tissueMesh.uv = uv;
        tissueMesh.triangles = triangles.ToArray();

        tissueMesh.RecalculateNormals();
        tissueMesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = tissueMesh;

        originalVertices =
            (Vector3[])vertices.Clone();

        currentVertices =
            (Vector3[])vertices.Clone();

        UpdateCollider();
    }

    private Vector3 ProjectVertex(Vector3 localVertex)
    {
        if (patientCollider == null)
            return localVertex;

        Vector3 worldPoint =
            transform.TransformPoint(localVertex);

        Vector3 direction =
            -transform.up.normalized;

        float distance =
            Mathf.Max(0.01f, projectionDistance);

        Vector3 rayOrigin =
            worldPoint - direction * distance;

        Ray ray = new Ray(
            rayOrigin,
            direction
        );

        if (patientCollider.Raycast(
            ray,
            out RaycastHit hit,
            distance * 2f
        ))
        {
            Vector3 adjustedWorldPoint =
                hit.point +
                hit.normal * skinOffset;

            return transform.InverseTransformPoint(
                adjustedWorldPoint
            );
        }

        return localVertex;
    }

    public bool TryGrab(Vector3 worldPoint, Transform target)
    {
        if (target == null || tissueMesh == null)
            return false;

        if (activeGrabs.ContainsKey(target))
            return false;

        TissueGrab grab = new TissueGrab
        {
            target = target,
            localPoint =
                transform.InverseTransformPoint(worldPoint),
            initialTargetPosition = target.position
        };

        activeGrabs.Add(target, grab);

        Debug.Log(
            "TEJIDO ABDOMINAL SUJETADO - AGARRES: " +
            activeGrabs.Count
        );

        return true;
    }

    // La zona central acompana la pinza, mientras el perimetro queda fijo.
    private float GetAnchorWeight(int index)
    {
        int count = subdivisions + 1;
        int verticesPerSide = count * count;
        int localIndex = index % verticesPerSide;
        int x = localIndex % count;
        int z = localIndex / count;
        int side = index / verticesPerSide;

        float u = (float)x / subdivisions;
        float v = (float)z / subdivisions;
        float distanceFromOuter = side == 0 ? u : 1f - u;
        float distanceFromEnd = Mathf.Min(v, 1f - v);

        float sideWeight = Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01(distanceFromOuter / Mathf.Max(0.01f, outerAnchorTransition)));
        float endWeight = Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01(distanceFromEnd / Mathf.Max(0.01f, endAnchorTransition)));

        return sideWeight * endWeight;
    }

    private float GetGrabInfluence(float distance, float radius)
    {
        float normalized = Mathf.Clamp01(distance / radius);
        float plateau = Mathf.Clamp(liftPlateau, 0f, 0.8f);
        if (normalized <= plateau)
            return 1f;

        float transition = Mathf.Clamp01((normalized - plateau) / (1f - plateau));
        return 1f - Mathf.SmoothStep(0f, 1f, transition);
    }

    private void UpdateGrabDeformation()
    {
        float radius = Mathf.Max(0.01f, deformationRadius * liftSpreadMultiplier);
        float maxMove = Mathf.Max(0f, maxDisplacement);

        for (int i = 0; i < currentVertices.Length; i++)
        {
            if (anchoredVertices[i])
            {
                currentVertices[i] = originalVertices[i];
                continue;
            }

            Vector3 original = GetRestPosition(i);
            Vector3 totalDisplacement = Vector3.zero;
            float totalInfluence = 0f;

            foreach (TissueGrab grab in activeGrabs.Values)
            {
                float distance = Vector2.Distance(
                    new Vector2(original.x, original.z),
                    new Vector2(grab.localPoint.x, grab.localPoint.z));

                if (distance >= radius)
                    continue;

                float influence = GetGrabInfluence(distance, radius);
                Vector3 worldMove = Vector3.ClampMagnitude(
                    grab.target.position - grab.initialTargetPosition, maxMove);
                Vector3 localMove = transform.InverseTransformVector(worldMove);

                totalDisplacement += localMove * influence;
                totalInfluence += influence;
            }

            if (totalInfluence > 1f)
                totalDisplacement /= totalInfluence;

            float anchorWeight = GetAnchorWeight(i);
            currentVertices[i] = original + totalDisplacement * anchorWeight;
        }

        UpdateMesh();
    }

    public void ReleaseTissue(Transform target)
    {
        if (target == null)
            return;

        if (!activeGrabs.Remove(target))
            return;

        Debug.Log(
            "TEJIDO ABDOMINAL LIBERADO - AGARRES: " +
            activeGrabs.Count
        );
    }

    private void RecoverTissue()
    {
        bool changed = false;

        for (int i = 0; i < currentVertices.Length; i++)
        {
            if (anchoredVertices[i])
                continue;

            Vector3 recovered = Vector3.MoveTowards(
                currentVertices[i],
                GetRestPosition(i),
                recoverySpeed * Time.deltaTime
            );

            if ((recovered - currentVertices[i]).sqrMagnitude >
                0.00000001f)
            {
                currentVertices[i] = recovered;
                changed = true;
            }
        }

        if (changed)
            UpdateMesh();
    }

    public void ApplyDeformation(
        Vector3 worldPoint,
        float force
    )
    {
        if (tissueMesh == null)
            return;

        Vector3 localPoint =
            transform.InverseTransformPoint(worldPoint);

        float radius =
            Mathf.Max(0.01f, deformationRadius);

        for (int i = 0; i < currentVertices.Length; i++)
        {
            if (anchoredVertices[i])
                continue;

            Vector3 original = originalVertices[i];

            float distance = Vector2.Distance(
                new Vector2(original.x, original.z),
                new Vector2(localPoint.x, localPoint.z)
            );

            float influence =
                Mathf.Clamp01(1f - distance / radius);

            influence = influence * influence *
                        (3f - 2f * influence);

            currentVertices[i].y +=
                force * influence;
        }

        UpdateMesh();
    }

    // Prototipo: un punto de cierre en el centro de la incision.
    // No representa todavia una aguja ni un hilo fisico.
    [ContextMenu("Prueba: cerrar punto central")]
    public void TestCloseCentralPoint()
    {
        SetSutureClosure(0.95f);
    }

    [ContextMenu("Prueba: reabrir punto central")]
    public void TestReopenCentralPoint()
    {
        SetSutureClosure(0f);
    }

    public void SetSutureClosure(float value)
    {
        sutureClosure = Mathf.Clamp01(value);
        // LateUpdate acerca los vertices al nuevo reposo progresivamente.
    }

    // Pruebas desde el menu contextual del componente (en modo Play).
    [ContextMenu("Sutura: agregar punto configurado")]
    public void AddConfiguredSuturePoint()
    {
        AddSuturePoint(newSuturePosition, newSutureClosure);
    }

    [ContextMenu("Sutura: agregar punto superior")]
    public void AddUpperSuturePoint() { AddSuturePoint(0.2f); }

    [ContextMenu("Sutura: agregar punto central")]
    public void AddMiddleSuturePoint() { AddSuturePoint(0.4f); }

    [ContextMenu("Sutura: agregar punto inferior")]
    public void AddLowerSuturePoint() { AddSuturePoint(0.6f); }

    [ContextMenu("Sutura: retirar todos los puntos")]
    public void ClearSuturePoints()
    {
        suturePoints.Clear();
    }

    // position: 0 = extremo inferior del eje local Z; 1 = extremo superior.
    // El orden anatomico visible depende de la orientacion del objeto.
    public void AddSuturePoint(float position, float closure = 1f)
    {
        position = Mathf.Clamp01(position);
        closure = Mathf.Clamp01(closure);
        if (suturePoints == null)
            suturePoints = new List<SuturePoint>();

        // Actualiza el punto existente en lugar de duplicarlo.
        foreach (SuturePoint point in suturePoints)
        {
            if (point != null && Mathf.Abs(point.position - position) < 0.01f)
            {
                point.closure = closure;
                return;
            }
        }
        suturePoints.Add(new SuturePoint { position = position, closure = closure });
        Debug.Log("PUNTO DE SUTURA AGREGADO - TOTAL: " + suturePoints.Count);
    }

    private float GetSutureInfluence(float v)
    {
        // Se conserva el cierre central original como prueba independiente.
        float centerDistance = Mathf.Abs(v - 0.5f);
        float centerWeight = 1f - Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01(centerDistance / Mathf.Max(0.01f, sutureZoneLength)));
        float closure = Mathf.Clamp01(sutureClosure) * centerWeight;

        if (suturePoints == null)
            return closure;

        foreach (SuturePoint point in suturePoints)
        {
            if (point == null || point.closure <= 0f)
                continue;
            float distance = Mathf.Abs(v - Mathf.Clamp01(point.position));
            float weight = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(distance / Mathf.Max(0.01f, individualSutureZoneLength)));
            // Max evita sobrecerrar donde coinciden dos zonas de sutura.
            closure = Mathf.Max(closure, Mathf.Clamp01(point.closure) * weight);
        }
        return Mathf.Clamp01(closure);
    }

    private Vector3 GetRestPosition(int index)
    {
        Vector3 rest = originalVertices[index];
        if (anchoredVertices[index])
            return rest;

        int count = subdivisions + 1;
        int perSide = count * count;
        int side = index / perSide;
        int local = index % perSide;
        int x = local % count;
        int z = local / count;

        float u = (float)x / subdivisions;
        float v = (float)z / subdivisions;
        float distanceFromInner = side == 0 ? 1f - u : u;
        float lateral = 1f - Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01(distanceFromInner / Mathf.Max(0.01f, sutureLateralFalloff)));

        float longitudinal = GetSutureInfluence(v);

        // El desplazamiento de cada borde se calcula con la abertura
        // original en esta fila, sin cerrar las zonas no suturadas.
        int innerX = side == 0 ? subdivisions : 0;
        int innerIndex = side * perSide + z * count + innerX;
        float halfGap = Mathf.Abs(originalVertices[innerIndex].x);
        float direction = side == 0 ? 1f : -1f;
        rest.x += direction * halfGap * lateral * longitudinal;
        return rest;
    }

    private void UpdateMesh()
    {
        tissueMesh.vertices = currentVertices;
        tissueMesh.RecalculateNormals();
        tissueMesh.RecalculateBounds();

        UpdateCollider();
    }

    private void UpdateCollider()
    {
        if (tissueCollider == null || tissueMesh == null)
            return;

        tissueCollider.sharedMesh = null;
        tissueCollider.sharedMesh = tissueMesh;
    }

    private void OnDestroy()
    {
        activeGrabs.Clear();

        if (tissueCollider != null)
            tissueCollider.sharedMesh = null;

        if (tissueMesh != null)
            Destroy(tissueMesh);
    }
}
