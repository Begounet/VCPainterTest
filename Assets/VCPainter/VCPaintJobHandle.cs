using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using System;
using Unity.Collections;
using System.Linq;
using Unity.Transforms;
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
    
    [Unity.Burst.BurstCompile]
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

        [WriteOnly] public NativeArray<float> colors;

        public void Initialize(Mesh mesh)
        {
            vertices = UnsafeUtility.AddressOf(ref mesh.vertices[0]);
            numVertices = mesh.vertexCount;
            
            colors = new NativeArray<float>(numVertices * 3, Allocator.TempJob);
        }

        public void GetResult(Mesh mesh)
        {
            if (mesh.colors.Length != numVertices)
            {
                mesh.colors = new Color[numVertices];
            }

            Color[] meshColors = mesh.colors;
            for (int i = 0; i < numVertices; ++i)
            {
                meshColors[i] = new Color(colors[i * 3], colors[i * 3 + 1], colors[i * 3 + 2], 1.0f);
            }
            mesh.colors = meshColors;

            //void* managedColors = UnsafeUtility.AddressOf(ref mesh.colors[0]);
            //void* managedNativeArray = UnsafeUtility.AddressOf(ref colors);
            //UnsafeUtility.MemCpy(managedColors, managedNativeArray, mesh.colors.Length * UnsafeUtility.SizeOf(typeof(Color)));
        }

        public void Execute()
        {
            for (int index = 0; index < numVertices; ++index)
            {
                Vector3 vertex = UnsafeUtility.ReadArrayElement<Vector3>(vertices, index);
                float verticeDistance = Vector3.Distance(brushPosition, meshTransformMatrix.MultiplyPoint(vertex));

                float colorWeight = (verticeDistance - innerRadius) / (outerRadius - innerRadius);

                Color finalColor = Color.Lerp(innerColor, outerColor, colorWeight);

                colors[index * 3] = finalColor.r;
                colors[index * 3 + 1] = finalColor.g;
                colors[index * 3 + 2] = finalColor.b;
            }
        }
    }

    private VCPaintJob paintJob;
    private JobHandle paintJobHandle;

    private MeshFilter meshFilter;

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
                
        paintJob.Initialize(mesh);

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
            paintJob.GetResult(meshFilter.mesh);
            ReleaseResources();
            
            OnJobCompleted(this);
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
        paintJob.colors.Dispose();
    }
}
