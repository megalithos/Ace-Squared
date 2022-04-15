using System;
using System.Collections.Generic;
using System.Security.Cryptography; // for hashing vectors
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

public class Chunk
{
    public ChunkCoord coord;
    GameObject chunkObject;
    public MeshRenderer meshRenderer;
    public MeshFilter meshFilter;
    MeshCollider meshCollider;
    int vertexIndex = 0;
    public byte[,,] voxelMap = new byte[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth]; // olkoot korkeus eka (y) sitten x ja sit z
    public UInt32[,,] voxelColors = new UInt32[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth];
    public Renderer renderer;
    public float[] darknessValues = new float[1];//  new float[VoxelData.ChunkWidth * VoxelData.ChunkHeight * VoxelData.ChunkWidth]; // <- change to byte array somehow
    List<int> colorIDS = new List<int>();
    List<Vector3> vertices = new List<Vector3>();
    List<int> triangles = new List<int>();
    Dictionary<Face, Color32> faceColors = new Dictionary<Face, Color32>();

    World world;
    public bool isVoxelMapPopulated = false;

    public bool isActive
    {

        get { return chunkObject.activeSelf; }
        set { chunkObject.SetActive(value); }

    }

    public Vector3 position
    {

        get { return chunkObject.transform.position; }

    }

    bool IsVoxelInChunk(int x, int y, int z)
    {

        if (x < 0 || x > VoxelData.ChunkWidth - 1 || y < 0 || y > VoxelData.ChunkHeight - 1 || z < 0 || z > VoxelData.ChunkWidth - 1)
            return false;
        else
            return true;

    }

    public Chunk(ChunkCoord _coord, World _world, byte[] mapData, UInt32[] _flattenedColors)
    {

        coord = _coord;
        world = _world;
        chunkObject = new GameObject();
        meshFilter = chunkObject.AddComponent<MeshFilter>();
        meshRenderer = chunkObject.AddComponent<MeshRenderer>();
        meshCollider = chunkObject.AddComponent<MeshCollider>();
        meshRenderer.material = world.material;
        chunkObject.transform.SetParent(world.transform);
        chunkObject.transform.position = new Vector3(coord.x * VoxelData.ChunkWidth, 0f, coord.z * VoxelData.ChunkWidth);
        chunkObject.name = "Chunk " + coord.x + ", " + coord.z;
        chunkObject.tag = "worldterrain";
        meshCollider.sharedMaterial = world.noFrictionMaterial;
        PopulateVoxelMap(mapData, _flattenedColors);
        GenerateVoxelDarknessValues();
    }


