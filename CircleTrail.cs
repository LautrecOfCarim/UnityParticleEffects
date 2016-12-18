// Copyright (c) 2016 Alexander Rrdkov
// This file is distributed under GPL v3. See LICENSE.md for details.

using UnityEngine;

public class CircleTrail : MonoBehaviour
{
    [SerializeField]
    Material material;

    [SerializeField]
    float lifeTime = 1.00f;

    [SerializeField]
    int subdivisions = 4;

    [SerializeField]
    Transform baseTransform;

    [SerializeField]
    Transform tipTransform;

    GameObject trailObject;
    Mesh trailMesh;
    LineSegment[] lineSeg;
    LineSegment[] smoothSeg;
    int nextIdx = 0;
    int lastIdx = 0;
    int smoothLength = 0;

    public struct LineSegment
    {
        public Vector3 basePosition;
        public Vector3 tipPosition;
        public float timeCreated;
    }

    void Start()
    {
        var maximumNumber = (int)(60f / lifeTime) + 1;
        lineSeg = new LineSegment[maximumNumber];
        smoothSeg = new LineSegment[maximumNumber * (1 + subdivisions)];
        nextIdx = 0;
        lastIdx = 0;

        trailObject = new GameObject("Trail");
        trailObject.transform.parent = null;
        trailObject.transform.position = Vector3.zero;
        trailObject.transform.rotation = Quaternion.identity;
        trailObject.transform.localScale = Vector3.one;
        trailObject.AddComponent(typeof(MeshFilter));
        trailObject.AddComponent(typeof(MeshRenderer));
        trailObject.GetComponent<Renderer>().material = material;

        trailMesh = new Mesh();
        trailMesh.name = name + "TrailMesh";
        trailObject.GetComponent<MeshFilter>().mesh = trailMesh;
    }

    void OnDisable()
    {
        Destroy(trailObject);
    }

    void Update()
    {
        // Early out if there is no camera
        if (!Camera.main)
            return;

        LineSegment newSeg = new LineSegment
        {
            basePosition = baseTransform.position,
            tipPosition = tipTransform.position,
            timeCreated = Time.time,
        };
        
        // Add new points
        lineSeg[nextIdx] = newSeg;
        var newNextdx = (nextIdx + 1) % lineSeg.Length;
        if (newNextdx != lastIdx) nextIdx = newNextdx;

        // Remove old points
        while (Time.time - lineSeg[lastIdx].timeCreated  > lifeTime)
        {
            lastIdx = (lastIdx + 1) % lineSeg.Length;
        }

        Smooth();

        RenderTrail(smoothSeg, smoothLength);
    }

    protected void Smooth()
    {
        smoothLength = 0;

        var i = nextIdx;
        i = (i == 0) ? lineSeg.Length - 1 : i - 1;
        var iNext = (i == lastIdx) ? lastIdx : ((i == 0) ? lineSeg.Length - 1 : i - 1);
        var iNext2 = (iNext == lastIdx) ? lastIdx : ((iNext == 0) ? lineSeg.Length - 1 : iNext - 1);

        smoothSeg[smoothLength++] = lineSeg[i];
        if (i == lastIdx)
            return;

        var farPoint = lineSeg[i].basePosition + (lineSeg[i].basePosition - lineSeg[i].tipPosition) * 0.3f;

        var centerB1 = Circumcenter(ref lineSeg[i].basePosition, ref lineSeg[iNext].basePosition, ref lineSeg[iNext2].basePosition);
        centerB1 = centerB1 * 0.1f + farPoint * 0.9f;
        var radiusB1 = (centerB1 - lineSeg[i].basePosition).magnitude;
        var centerB2 = centerB1;
        var radiusB2 = (centerB2 - lineSeg[iNext].basePosition).magnitude;

        var centerT1 = Circumcenter(ref lineSeg[i].tipPosition, ref lineSeg[iNext].tipPosition, ref lineSeg[iNext2].tipPosition);
        centerT1 = centerT1 * 0.1f + farPoint * 0.9f;
        var radiusT1 = (centerT1 - lineSeg[i].tipPosition).magnitude;
        var centerT2 = centerT1;
        var radiusT2 = (centerT2 - lineSeg[iNext].tipPosition).magnitude;

        var subD = (1f / (float)(subdivisions + 1));

        while (true)
        {
            for (int j = 1; j <= subdivisions; j++)
            {
                var tipPos = Vector3.Lerp(lineSeg[i].tipPosition, lineSeg[iNext].tipPosition, subD * j);
                var basePos = Vector3.Lerp(lineSeg[i].basePosition, lineSeg[iNext].basePosition, subD * j);
                var time = lineSeg[i].timeCreated * (1f - subD * j) + lineSeg[iNext].timeCreated * subD * j;

                var baseRad = radiusB1 * (1f - subD * j) + radiusB2 * subD * j;
                var baseCen = Vector3.Lerp(centerB1, centerB2, subD * j);
                var baseDir = (basePos - baseCen).normalized;
                basePos = baseCen + baseDir * baseRad;

                var tipRad = radiusT1 * (1f - subD * j) + radiusT2 * subD * j;
                var tipCen = Vector3.Lerp(centerT1, centerT2, subD * j);
                var tipDir = (tipPos - tipCen).normalized;
                tipPos = tipCen + tipDir * tipRad;

                smoothSeg[smoothLength++] = new LineSegment { basePosition = basePos, tipPosition = tipPos, timeCreated = time };
            }

            smoothSeg[smoothLength++] = lineSeg[iNext];

            if (iNext == lastIdx)
                return;

            i = iNext;
            iNext = iNext2;
            iNext2 = (iNext2 == lastIdx) ? iNext2 : ((iNext2 == 0) ? lineSeg.Length - 1 : iNext2 - 1);

            farPoint = lineSeg[i].basePosition + (lineSeg[i].basePosition - lineSeg[i].tipPosition) * 0.3f;

            centerB1 = centerB2;
            radiusB1 = radiusB2;
            centerB2 = Circumcenter(ref lineSeg[i].basePosition, ref lineSeg[iNext].basePosition, ref lineSeg[iNext2].basePosition);
            centerB2 = centerB2 * 0.1f + farPoint * 0.9f;
            radiusB2 = (centerB2 - lineSeg[iNext].basePosition).magnitude;

            centerT1 = centerT2;
            radiusT1 = radiusT2;
            centerT2 = Circumcenter(ref lineSeg[i].tipPosition, ref lineSeg[iNext].tipPosition, ref lineSeg[iNext2].tipPosition);
            centerT2 = centerT2 * 0.1f + farPoint * 0.9f;
            radiusT2 = (centerT2 - lineSeg[iNext].tipPosition).magnitude;
        }
    }

