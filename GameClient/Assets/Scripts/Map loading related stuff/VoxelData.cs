using System;
using UnityEngine;

public static class VoxelData
{
    public static int ChunkWidth;
    public static int ChunkHeight;
    public static int xWidth;
    public static int zWidth;
    public static int WorldSizeInChunks;

    public static void Init(byte[] mapData)
    {
        WorldSizeInChunks = BitConverter.ToInt32(mapData, 0);
        xWidth = BitConverter.ToInt32(mapData, 4);
        zWidth = BitConverter.ToInt32(mapData, 8);
        ChunkWidth = BitConverter.ToInt32(mapData, 12);
        ChunkHeight = BitConverter.ToInt32(mapData, 16);

        Debug.Log("Set world data.");
        Debug.Log("WorldSizeInChunks: " + WorldSizeInChunks);
        Debug.Log("xWidth: " + xWidth + ", with type: " + xWidth.GetType());
        Debug.Log("zWidth: " + zWidth);
        Debug.Log("ChunkWidth: " + ChunkWidth);
        Debug.Log("ChunkHeight: " + ChunkHeight);
    }
    public static int WorldSizeInVoxels
    {
        get { return WorldSizeInChunks * ChunkWidth; }

    }

    // paikkavektorit kuution kulmille (kulmien pisteille)
    public static readonly Vector3[] voxelVerts = new Vector3[8] {

        new Vector3(0.0f, 0.0f, 0.0f),
        new Vector3(1.0f, 0.0f, 0.0f),
        new Vector3(1.0f, 1.0f, 0.0f),
        new Vector3(0.0f, 1.0f, 0.0f),
        new Vector3(0.0f, 0.0f, 1.0f),
        new Vector3(1.0f, 0.0f, 1.0f),
        new Vector3(1.0f, 1.0f, 1.0f),
        new Vector3(0.0f, 1.0f, 1.0f),

    };

    public static readonly Vector3[] faceChecks = new Vector3[6] {

        new Vector3(0.0f, 0.0f, -1.0f),
        new Vector3(0.0f, 0.0f, 1.0f),
        new Vector3(0.0f, 1.0f, 0.0f),
        new Vector3(0.0f, -1.0f, 0.0f),
        new Vector3(-1.0f, 0.0f, 0.0f),
        new Vector3(1.0f, 0.0f, 0.0f)

    };

    // paikkavektorit jokaisen pinnan kolmioille
    // ensimmäinen pari kertoo pinnan toisen kolmion paikkakoordinaatit ja 
    // toinen pari kertoo juurikin toisen kolmion paikkakoordinaatit
    public static readonly int[,] voxelTris = new int[6, 4] {

		// 0 1 2 2 1 3
		{0, 3, 1, 2}, // Back Face
		{5, 6, 4, 7}, // Front Face
		{3, 7, 2, 6}, // Top Face
		{1, 5, 0, 4}, // Bottom Face
		{4, 7, 0, 3}, // Left Face
		{1, 2, 5, 6} // Right Face

	};
}
