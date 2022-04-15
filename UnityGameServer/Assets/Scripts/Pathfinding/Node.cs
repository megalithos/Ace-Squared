using UnityEngine;
using System.Collections;
using System;

public class Node// : IEquatable<Node>
{
	public bool walkable;
	public Vector3 gridPos;
	public int gridX;
	public int gridY;
	public int gCost;
	public int hCost;
	public Node parent;

	public Node(bool _walkable, Vector3 _gridPos)
	{
		walkable = _walkable;
		gridPos = _gridPos;
	}

	public int fCost
	{
		get
		{
			return gCost + hCost;
		}
	}

//	public bool Equals(Node other)
//	{
//		return gridPos == other.gridPos;
//	}
}