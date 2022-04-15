using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// this is used for only finding if a voxel is connected to the root y level of the world.
// which can be (and is used) to find out which voxels need to be destroyed, when a 
// player destroys one voxel. so we get block destruction like in old ace of spades.
public class Pathfinder 
{
	Grid grid;
	int targetYLevel;

	// constructor
	public Pathfinder()
	{
		grid = new Grid();
		targetYLevel = 1;
	}

	HashSet<Node> FindPath(Vector3 startPos)
	{
		Node startNode = grid.NodeFromWorldPoint(startPos);
		Node targetNode = grid.NodeFromWorldPoint(new Vector3(startPos.x, targetYLevel, startPos.z));

		//throw new System.Exception("lol");
		List<Node> openSet = new List<Node>();
		HashSet<Node> closedSet = new HashSet<Node>();
		openSet.Add(startNode);

		while (openSet.Count > 0)
		{
			Node node = openSet[0];
			for (int i = 1; i < openSet.Count; i++)
			{
				if (openSet[i].fCost < node.fCost || openSet[i].fCost == node.fCost)
				{
					if (openSet[i].hCost < node.hCost)
						node = openSet[i];
				}
			}

			openSet.Remove(node);
			closedSet.Add(node);

			// check if we reached the target
			if (node == targetNode)
			{
				RetracePath(startNode, targetNode);
				return null;
			}

			foreach (Node neighbour in grid.GetNeighbours(node))
			{
				if (!neighbour.walkable || closedSet.Contains(neighbour))
				{
					continue;
				}

				int newCostToNeighbour = node.gCost + GetDistance(node, neighbour);
				if (newCostToNeighbour < neighbour.gCost || !openSet.Contains(neighbour))
				{
					neighbour.gCost = newCostToNeighbour;
					neighbour.hCost = GetDistance(neighbour, targetNode);
					neighbour.parent = node;

					if (!openSet.Contains(neighbour))
						openSet.Add(neighbour);
				}
			}
		}

		// since we didn't find path, return all blocks we checked
		return closedSet;
	}

	public HashSet<Server.UpdatedBlock> GetPathBlocks(Vector3 pathStart)
	{
		HashSet<Server.UpdatedBlock> updatedBlocksList = new HashSet<Server.UpdatedBlock>();
		//FindPath(pathStart);	
		HashSet<Node> lst = FindPath(pathStart);
		if (lst == null)
			return null;

		foreach (Node node in lst)
		{
			updatedBlocksList.Add(new Server.UpdatedBlock(node.gridPos, 0, 0));
		}

		return updatedBlocksList;
	}
	
	void RetracePath(Node startNode, Node endNode)
	{
		List<Node> path = new List<Node>();
		Node currentNode = endNode;

		while (currentNode != startNode)
		{
			path.Add(currentNode);
			currentNode = currentNode.parent;
		}
		path.Reverse();

		grid.path = path;

	}

	int GetDistance(Node nodeA, Node nodeB)
	{
		return System.Convert.ToInt32(Vector3.Distance(nodeA.gridPos, nodeB.gridPos));
	}

	public void TestIsSolid(Vector3 pos)
	{
		Vector3 checkPos = pos;
	}
}
