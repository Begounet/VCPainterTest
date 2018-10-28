using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using System;
using Unity.Collections;
using System.Linq;
using Unity.Collections.LowLevel.Unsafe;

public class VCPaintJobHandle
{
    public struct Args
    {
        public Color innerColor;
        public Color outerColor;
        public float innerRadius;
        public float outerRadius;

        public Vector3 brushPosition;
        public MeshFilter meshFilter;
    }
    
    unsafe struct VCPaintJob : IJob
    {
        [ReadOnly] public Matrix4x4 meshTransformMatrix;
        [ReadOnly] public Vector3 brushPosition;

        [ReadOnly] public Color innerColor;
        [ReadOnly] public Color outerColor;

        [ReadOnly] public float innerRadius;
        [ReadOnly] public float outerRadius;

        [ReadOnly] [NativeDisableUnsafePtrRestriction] void* vertices;
        [ReadOnly] public int numVertices;

        //[WriteOnly] public NativeArray<float> colors;
        [WriteOnly] [NativeDisableUnsafePtrRestriction] void* colors;

        public void Initialize(Mesh mesh, ref Color[] meshColors)
        {
            vertices = UnsafeUtility.AddressOf(ref mesh.vertices[0]);
            numVertices = mesh.vertexCount;

            colors = UnsafeUtility.AddressOf(ref meshColors[0]);
            UnsafeUtility.WriteArrayElementWithStride(colors, 0, sizeof(float), 0.88f);
            UnsafeUtility.WriteArrayElementWithStride(colors, 1, sizeof(float), 0.77f);
            UnsafeUtility.WriteArrayElementWithStride(colors, 4, sizeof(float), 0.55f);
        }

        public void Execute()
        {
            for (int index = 0; index < numVertices; ++index)
            {
                Vector3 vertex = UnsafeUtility.ReadArrayElement<Vector3>(vertices, index);
                float verticeDistance = Vector3.Distance(brushPosition, meshTransformMatrix.MultiplyPoint(vertex));

                float colorWeight = (verticeDistance - innerRadius) / (outerRadius - innerRadius);

                Color finalColor = Color.Lerp(innerColor, outerColor, colorWeight);

                int colorIndex = index * 4;
                UnsafeUtility.WriteArrayElementWithStride(colors, colorIndex, sizeof(float), finalColor.r);
                UnsafeUtility.WriteArrayElementWithStride(colors, colorIndex + 1, sizeof(float), finalColor.g);
                UnsafeUtility.WriteArrayElementWithStride(colors, colorIndex + 2, sizeof(float), finalColor.b);
            }
        }
    }

    private VCPaintJob paintJob;
    private JobHandle paintJobHandle;

    private MeshFilter meshFilter;
    private Color[] colors;

    private bool isRunning = false;
    private bool isCompleted = false;

    public event Action<VCPaintJobHandle> OnJobCompleted;

    public void Start(VCPaintJobHandle.Args args)
    {
        paintJob = new VCPaintJob()
        {
            innerColor = args.innerColor,
            outerColor = args.outerColor,

            innerRadius = args.innerRadius,
            outerRadius = args.outerRadius,

            brushPosition = args.brushPosition,
            meshTransformMatrix = args.meshFilter.transform.localToWorldMatrix,
        };

        meshFilter = args.meshFilter;
        Mesh mesh = args.meshFilter.mesh;

        if (mesh.colors.Length != mesh.vertexCount)
        {
            mesh.colors = new Color[mesh.vertexCount];
        }

        colors = mesh.colors;

        paintJob.Initialize(mesh, ref colors);

        paintJobHandle = paintJob.Schedule();
        isRunning = true;
    }

    public void Update()
    {
        if (paintJobHandle.IsCompleted && isRunning)
        {
            isRunning = false;
            isCompleted = true;
            paintJobHandle.Complete();
            meshFilter.mesh.colors = colors;
            ReleaseResources();
            
            if (OnJobCompleted != null)
            {
                OnJobCompleted(this);
            }
        }
    }

    public void CompleteJob()
    {
        if (isRunning)
        {
            paintJobHandle.Complete();

            ReleaseResources();
        }
    }

    public bool IsCompleted()
    {
        return (isCompleted);
    }

    public void ReleaseResources()
    {
       // paintJob.colors.Dispose();
    }
}
