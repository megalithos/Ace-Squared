using System;
using System.Collections.Generic;
using UnityEngine;

public class Chunk
{
	public ChunkCoord coord;
	GameObject chunkObject;
	public MeshRenderer meshRenderer;
	public MeshFilter meshFilter;
	MeshCollider meshCollider;
	int vertexIndex = 0;

	public byte[,,] voxelMap = new byte[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth]; // olkoot korkeus eka (y) sitten x ja sit z
	List<int> colorIDS = new List<int>();
	List<Vector3> vertices = new List<Vector3>();
	List<int> triangles = new List<int>();
	Dictionary<Face, byte> faceColors = new Dictionary<Face, byte>();
	public UInt32[,,] voxelColors = new UInt32[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkWidth];

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

	public Chunk(ChunkCoord _coord, World _world, byte[] mapData)
	{

		coord = _coord;
		world = _world;
		chunkObject = new GameObject();
		meshFilter = chunkObject.AddComponent<MeshFilter>();
		meshRenderer = chunkObject.AddComponent<MeshRenderer>();
        meshRenderer.enabled = false;
		meshCollider = chunkObject.AddComponent<MeshCollider>();
		meshRenderer.material = world.material;
		chunkObject.transform.SetParent(world.transform);
		chunkObject.transform.position = new Vector3(coord.x * VoxelData.ChunkWidth, 0f, coord.z * VoxelData.ChunkWidth);
		chunkObject.name = "Chunk " + coord.x + ", " + coord.z;
		chunkObject.tag = "worldterrain";
		meshCollider.sharedMaterial = world.noFrictionMaterial;
		PopulateVoxelMap(mapData);
	}

	void UpdateChunk()
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

	public void EditVoxel(Vector3 pos, byte newID, int color)
	{

		int xCheck = Mathf.FloorToInt(pos.x);
		int yCheck = Mathf.FloorToInt(pos.y);
		int zCheck = Mathf.FloorToInt(pos.z);

		xCheck -= Mathf.FloorToInt(chunkObject.transform.position.x);
		zCheck -= Mathf.FloorToInt(chunkObject.transform.position.z);

		voxelMap[xCheck, yCheck, zCheck] = newID;

		if (newID >0) // if we wish to place blocks
			voxelColors[xCheck, yCheck, zCheck] = (uint)color;

		UpdateSurroundingVoxels(xCheck, yCheck, zCheck);

		UpdateChunk();

	}

	void ClearMeshData()
	{

		vertexIndex = 0;
		vertices.Clear();
		triangles.Clear();
		faceColors.Clear();
	}

	public void FinishInitialization()
	{
		UpdateChunk();
	}

	static int count = 20;
	void PopulateVoxelMap(byte[] mapData)
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
					voxelMap[x, z, y] = mapData[count++];
				}
			}
		}

		isVoxelMapPopulated = true;
	}

	//public byte GetVoxelFromGlobalVector3(Vector3 pos)
	//{

	//	int xCheck = Mathf.FloorToInt(pos.x);
	//	int yCheck = Mathf.FloorToInt(pos.y);
	//	int zCheck = Mathf.FloorToInt(pos.z);

	//	xCheck -= Mathf.FloorToInt(chunkObject.transform.position.x);
	//	zCheck -= Mathf.FloorToInt(chunkObject.transform.position.z);
	//	if (xCheck < 0 || xCheck > VoxelData.ChunkWidth - 1 || yCheck < 0 || yCheck > VoxelData.ChunkHeight - 1 || zCheck < 0 || zCheck > VoxelData.ChunkWidth - 1)
	//		return 0; // it is outside, return air

	//	return voxelMap[xCheck, yCheck, zCheck];

	//}

	public byte GetVoxelFromGlobalVector3(Vector3 pos)
	{

		int xCheck = Mathf.FloorToInt(pos.x);
		int yCheck = Mathf.FloorToInt(pos.y);
		int zCheck = Mathf.FloorToInt(pos.z);

		xCheck -= Mathf.FloorToInt(position.x);
		zCheck -= Mathf.FloorToInt(position.z);
		if (xCheck < 0 || xCheck > VoxelData.ChunkWidth - 1 || yCheck < 0 || yCheck > VoxelData.ChunkHeight - 1 || zCheck < 0 || zCheck > VoxelData.ChunkWidth - 1)
			return 0;
		return voxelMap[xCheck, yCheck, zCheck];
	}

	// This method is for checking if certain point on the voxelmap is valid.
	bool CheckVoxel(Vector3 pos)
	{

		int x = Mathf.FloorToInt(pos.x);
		int y = Mathf.FloorToInt(pos.y);
		int z = Mathf.FloorToInt(pos.z);

		if (!IsVoxelInChunk(x, y, z))
			return world.CheckForVoxel(pos + position);

		return world.blocktypes[voxelMap[x, y, z]].isSolid;

	}

	void UpdateSurroundingVoxels(int x, int y, int z)
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

	void UpdateMeshData(Vector3 pos)
	{

		if (voxelMap[Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z)] != 0)
		{
			// loop through each face of the cube
			for (int p = 0; p < 6; p++)
			{
				// if there is no voxel -> draw the face
				if (!CheckVoxel(pos + VoxelData.faceChecks[p]))
				{
					Face thisFace = new Face(new Vector3(pos[0], pos[1], pos[2]) + VoxelData.voxelVerts[VoxelData.voxelTris[p, 0]],
						new Vector3(pos[0], pos[1], pos[2]) + VoxelData.voxelVerts[VoxelData.voxelTris[p, 1]],
						new Vector3(pos[0], pos[1], pos[2]) + VoxelData.voxelVerts[VoxelData.voxelTris[p, 2]],
						new Vector3(pos[0], pos[1], pos[2]) + VoxelData.voxelVerts[VoxelData.voxelTris[p, 3]]);

					// add face data
					//colorIDS.Add(voxelMap[(int)pos.x, (int)pos.y, (int)pos.z]);
					if (!faceColors.ContainsKey(thisFace))
					{
						faceColors.Add(thisFace, voxelMap[(int)pos.x, (int)pos.y, (int)pos.z]);
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

		int _count = 0;
		//for (int i = 0; i < colorIDS.Count; i++)
		//{
		//	for (int j = 0; j < 4; j++)
		//	{
		//		switch (colorIDS[i])
		//		{
		//			case 0:
		//				colors[_count] = Color.magenta;
		//				break;
		//			case 1:
		//				colors[_count] = Color.green;
		//				break;
		//			case 2:
		//				colors[_count] = Color.yellow;
		//				break;

		//			case 3:
		//				colors[_count] = Color.red;
		//				break;
		//		}
		//		_count++;
		//	}
		//}

		foreach (KeyValuePair<Face, byte> kvp in faceColors)
		{
			for (int j = 0; j < 4; j++)
			{
				switch (kvp.Value)
				{
					case 0:
						colors[_count] = Color.magenta;
						break;
					case 1:
						colors[_count] = Color.green;
						break;
					case 2:
						colors[_count] = Color.yellow;
						break;

					case 3:
						colors[_count] = Color.red;
						break;
				}
				_count++;
			}
		}

		// assign the array of colors to the Mesh.
		mesh.colors = colors;
		meshCollider.sharedMesh = meshFilter.mesh;
	}

}

class Face
{
	Vector3[] vertices = new Vector3[4];

	public Face(Vector3 vec1, Vector3 vec2, Vector3 vec3, Vector3 vec4)
	{
		vertices[0] = vec1;
		vertices[1] = vec2;
		vertices[2] = vec3;
		vertices[3] = vec4;
	}
}

public class ChunkCoord
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