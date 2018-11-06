using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.InteropServices;

public static class VCVertexBufferStaticContainer
{
    // Use a class so we use references instead of copies 
    // when manipulating the vertexBuffersMap
    class VertexBufferData
    {
        public Vector3[] vertices;
    }

    private static bool isInitialized = false;
    private static Dictionary<Mesh, VertexBufferData> vertexBuffersMap;

    public static IntPtr GetOrCacheVertexBufferPtrFromMesh(Mesh mesh)
    {
        InitIFN();

        VertexBufferData vertexBufferData;
        if (!vertexBuffersMap.TryGetValue(mesh, out vertexBufferData))
        {
            vertexBufferData = new VertexBufferData()
            {
                vertices = mesh.vertices
            };
            vertexBuffersMap.Add(mesh, vertexBufferData);
        }
        
        return Marshal.UnsafeAddrOfPinnedArrayElement(vertexBufferData.vertices, 0);
    }

    private static void InitIFN()
    {
        if (!isInitialized)
        {
            isInitialized = true;
            vertexBuffersMap = new Dictionary<Mesh, VertexBufferData>();
        }
    }

    public static void Release()
    {
        if (vertexBuffersMap != null)
        {
            vertexBuffersMap.Clear();
        }
    }
}