    void GenerateVoxelDarknessValues()
    {
        for (int i = 0; i < darknessValues.Length; i++)
        {
            float randomValue = Random.Range(-0.075f, 0f);
            darknessValues[i] = randomValue;
        }
    }
    public void UpdateChunk()
    {

        ClearMeshData();

        for (int y = 0; y < VoxelData.ChunkHeight; y++)
        {
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int z = 0; z < VoxelData.ChunkWidth; z++)
                {
                    if (world.blocktypes[voxelMap[x, y, z]].isSolid)
                        UpdateMeshData(new Vector3(x, y, z));
                }
            }
        }

        CreateMesh();

    }

    // !!! potential point of optimizing.
    // maybe don't update whole chunk if only one block is edited.
    public void EditVoxel(Vector3 pos, int color, byte newID)
    {
        int xCheck = Mathf.FloorToInt(pos.x);
        int yCheck = Mathf.FloorToInt(pos.y);
        int zCheck = Mathf.FloorToInt(pos.z);

        xCheck -= Mathf.FloorToInt(chunkObject.transform.position.x);
        zCheck -= Mathf.FloorToInt(chunkObject.transform.position.z);

        voxelMap[xCheck, yCheck, zCheck] = newID;
        if (newID > 0)
        {
            voxelColors[xCheck, yCheck, zCheck] = (uint)color;
        }

        UpdateChunk();
        UpdateSurroundingVoxels(xCheck, yCheck, zCheck);
    }

    public Vector3 EditVoxelNoUpdate(Vector3 pos, int color, byte solid)
    {
        int xCheck = Mathf.FloorToInt(pos.x);
        int yCheck = Mathf.FloorToInt(pos.y);
        int zCheck = Mathf.FloorToInt(pos.z);

        xCheck -= Mathf.FloorToInt(chunkObject.transform.position.x);
        zCheck -= Mathf.FloorToInt(chunkObject.transform.position.z);

        voxelMap[xCheck, yCheck, zCheck] = solid;
        if (solid > 0)
        {
            voxelColors[xCheck, yCheck, zCheck] = (uint)color;
        }

        return new Vector3(xCheck, yCheck, zCheck);
    }

    void ClearMeshData()
    {

        vertexIndex = 0;
        vertices.Clear();
        triangles.Clear();
        faceColors.Clear();
        numberOrder = -1;
    }

    public void FinishInitialization()
    {
        UpdateChunk();
    }

    public GameObject GetReferenceToThisGameObject()
    {
        return chunkObject;
    }

    public const int defaultCount = 20;
    static int count = defaultCount;

    public static void ResetCount()
    {
        count = defaultCount;
    }

    void PopulateVoxelMap(byte[] mapData, UInt32[] _flattenedColors)
    {
        //for (int y = 0; y < VoxelData.ChunkHeight; y++)
        //{
        //	for (int x = 0; x < VoxelData.ChunkWidth; x++)
        //	{
        //		for (int z = 0; z < VoxelData.ChunkWidth; z++)
        //		{
        //			

        //		}
        //	}
        //}
        for (int y = 0; y < VoxelData.ChunkWidth; ++y)
        {
            for (int x = 0; x < VoxelData.ChunkWidth; ++x)
            {
                for (int z = 0; z < VoxelData.ChunkHeight; ++z)
                {
                    voxelMap[x, z, y] = mapData[count];
                    voxelColors[x, z, y] = _flattenedColors[count];
                    count++;
                }
            }
        }

        isVoxelMapPopulated = true;
    }

    public byte GetVoxelFromGlobalVector3(Vector3 pos)
    {

        int xCheck = Mathf.FloorToInt(pos.x);
        int yCheck = Mathf.FloorToInt(pos.y);
        int zCheck = Mathf.FloorToInt(pos.z);

        xCheck -= Mathf.FloorToInt(chunkObject.transform.position.x);
        zCheck -= Mathf.FloorToInt(chunkObject.transform.position.z);
        if (xCheck < 0 || xCheck > VoxelData.ChunkWidth - 1 || yCheck < 0 || yCheck > VoxelData.ChunkHeight - 1 || zCheck < 0 || zCheck > VoxelData.ChunkWidth - 1)
            return 0;

        return voxelMap[xCheck, yCheck, zCheck];

    }

    public uint GetVoxelColorFromGlobalVector3(Vector3 pos)
    {
        int xCheck = Mathf.FloorToInt(pos.x);
        int yCheck = Mathf.FloorToInt(pos.y);
        int zCheck = Mathf.FloorToInt(pos.z);

        xCheck -= Mathf.FloorToInt(chunkObject.transform.position.x);
        zCheck -= Mathf.FloorToInt(chunkObject.transform.position.z);
        if (xCheck < 0 || xCheck > VoxelData.ChunkWidth - 1 || yCheck < 0 || yCheck > VoxelData.ChunkHeight - 1 || zCheck < 0 || zCheck > VoxelData.ChunkWidth - 1)
            return 0;

        return voxelColors[xCheck, yCheck, zCheck];
    }

    // This method is for checking if certain point on the voxelmap is valid.
    bool CheckVoxel(Vector3 positionOfTileRelativeToChunk)
    {

        int x = Mathf.FloorToInt(positionOfTileRelativeToChunk.x);
        int y = Mathf.FloorToInt(positionOfTileRelativeToChunk.y);
        int z = Mathf.FloorToInt(positionOfTileRelativeToChunk.z);

        if (!IsVoxelInChunk(x, y, z))
            return world.CheckForVoxel(positionOfTileRelativeToChunk + position);

        return world.blocktypes[voxelMap[x, y, z]].isSolid;

    }

    public void UpdateSurroundingVoxels(int x, int y, int z)
    {

        Vector3 thisVoxel = new Vector3(x, y, z);

        for (int p = 0; p < 6; p++)
        {

            Vector3 currentVoxel = thisVoxel + VoxelData.faceChecks[p];

            if (!IsVoxelInChunk((int)currentVoxel.x, (int)currentVoxel.y, (int)currentVoxel.z))
            {
                if (world.IsChunkInWorld(world.GetChunkCoordFromVector3(currentVoxel + position)))
                    world.GetChunkFromVector3(currentVoxel + position).UpdateChunk();
            }

        }

    }

    /// <summary>
    /// for adding right 'nth' numbers to Faces
    /// </summary>
    int numberOrder = -1;
    void UpdateMeshData(Vector3 pos)
    {
        numberOrder++;
        if (voxelMap[Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z)] != 0)
        {
            // loop through each face of the cube
            for (int p = 0; p < 6; p++)
            {

                if (!CheckVoxel(pos + VoxelData.faceChecks[p]))
                {
                    Face thisFace = new Face(new Vector3(pos[0], pos[1], pos[2]) + VoxelData.voxelVerts[VoxelData.voxelTris[p, 0]],
                        new Vector3(pos[0], pos[1], pos[2]) + VoxelData.voxelVerts[VoxelData.voxelTris[p, 1]],
                        new Vector3(pos[0], pos[1], pos[2]) + VoxelData.voxelVerts[VoxelData.voxelTris[p, 2]],
                        new Vector3(pos[0], pos[1], pos[2]) + VoxelData.voxelVerts[VoxelData.voxelTris[p, 3]],
                        numberOrder,
                        pos
                        );


                    // add face data
                    if (!faceColors.ContainsKey(thisFace))
                    {
                        byte[] colorBytes = BitConverter.GetBytes(voxelColors[(int)pos.x, (int)pos.y, (int)pos.z]);
                        //int red = BitConverter.ToInt16(colorBytes, colorBytes[2]);
                        //int green = BitConverter.ToInt16(colorBytes, colorBytes[4]);
                        //int blue = BitConverter.ToInt16(colorBytes, colorBytes[6]);

                        Color32 _color = new Color32(colorBytes[2], colorBytes[1], colorBytes[0], (byte)1);

                        faceColors.Add(thisFace, _color);
                    }

                    vertices.Add(new Vector3(pos[0], pos[1], pos[2]) + VoxelData.voxelVerts[VoxelData.voxelTris[p, 0]]);
                    vertices.Add(new Vector3(pos[0], pos[1], pos[2]) + VoxelData.voxelVerts[VoxelData.voxelTris[p, 1]]);
                    vertices.Add(new Vector3(pos[0], pos[1], pos[2]) + VoxelData.voxelVerts[VoxelData.voxelTris[p, 2]]);
                    vertices.Add(new Vector3(pos[0], pos[1], pos[2]) + VoxelData.voxelVerts[VoxelData.voxelTris[p, 3]]);
                    triangles.Add(vertexIndex);
                    triangles.Add(vertexIndex + 1);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex + 2);
                    triangles.Add(vertexIndex + 1);
                    triangles.Add(vertexIndex + 3);
                    vertexIndex += 4;
                }
            }
        }

    }

    void CreateMesh()
    {

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();

        mesh.triangles = triangles.ToArray();

        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
        Vector3[] localVertices = mesh.vertices;

        // create new colors array where the colors will be created.
        Color[] colors = new Color[localVertices.Length];

        int a = UnityEngine.Random.Range(0, 3);

        SetColors(colors);

        // assign the array of colors to the Mesh.
        mesh.colors = colors;
        meshCollider.sharedMesh = meshFilter.mesh;
    }

    void SetColors(Color[] colors)
    {
        int _count = 0;
        foreach (KeyValuePair<Face, Color32> kvp in faceColors)
        {
            Vector3 globalPosOfVoxel = GetGlobalPosOfVoxel(kvp.Key.GetLocalPos());
            int darknessValueOfVoxel = BitConverter.ToInt32(GetColorDarknessValue(globalPosOfVoxel), 0);

            for (int j = 0; j < 4; j++)
            {
                Random.seed = darknessValueOfVoxel;
                colors[_count] = (Color)kvp.Value;//ChangeColorBrightness(color, Random.Range(-.1f, 0f));
                _count++;
            }
        }
    }

    Vector3 GetGlobalPosOfVoxel(Vector3 voxelLocalPos)
    {
        Vector3 vectorToChunkOrigin = new Vector3(coord.x * VoxelData.ChunkWidth, 0f, coord.z * VoxelData.ChunkWidth);
        return voxelLocalPos + vectorToChunkOrigin;
    }

    byte[] GetColorDarknessValue(Vector3 globalPos)
    {
        string Vector3String = Convert.ToString(globalPos.x) + Convert.ToString(globalPos.y) + Convert.ToString(globalPos.z);
        byte[] hashed = GetHash(Vector3String);
        return hashed;
    }

    public float ScaleFloat(float OldMin, float OldMax, float NewMin, float NewMax, float OldValue)
    {

        float OldRange = (OldMax - OldMin);
        float NewRange = (NewMax - NewMin);
        float NewValue = (((OldValue - OldMin) * NewRange) / OldRange) + NewMin;

        return (NewValue);
    }

    private byte[] GetHash(string inputString)
    {
        using (HashAlgorithm algorithm = SHA256.Create())
            return algorithm.ComputeHash(Encoding.UTF8.GetBytes(inputString));
    }

    private string GetHashString(string inputString)
    {
        StringBuilder sb = new StringBuilder();
        foreach (byte b in GetHash(inputString))
            sb.Append(b.ToString("X2"));

        return sb.ToString();
    }


    /// <summary>
    /// Creates color with corrected brightness.
    /// </summary>
    /// <param name="color">Color to correct.</param>
    /// <param name="correctionFactor">The brightness correction factor. Must be between -1 and 1. 
    /// Negative values produce darker colors.</param>
    /// <returns>
    /// Corrected <see cref="Color"/> structure.
    /// </returns>
    public static Color ChangeColorBrightness(Color color, float correctionFactor)
    {
        float red = (float)color.r;
        float green = (float)color.g;
        float blue = (float)color.b;

        if (correctionFactor < 0)
        {
            correctionFactor = 1 + correctionFactor;
            red *= correctionFactor;
            green *= correctionFactor;
            blue *= correctionFactor;
        }
        else
        {
            red = (1 - red) * correctionFactor + red;
            green = (1 - green) * correctionFactor + green;
            blue = (1 - blue) * correctionFactor + blue;
        }

        return new Color(red, green, blue, color.a);
    }
}

