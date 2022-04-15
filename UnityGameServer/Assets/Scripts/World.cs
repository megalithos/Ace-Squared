using System.Collections;
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class World : MonoBehaviour
{
	public static World instance;
	public int seed;

	public Transform player;
	public Vector3 spawnPosition;
	public BlockType[] blocktypes;
	public Material material;
	public PhysicMaterial noFrictionMaterial;
	Chunk[,] chunks;

	List<ChunkCoord> activeChunks = new List<ChunkCoord>();
	ChunkCoord playerChunkCoord;
	ChunkCoord playerLastChunkCoord;
	Pathfinder pathfinder;

	byte[] mapData;

	private void Awake()
	{
		// setup instance
		if (instance == null)
		{
			instance = this;
		}
		else
			Destroy(this);

		// ensure we get the map filepath before trying to read the map data 
		Config.Init();

		gameObject.transform.position = Vector3.zero;

		Random.InitState(seed);
		mapData = BasicMapGenerator.Program.LoadVxlFile(Config.MAP_FILEPATH);
		
		VoxelData.Init(mapData);
		chunks = new Chunk[VoxelData.xWidth, VoxelData.zWidth];
		
		spawnPosition = new Vector3(239.9905f, 20, 239.9905f);
		GenerateWorld();
		//playerLastChunkCoord = GetChunkCoordFromVector3(player.position);
		Debug.Log("Map initialized and loaded successfully.");

		if (Config.ENABLE_FALLING_BLOCKS)
			pathfinder = new Pathfinder();
	}

	private void Update()
	{
		if (pathfindingDone && Config.ENABLE_FALLING_BLOCKS)
		{
			//Debug.Log("calling onpathfindingpartdone()");
			pathfindingDone = false;
			OnPathfindingPartDone(); // call on main thread so we can update the correct chunks
		}
	}

	void GenerateWorld()
	{

		for (int x = 0; x < VoxelData.xWidth; x++)
		{
			for (int z = 0; z < VoxelData.zWidth; z++)
			{
				CreateNewChunk(x, z);
			}
		}

		for (int x = 0; x < VoxelData.xWidth; x++)
		{
			for (int z = 0; z < VoxelData.zWidth; z++)
			{
				chunks[x, z].FinishInitialization();
			}
		}
		//player.position = spawnPosition;

	}

	public ChunkCoord GetChunkCoordFromVector3(Vector3 pos)
	{

		int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
		int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);
		return new ChunkCoord(x, z);

	}
	public Chunk GetChunkFromVector3(Vector3 pos)
	{
		int x = Mathf.FloorToInt(pos.x / VoxelData.ChunkWidth);
		int z = Mathf.FloorToInt(pos.z / VoxelData.ChunkWidth);
		return chunks[x, z];
	}
	//public bool CheckForVoxel(Vector3 pos)
	//{
	//	ChunkCoord thisChunk = new ChunkCoord(pos);

	//	if (!IsChunkInWorld(thisChunk) || pos.y < 0 || pos.y >= VoxelData.ChunkHeight)
	//		return false; // dont draw, the chunk we are checking is outside the world

	//	//Debug.Log(chunks[thisChunk.x, thisChunk.z].GetVoxelFromGlobalVector3(pos));
	//	return blocktypes[chunks[thisChunk.x, thisChunk.z].GetVoxelFromGlobalVector3(pos)].isSolid; // if it is solid dont draw.
	//}
	public bool CheckForVoxel(Vector3 pos)
	{
		ChunkCoord thisChunk = new ChunkCoord(pos);

		if (!IsChunkInWorld(thisChunk) || pos.y < 0 || pos.y >= VoxelData.ChunkHeight)
			return false; // dont draw, the chunk we are checking is outside the world


		//Debug.Log(chunks[thisChunk.x, thisChunk.z].GetVoxelFromGlobalVector3(pos));
		return blocktypes[chunks[thisChunk.x, thisChunk.z].GetVoxelFromGlobalVector3(pos)].isSolid; // if it is solid dont draw.
	}

	void CreateNewChunk(int x, int z)
	{
		chunks[x, z] = new Chunk(new ChunkCoord(x, z), this, mapData);
		activeChunks.Add(new ChunkCoord(x, z));
	}


	public bool IsChunkInWorld(ChunkCoord coord)
	{

		if (coord.x >= 0 && coord.x < VoxelData.xWidth && coord.z >= 0 && coord.z < VoxelData.zWidth)
			return true;
		else
		{
			return false;
		}
	}

	public bool IsVoxelInWorld(Vector3 pos)
	{

		if (pos.x >= 0 && pos.x < VoxelData.WorldSizeInVoxels && pos.y >= 0 && pos.y < VoxelData.ChunkHeight && pos.z >= 0 && pos.z < VoxelData.WorldSizeInVoxels)
			return true;
		else
			return false;

	}

	public void UpdateBlock(Vector3 pos, byte blockID, int color)
	{
		GetChunkFromVector3(pos).EditVoxel(pos, blockID, color);
	}

	HashSet<Server.UpdatedBlock> fallenBlocks = new HashSet<Server.UpdatedBlock>();
	bool pathfindingDone = false;
	/// <summary>
	/// If path is found, returns null list, which basically means that those blocks must not be destroyed.
	/// If path is not found, returns a list of all blocks that were looped through so the caller
	/// can then destroy those and update them.
	/// </summary>
	/// <param name="pathStart">Start of the path we wish to start looking for path.</param>
	/// <returns></returns>
	public HashSet<Server.UpdatedBlock> GetPathBlocks(Vector3 pos)
	{
		foreach (Vector3 vector3 in VoxelData.faceChecks)
		{
			Vector3 pathStart = pos + vector3;

			// if the node is not walkable, continue
			if (!Grid.instance.CheckForNode(pathStart))
				continue;
			
			HashSet<Server.UpdatedBlock> range = pathfinder.GetPathBlocks(pathStart);

			// don't add if we return null
			if (range != null)
				fallenBlocks.UnionWith(range);
		}
		pathfindingDone = true;
		return fallenBlocks;
	}

	public void OnPathfindingPartDone()
	{
		// edit fallen blocks/voxels to 0
		foreach (Server.UpdatedBlock block in fallenBlocks)
		{
			GetChunkFromVector3(block.position).EditVoxel(block.position, 0, 0);

			//if it is in list->remove it and add new one
			// if not in list->just add normally
			if (Server.updatedBlocks.Contains(block))
			{
				Server.updatedBlocks.Remove(block);
			}

			Server.updatedBlocks.Add(block);
		}

		if (fallenBlocks.Count > 0)
		{
			// send all fallen blocks to clients
			ServerSend.SplitServerUpdatedBlocksIntoChunksAndSendToNewlyConnectedClient(-1, fallenBlocks, true);
		}

		fallenBlocks.Clear();
	}

	public void TestIsSolid(Vector3 pos)
	{
		pathfinder.TestIsSolid(pos);
	}
}

