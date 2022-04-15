using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Grid 
{
	public static Grid instance;	

	public Vector3 gridWorldSize;
	Node[,,] grid;

	int gridSizeX, gridSizeY, gridSizeZ;
	public Grid()
	{
		gridSizeX = 512;//VoxelData.ChunkWidth * VoxelData.xWidth;
		gridSizeY = 64;//VoxelData.ChunkHeight * VoxelData.zWidth;
		gridSizeZ = 512;//VoxelData.ChunkWidth * VoxelData.xWidth;
		grid = new Node[gridSizeX, gridSizeY, gridSizeZ];
		Debug.Log("initialized grid map size (x,y,z): " + gridSizeX + " " + gridSizeY + " " + gridSizeZ);
		CreateGrid();

		if (instance == null)
			instance = this;
		else
			throw new System.Exception("YOu should only instantiate one Grid object");
	}

	void CreateGrid()
	{
		for (int x = 0; x < gridSizeX; x++)
		{
			for (int y = 0; y < gridSizeY; y++)
			{
				for (int z = 0; z < gridSizeZ; z++)
				{
					Vector3 checkVector = new Vector3(x, y, z);
					bool isSolid = World.instance.CheckForVoxel(checkVector);
					grid[x, y, z] = new Node(isSolid, checkVector);
				}
			}
		}
	}

	public static int count = 0;
	public List<Node> GetNeighbours(Node node)
	{
		//if (count >= 10)
		//	throw new System.Exception("fail");
		List<Node> neighbours = new List<Node>();

		//Debug.Log("start");
		foreach(Vector3 vector in VoxelData.faceChecks)
		{
			Vector3 checkPos = node.gridPos + vector;

			// we only want to add it if it's in the world (so no out of range bugs)
			if (World.instance.IsVoxelInWorld(checkPos))
				neighbours.Add(grid[Mathf.FloorToInt(checkPos.x), Mathf.FloorToInt(checkPos.y),Mathf.FloorToInt(checkPos.z)]);
		}

		//Debug.Log("end------------------");
		count++;
		return neighbours;
	}

	public Node NodeFromWorldPoint(Vector3 worldPosition)
	{
		return grid[Mathf.FloorToInt(worldPosition.x), Mathf.FloorToInt(worldPosition.y), Mathf.FloorToInt(worldPosition.z)];
	}
	public List<Node> path;

	public Node[,,] GetGrid()
	{
		return this.grid;
	}

	public void UpdateNode(Vector3 pos, bool isWalkable)
	{
		grid[Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z)].walkable = isWalkable;
	}

	public bool CheckForNode(Vector3 pos)
	{
		return grid[Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z)].walkable; 
	}
}