class Face
{
    Vector3[] vertices = new Vector3[4];
    Vector3 localCoords;

    int nth;
    public Face(Vector3 vec1, Vector3 vec2, Vector3 vec3, Vector3 vec4, int _nth, Vector3 _localCoords)
    {
        vertices[0] = vec1;
        vertices[1] = vec2;
        vertices[2] = vec3;
        vertices[3] = vec4;
        nth = _nth;
        localCoords = _localCoords;
    }

    /// <summary>
    /// Basically just multiply voxelPos.x voxelPos.y voxelPos.z together and take the result
    /// </summary>
    public int GetNth()
    {
        return nth;
    }

    public Vector3 GetLocalPos()
    {
        return localCoords;
    }
}

public class ChunkCoord : IEquatable<ChunkCoord>
{

    public int x;
    public int z;

    public ChunkCoord()
    {

        x = 0;
        z = 0;

    }

    public ChunkCoord(int _x, int _z)
    {

        x = _x;
        z = _z;

    }

    public ChunkCoord(Vector3 pos)
    {

        int xCheck = Mathf.FloorToInt(pos.x);
        int zCheck = Mathf.FloorToInt(pos.z);
        x = xCheck / VoxelData.ChunkWidth;
        z = zCheck / VoxelData.ChunkWidth;

    }

    public bool Equals(ChunkCoord other)
    {

        if (other == null)
            return false;
        else if (other.x == x && other.z == z)
            return true;
        else
            return false;

    }

}