[System.Serializable]
public class BlockType
{

	public string blockName;
	public bool isSolid;

}

namespace BasicMapGenerator
{
	static class Program
	{
		// 1. on z, 2. on y ja 3. on x
		const int worldSizeInChunks = 4096;
		const int xWidth = 64;
		const int zWidth = 64;
		const int chunkWidthInVoxels = 8;
		const int chunkHeightInVoxels = 64;

		static int[] beginningBytes = new int[5] // int is 4 bytes
		{
			worldSizeInChunks,
			zWidth,
			xWidth,
			chunkWidthInVoxels,
			chunkHeightInVoxels
		};

		static byte[,,] chunk = new byte[chunkWidthInVoxels, chunkHeightInVoxels, chunkWidthInVoxels];
		static byte[] flattenedChunk = new byte[beginningBytes.Length * 4 + worldSizeInChunks * chunkWidthInVoxels * chunkWidthInVoxels * chunkHeightInVoxels];

		static void GenerateMap()
		{
			int count = 0;
			foreach (int i in beginningBytes)
			{
				byte[] theseBytes = BitConverter.GetBytes(i);

				for (int j = 0; j < 4; j++) // because int is 4 bytes
				{
					flattenedChunk[count++] = theseBytes[j];
				}
			}

			Console.WriteLine("Generating a chunk...");
			for (int i = 0; i < worldSizeInChunks; i++)
			{
				for (int y = 0; y < chunkHeightInVoxels; y++)
				{
					for (int x = 0; x < chunkWidthInVoxels; x++)
					{
						for (int z = 0; z < chunkWidthInVoxels; z++)
						{
							//if (y == 0)
							//{
							//	flattenedChunk[count] = 1;
							//}
							//else if (y < 8)
							//{
							//	flattenedChunk[count] = 2;
							//}
							//else
							//{
							//	flattenedChunk[count] = 0; // air
							//}
							//if (i == 0 && y == 0 && x == 0 && z == 0)
							//	flattenedChunk[count] = 0;
							//if (i == 0 && y == 7 && x == 7 && z == 7)
							//	flattenedChunk[count] = 0;
							//if (i == 99 && y == 7 && x == 7 && z == 7)
							//	flattenedChunk[count] = 0;
							//if (i == 15 && y == 7 && x == 5 && z == 5)
							//	flattenedChunk[count] = 0;
							//if (i == 15 && y == 6 && x == 5 && z == 5)
							//	flattenedChunk[count] = 0;
							//if (i == 15 && y == 5 && x == 5 && z == 5)

							flattenedChunk[count] = 1;
							count++;
						}

					}
				}
			}

			Console.WriteLine("Chunk generated.");
		}

