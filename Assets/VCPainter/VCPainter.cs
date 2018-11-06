using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class VCPainter : MonoBehaviour
{
    public enum Mode
    {
        SingleCPU,
        JobSystem
    }

    public Color paintColor = Color.red;
    public Color outRadiusColor = Color.blue;
    public LayerMask layerMask;
    public Mode mode;

    public float outerRadius = 1.0f;
    public float innerRadius = 0.9f;

    [Header("Dev")]
    public bool updatePaint = false;
    public bool updateEachFrame = false;

    private List<VCPaintJobHandle> paintJobHandles;
    private bool isPaintingDirty = false;

    void Awake()
    {
        paintJobHandles = new List<VCPaintJobHandle>();
    }

    void Start ()
    {
        Paint();
    }

    void OnDestroy()
    {
        paintJobHandles.ForEach(paintJobHandle => paintJobHandle.CompleteJob());
    }

    void Update()
    {
        if (mode == Mode.JobSystem)
        {
            paintJobHandles.ForEach(paintJobHandle => paintJobHandle.Update());
            paintJobHandles.RemoveAll(paintJobHandle => paintJobHandle.IsCompleted());

            if (paintJobHandles.Count == 0 && isPaintingDirty)
            {
                updatePaint = true;
                Debug.Log("All painting completed. Start new one...");
            }
        }

        if (updatePaint || updateEachFrame)
        {
            updatePaint = false;
            Paint();
        }
    }

    private void OnValidate()
    {
        outerRadius = Mathf.Max(outerRadius, 0.0f);
        innerRadius = Mathf.Clamp(innerRadius, 0.0f, outerRadius);
    }

    public void Paint()
    {
        if (mode == Mode.JobSystem && paintJobHandles.Count > 0)
        {
            isPaintingDirty = true;
        }
        else
        {
            //Debug.Log("Paint...");

            Collider[] colliders = Physics.OverlapSphere(this.transform.position, outerRadius, layerMask.value);

            for (int i = 0; i < colliders.Length; ++i)
            {
                MeshFilter meshFilter = colliders[i].GetComponent<MeshFilter>();
                if (meshFilter)
                {
                    PaintMesh(meshFilter);
                }
            }

            isPaintingDirty = false;
        }
    }

    public void PaintMesh(MeshFilter meshFilter)
    {
        Mesh mesh = meshFilter.mesh;
        if (mesh == null || Mathf.Approximately(outerRadius, 0.0f))
            return;

        if (mode == Mode.SingleCPU)
        {
            System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();

            Transform meshTransform = meshFilter.transform;

            Vector3[] vertices = mesh.vertices;
            Color[] colors = new Color[vertices.Length];
            for (int i = 0; i < vertices.Length; ++i)
            {
                float verticeDistance = Vector3.Distance(this.transform.position, meshTransform.TransformPoint(vertices[i]));
                float colorWeight = (verticeDistance - innerRadius) / (outerRadius - innerRadius);

                colors[i] = Color.Lerp(paintColor, outRadiusColor, colorWeight);
            }
            mesh.colors = colors;

            stopWatch.Stop();
            //Debug.LogFormat("Mesh {0} done in {1} ms", meshFilter.gameObject.name, stopWatch.ElapsedMilliseconds);
        }
        else // if (mode == Mode.JobSystem)
        {
            //Debug.LogFormat("Queue new job for mesh {0}...", meshFilter.gameObject.name);

            VCPaintJobHandle.Args args = new VCPaintJobHandle.Args();
            args.innerColor = paintColor;
            args.outerColor = outRadiusColor;
            args.meshFilter = meshFilter;
            args.innerRadius = innerRadius;
            args.outerRadius = outerRadius;
            args.brushPosition = this.transform.position;

            VCPaintJobHandle newJobHandle = new VCPaintJobHandle();
            newJobHandle.OnJobCompleted += OnJobCompleted;
            paintJobHandles.Add(newJobHandle);
            newJobHandle.Start(args);
        }
    }

    private void OnJobCompleted(VCPaintJobHandle jobHandle)
    {
        //Debug.Log("Paint job completed!");
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(this.transform.position, outerRadius);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(this.transform.position, innerRadius);
    }
}
