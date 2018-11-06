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
    public class Args
    {
        public Color innerColor;
        public Color outerColor;
        public float innerRadius;
        public float outerRadius;

        public Vector3 brushPosition;
        public MeshFilter meshFilter;
    }
    
    //[Unity.Burst.BurstCompile]
    unsafe struct VCPaintJob : IJob
    {
        [ReadOnly] public Matrix4x4 meshTransformMatrix;
        [ReadOnly] public Vector3 brushPosition;

        [ReadOnly] public Color innerColor;
        [ReadOnly] public Color outerColor;

        [ReadOnly] public float innerRadiusSqr;
        [ReadOnly] public float outerRadiusSqr;

        [ReadOnly] public int indexStart;
        [ReadOnly] public int indexEnd;

        [ReadOnly] [NativeDisableUnsafePtrRestriction] void* vertices;
        [ReadOnly] public int numVertices;

        [WriteOnly] [NativeDisableUnsafePtrRestriction] void* colors;

        public void Initialize(Mesh mesh, ref Color[] meshColors)
        { 
            IntPtr vertexBufferPtr = VCVertexBufferStaticContainer.GetOrCacheVertexBufferPtrFromMesh(mesh);
            vertices = vertexBufferPtr.ToPointer();
            numVertices = mesh.vertexCount;

            colors = UnsafeUtility.AddressOf(ref meshColors[0]);
        }

        public void Execute()
        {
            int numComponentsInColor = sizeof(Color) / sizeof(float);

            for (int index = indexStart; index < numVertices && index < indexEnd; ++index)
            {
                Vector3 vertex = UnsafeUtility.ReadArrayElement<Vector3>(vertices, index);
                Vector3 brushToVertex = meshTransformMatrix.MultiplyPoint(vertex) - brushPosition;
                float verticeDistance = brushToVertex.sqrMagnitude;

                float colorWeight = (verticeDistance - innerRadiusSqr) / (outerRadiusSqr - innerRadiusSqr);

                Color finalColor = Color.Lerp(innerColor, outerColor, colorWeight);

                int colorIndex = index * numComponentsInColor;
                UnsafeUtility.WriteArrayElementWithStride(colors, colorIndex, sizeof(float), finalColor.r);
                UnsafeUtility.WriteArrayElementWithStride(colors, colorIndex + 1, sizeof(float), finalColor.g);
                UnsafeUtility.WriteArrayElementWithStride(colors, colorIndex + 2, sizeof(float), finalColor.b);
            }
        }
    }

    private List<JobHandle> paintJobHandles;

    private MeshFilter meshFilter;
    private Color[] colors;

    private bool isRunning = false;
    private bool isCompleted = false;

    public event Action<VCPaintJobHandle> OnJobCompleted;

    public void Start(VCPaintJobHandle.Args args)
    {
        meshFilter = args.meshFilter;
        Mesh mesh = args.meshFilter.mesh;
        if (mesh.colors.Length != mesh.vertexCount)
        {
            mesh.colors = new Color[mesh.vertexCount];
        }
        colors = mesh.colors;

        ScheduleJobs(args);
        isRunning = true;
    }

    void ScheduleJobs(VCPaintJobHandle.Args args)
    {
        VCPaintJob paintJob = new VCPaintJob()
        {
            innerColor = args.innerColor,
            outerColor = args.outerColor,

            innerRadiusSqr = args.innerRadius * args.innerRadius,
            outerRadiusSqr = args.outerRadius * args.outerRadius,

            brushPosition = args.brushPosition,
            meshTransformMatrix = args.meshFilter.transform.localToWorldMatrix,
        };

        Mesh mesh = args.meshFilter.mesh;
        paintJob.Initialize(mesh, ref colors);

        paintJobHandles = new List<JobHandle>();

        int numCores = 1; //SystemInfo.processorCount;
        int numVerticesPerCore = mesh.vertexCount / numCores;
        int remainingNumVerticesPerCore = mesh.vertexCount % numCores;
        for (int coreId = 0; coreId < numCores; ++coreId)
        {
            paintJob.indexStart = coreId * numVerticesPerCore;
            paintJob.indexEnd = coreId * numVerticesPerCore + numVerticesPerCore;
            if (coreId + 1 >= numCores)
            {
                paintJob.indexEnd += remainingNumVerticesPerCore;
            }

            paintJobHandles.Add(paintJob.Schedule());
        }
    }

    public void Update()
    {
        if (isRunning && paintJobHandles.TrueForAll(paintJobHandle => paintJobHandle.IsCompleted))
        {
            paintJobHandles.ForEach(paintJobHandle => paintJobHandle.Complete());

            isRunning = false;
            isCompleted = true;
            meshFilter.mesh.colors = colors;

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
            paintJobHandles.ForEach(paintJobHandle => paintJobHandle.Complete());
        }
    }

    public bool IsCompleted()
    {
        return (isCompleted);
    }
}