    /// <summary>
    /// Finds the circumcenter coordinates for triangle ABC
    /// </summary>
    public static Vector3 Circumcenter(ref Vector3 A, ref Vector3 B, ref Vector3 C)
    {
        var a = A - C;
        var b = B - C;

        var crossAB = Vector3.Cross(a, b);
        var lenAB = crossAB.sqrMagnitude;
        if (lenAB <= Mathf.Epsilon)
            return C;

        return C + Vector3.Cross(a.sqrMagnitude * b - b.sqrMagnitude * a, crossAB) / (2 * lenAB);
    }

    /// <summary>
    /// Render the trail by building the mesh data from the segment list
    /// </summary>
    /// <param name="segments">Array of segments to render starting from element 0</param>
    /// <param name="count">Number of segments to render (not necessarily equal to the array's length)</param>
    protected void RenderTrail(LineSegment[] segments, int count)
    {
        // Early out if the segments are too few to draw
        if (count <= 1)
        {
            trailMesh.Clear();
            return;
        }

        Vector3[] vertices = new Vector3[count * 2];
        Vector2[] uv = new Vector2[count * 2];
        int[] triangles = new int[(count - 1) * 6];
        Color[] colors = new Color[count * 2];

        // Leading vertices
        vertices[0] = segments[0].basePosition;
        vertices[1] = segments[0].tipPosition;
        colors[0] = colors[1] = Color.white;
        uv[0] = new Vector2(0f, 0f);
        uv[1] = new Vector2(0f, 1f);

        int tri = 0;
        int vIdx = 0;
        for (int i = 1; i < count; i++)
        {
            float time = (Time.time - segments[i].timeCreated) / lifeTime;
            float factor = (float)i / count;

            vertices[i * 2] = segments[i].basePosition;
            vertices[(i * 2) + 1] = segments[i].tipPosition;
            colors[i * 2] = colors[(i * 2) + 1] = Color.Lerp(Color.white, Color.clear, time);
            uv[i * 2] = new Vector2(factor, 0f);
            uv[(i * 2) + 1] = new Vector2(factor, 1f);

            // Optimized triangles building
            triangles[tri++] = vIdx++; // +0
            triangles[tri++] = vIdx++; // +1
            triangles[tri++] = vIdx++; // +2
            triangles[tri++] = vIdx--; // +3
            triangles[tri++] = vIdx--; // +2
            triangles[tri++] = vIdx++; // +1 -> Next pair will start from +2
        }

        trailMesh.Clear();
        trailMesh.vertices = vertices;
        trailMesh.colors = colors;
        trailMesh.uv = uv;
        trailMesh.triangles = triangles;
    }
}