		static void GenerateChunkFromVxlFormat(string path)
		{
			int count = 0;
			foreach (int i in beginningBytes)
			{
				byte[] theseBytes = BitConverter.GetBytes(i);

				for (int j = 0; j < 4; j++) // because int is 4 bytes
				{
					flattenedChunk[count++] = theseBytes[j];
				}
			}
			int[,,] map = vxlConverter.GetloadedVxlMap(path);

			//for (int y = 0; y < 512; ++y)
			//{
			//	for (int x = 0; x < 512; ++x)
			//	{
			//		for (int z = 0; z < 64; ++z)
			//		{
			//			byte toSet = (byte)map[x, y, z];
			//			flattenedChunk[count++] = toSet;
			//		}
			//	}
			//}

			for (int chunkCoordZ = 0; chunkCoordZ < zWidth; chunkCoordZ++)
			{
				for (int chunkCoordX = 0; chunkCoordX < xWidth; chunkCoordX++)
				{
					for (int y = 0; y < chunkWidthInVoxels; ++y)
					{
						for (int x = 0; x < chunkWidthInVoxels; ++x)
						{
							for (int z = chunkHeightInVoxels - 1; z >= 0; --z)
							{
								byte toSet = (byte)map[y + chunkCoordX * chunkWidthInVoxels, x + chunkCoordZ * chunkWidthInVoxels, z];
								flattenedChunk[count] = toSet;
								count++;
							}
						}
					}
				}
			}
		}

		public static byte[] LoadVxlFile (string name)
		{
			GenerateChunkFromVxlFormat(name);
				
			return flattenedChunk;
			//ByteArrayToFile(@"C:\Users\kone\Desktop\Coding-master\C#\Unity\UnityGameServer\Assets\TestMap.zxmap", flattenedChunk);
			//ByteArrayToFile(@"C:\Users\kone\Desktop\Coding-master\C#\Unity\GameClient\Assets\StreamingAssets\TestMap.zxmap", flattenedChunk);
		}

		static bool ByteArrayToFile(string fileName, byte[] byteArray)
		{
			try
			{
				using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
				{
					fs.Write(byteArray, 0, byteArray.Length);
					return true;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("Exception caught in process: {0}", ex);
				return false;
			}

		}
	}

	public static class vxlConverter
	{
		static int[,,] map = new int[512, 512, 64]; // x, y, z
		static UInt32[,,] color = new UInt32[512, 512, 64];

		static void setgeom(int x, int y, int z, int t)
		{
			if (z >= 0 && z < 64)
			{
				map[x, y, z] = t;
			}
			else
				throw new Exception("Nyt meni jotain pieleen.");
		}

		// need to convert for endianness here if we read 32-bits at a time
		static void setcolor(int x, int y, int z, UInt32 c)
		{
			if (z >= 0 && z < 64)
			{
				color[x, y, z] = c;
			}
			else
				throw new Exception("Nyt meni jotain pieleen.");
		}

		// loads a map. Takes pointer to the byte array and a length parameter.
		static unsafe void load_map(byte* v, int len)
		{
			byte* basePtr = v;
			int x, y, z;

			// loop through 512x512x64 map matrix
			for (y = 0; y < 512; ++y)
			{
				for (x = 0; x < 512; ++x)
				{
					for (z = 0; z < 64; ++z)
					{
						setgeom(x, y, z, 1);
					}
					z = 0;
					for (; ; )
					{
						UInt32* color;

						int i;
						int number_4byte_chunks = v[0];
						int top_color_start = v[1];
						int top_color_end = v[2]; // inclusive
						int bottom_color_start;
						int bottom_color_end; // exclusive
						int len_top;
						int len_bottom;


						for (i = z; i < top_color_start; i++)
							setgeom(x, y, i, 0);

						color = (UInt32*)(v + 4);
						for (z = top_color_start; z <= top_color_end; z++)
							setcolor(x, y, z, *color++);

						len_bottom = top_color_end - top_color_start + 1;

						// check for end of data marker
						if (number_4byte_chunks == 0)
						{
							// infer ACTUAL number of 4-byte chunks from the length of the color data
							v += 4 * (len_bottom + 1);
							break;
						}

						// infer the number of bottom colors in next span from chunk length
						len_top = (number_4byte_chunks - 1) - len_bottom;

						// now skip the v pointer past the data to the beginning of the next span
						v += v[0] * 4;

						bottom_color_end = v[3]; // aka air start
						bottom_color_start = bottom_color_end - len_top;

						for (z = bottom_color_start; z < bottom_color_end; ++z)
						{
							setcolor(x, y, z, *color++);
						}

					}
				}
			}
			//assert(v - base == len);
		}

		public static unsafe int[,,] GetloadedVxlMap(string path)
		{
			//map = File.ReadAllBytes("hallway.vxl");
			byte[] arr = new byte[512 * 512 * 64];

			arr = File.ReadAllBytes(path);


			fixed (byte* ptr = arr)
			{
				load_map(ptr, arr.Length);
			}


			//while (true)
			//{
			//	int input = System.Convert.ToInt32(Console.ReadLine());
			//	int input2 = System.Convert.ToInt32(Console.ReadLine());
			//	int input3 = System.Convert.ToInt32(Console.ReadLine());

			//	Console.WriteLine("at coords {0}, {1}, {2} is {3}", input, input2, input3, map[input, input2, input3]);
			//}
			return map;
		}

	}
}