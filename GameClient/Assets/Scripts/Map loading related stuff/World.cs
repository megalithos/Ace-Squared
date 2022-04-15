using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Random = UnityEngine.Random;

public class World : MonoBehaviour
{
    public static World instance;

    public int seed;
    [HideInInspector]
    public Transform player;
    public Vector3 spawnPosition;
    public BlockType[] blocktypes;
    public Material material;
    public PhysicMaterial noFrictionMaterial;
    Chunk[,] chunks;
    public bool playerSpawned = false;
    public GameObject spawnableCube;

    List<ChunkCoord> activeChunks = new List<ChunkCoord>();
    ChunkCoord playerChunkCoord;
    ChunkCoord playerLastChunkCoord;
    public bool mapLoaded { get; private set; } = false;

    byte[] mapData;
    UInt32[] flattenedColors;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(this);
    }

    public void StartInitRoutine()
    {
        StartCoroutine(Init());
    }

    IEnumerator Init()
    {
        UIManager.instance.loadingText.text = "Loading map from file";
        Random.InitState(seed);

        mapData = BasicMapGenerator.Program.LoadVxlFile(Config.MAP_PATH, out flattenedColors);
        yield return 0;

        UIManager.instance.loadingText.text = "Constructing map";
        VoxelData.Init(mapData);
        chunks = new Chunk[VoxelData.xWidth, VoxelData.zWidth];

        spawnPosition = new Vector3(20, 20, 20);
        yield return StartCoroutine(GenerateWorld());

        yield return 0;

        UIManager.instance.loadingText.text = "Map loaded.";

        // inform the server about our class
        MapLoadingDone();
    }

    // gets called after client has loaded map and
    // all updated blocks
    public void SetupPingersAndUIEtc()
    {
        UIManager.instance.EnterInGameFromLoadingScreen();
        GameManager.instance.shouldStopCheckingForDeadConnection = false;
        GameManager.instance.lastTimeReceivedPingFromTheServer = GameManager.elapsedSeconds;
        StartCoroutine(GameManager.instance.CheckForDeadConnection());
        GameManager.instance.StartPinger();
    }

    public void MapLoadingDone()
    {
        // now the map loading is done so we can spawn ourselves:
        ClientSend.MapLoadingDone();
        mapLoaded = true;
    }

    private void Update()
    {
        if (player != null)
            playerChunkCoord = GetChunkCoordFromVector3(player.position);

        if (player != null && !playerChunkCoord.Equals(playerLastChunkCoord))
        {
            CheckViewDistance();
            playerLastChunkCoord = playerChunkCoord;
        }
    }

    public void ClearAllMapData()
    {
        DestroyChunkObjects();
        mapData = null;
        flattenedColors = null;
        mapLoaded = false;
        playerSpawned = false;
        chunks = null;
        activeChunks.Clear();
        Chunk.ResetCount();
        firstTimeLoadingUpdatedBlocks = true;

    }

    void DestroyChunkObjects()
    {
        for (int x = 0; x < VoxelData.xWidth; x++)
        {
            for (int z = 0; z < VoxelData.zWidth; z++)
            {
                Destroy(chunks[x, z].GetReferenceToThisGameObject());
                chunks[x, z] = null;
            }
        }
    }
    IEnumerator GenerateWorld()
    {
        float maxValue = (VoxelData.xWidth * VoxelData.xWidth) * 2;

        float count = 0;
        for (int x = 0; x < VoxelData.xWidth; x++)
        {
            for (int z = 0; z < VoxelData.zWidth; z++)
            {
                CreateNewChunk(x, z);
                count++;

                if (count % 200 == 0)
                {
                    UIManager.instance.loadingText.text = "Constructing world (" + Mathf.Round(count / maxValue * 100) + "%)";
                    yield return 0;
                }
            }
        }

        for (int x = 0; x < VoxelData.xWidth; x++)
        {
            for (int z = 0; z < VoxelData.zWidth; z++)
            {
                chunks[x, z].FinishInitialization();
                count++;

                if (count % 200 == 0)
                {
                    UIManager.instance.loadingText.text = "Constructing world (" + Mathf.Round(count / maxValue * 100) + "%)";
                    yield return 0;
                }
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
    public void ResetViewDistanceChunks()
    {

        ChunkCoord coord = GetChunkCoordFromVector3(player.position);
        for (int i = 0; i < VoxelData.xWidth; i++)
        {
            for (int j = 0; j < VoxelData.zWidth; j++)
            {
                if (chunks[i, j].isActive)
                    chunks[i, j].isActive = false;
            }
        }

        // Loop through all chunks currently within view distance of the player.
        for (int x = coord.x - System.Convert.ToInt32(Config.settings["chunk_view_distance"]); x < coord.x + System.Convert.ToInt32(Config.settings["chunk_view_distance"]) + 1; x++)
        {
            for (int z = coord.z - System.Convert.ToInt32(Config.settings["chunk_view_distance"]); z < coord.z + System.Convert.ToInt32(Config.settings["chunk_view_distance"]) + 1; z++)
            {

                // If the current chunk is in the world...
                if (IsChunkInWorld(new ChunkCoord(x, z)))
                {

                    // Check if it active, if not, activate it.
                    if (!chunks[x, z].isActive)
                    {
                        chunks[x, z].isActive = true;
                    }
                    activeChunks.Add(new ChunkCoord(x, z));
                }

            }

        }
        playerLastChunkCoord = coord;
        return;
    }

    void CheckViewDistance()
    {
        ChunkCoord coord = GetChunkCoordFromVector3(player.position);
        Vector2Int vecOfChunkMovement = new Vector2Int(System.Convert.ToInt32(-playerLastChunkCoord.x + coord.x), System.Convert.ToInt32(-playerLastChunkCoord.z + coord.z));
        Vector2Int otherVector = Vector2Int.zero;

        if (vecOfChunkMovement == Vector2Int.left || vecOfChunkMovement == Vector2Int.right)
            otherVector = Vector2Int.up;
        else if (vecOfChunkMovement == Vector2Int.up || vecOfChunkMovement == Vector2Int.down)
            otherVector = Vector2Int.right;

        if (otherVector == Vector2Int.zero)
        {
            playerLastChunkCoord = coord;
            return;
        }

        // cache, might have very small performance impact
        int viewDistance = System.Convert.ToInt32(Config.settings["chunk_view_distance"]);

        Debug.Log(vecOfChunkMovement);
        Vector2Int originVector = new Vector2Int(coord.x, coord.z) + vecOfChunkMovement * (viewDistance);
        Vector2Int otherOriginVector = new Vector2Int(coord.x, coord.z) - vecOfChunkMovement * (viewDistance + 1);

        for (int i = -viewDistance; i < viewDistance + 1; i++)
        {
            // enable any chunks that are 'in front of us'
            Vector2Int offsetVector = originVector + otherVector * i;

            if (IsChunkInWorld(new ChunkCoord(offsetVector.x, offsetVector.y)))
            {
                chunks[offsetVector.x, offsetVector.y].isActive = true;
                activeChunks.Add(new ChunkCoord(offsetVector.x, offsetVector.y));
            }

            // disable any chunks that are 'left behind'
            Vector2Int otherOffsetVector = otherOriginVector + otherVector * i;
            if (IsChunkInWorld(new ChunkCoord(otherOffsetVector.x, otherOffsetVector.y)))
            {
                chunks[otherOffsetVector.x, otherOffsetVector.y].isActive = false;

                ChunkCoord containable = new ChunkCoord(otherOffsetVector.x, otherOffsetVector.y);
                if (activeChunks.Contains(containable))
                    activeChunks.Remove(containable);
            }
        }
    }

    public bool CheckForVoxel(Vector3 pos)
    {
        ChunkCoord thisChunk = new ChunkCoord(pos);

        if (!IsChunkInWorld(thisChunk) || pos.y < 0 || pos.y > VoxelData.ChunkHeight)
            return false; // dont draw, the chunk we are checking is outside the world

        //Debug.Log(chunks[thisChunk.x, thisChunk.z].GetVoxelFromGlobalVector3(pos));
        return blocktypes[chunks[thisChunk.x, thisChunk.z].GetVoxelFromGlobalVector3(pos)].isSolid; // if it is solid dont draw.
    }

    public uint GetVoxelColorFromGlobalVector3(Vector3 pos)
    {
        ChunkCoord thisChunk = new ChunkCoord(pos);

        if (!IsChunkInWorld(thisChunk) || pos.y < 0 || pos.y > VoxelData.ChunkHeight)
            return 0; // dont draw, the chunk we are checking is outside the world

        return chunks[thisChunk.x, thisChunk.z].GetVoxelColorFromGlobalVector3(pos);
    }

    void CreateNewChunk(int x, int z)
    {

        chunks[x, z] = new Chunk(new ChunkCoord(x, z), this, mapData, flattenedColors);
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
    //bool IsVoxelInWorld(Vector3 pos)
    //{

    //	if (pos.x >= 0 && pos.x < VoxelData.WorldSizeInVoxels && pos.y >= 0 && pos.y < VoxelData.ChunkHeight && pos.z >= 0 && pos.z < VoxelData.WorldSizeInVoxels)
    //		return true;
    //	else
    //		return false;

    //}


    List<ClientHandle.UpdatedBlock> blocksToUpdate = new List<ClientHandle.UpdatedBlock>();
    List<ChunkCoord> chunksWeNeedToUpdate = new List<ChunkCoord>();
    Queue<Vector3> positionWhereWeNeedToCallUpdateSurroundingChunks = new Queue<Vector3>();

    void AddToChunksWeNeedToUpdateIfOnBorderOfTheChunk(Vector3 pos)
    {
        pos.x = Mathf.FloorToInt(pos.x);
        pos.z = Mathf.FloorToInt(pos.z);
        float comparisonX = pos.x % VoxelData.ChunkWidth;
        float comparisonZ = pos.z % VoxelData.ChunkWidth;

        if (comparisonX == VoxelData.ChunkWidth - 1)
        {
            // add right one
            AddIfDoesntExist(chunksWeNeedToUpdate, new Vector3(pos.x + 1, pos.y, pos.z));
        }
        else if (comparisonX == 0)
        {
            // add the left one
            AddIfDoesntExist(chunksWeNeedToUpdate, new Vector3(pos.x - 1, pos.y, pos.z));
        }

        if (comparisonZ == VoxelData.ChunkWidth - 1)
        {
            AddIfDoesntExist(chunksWeNeedToUpdate, new Vector3(pos.x, pos.y, pos.z + 1));
        }
        else if (comparisonZ == 0)
        {
            AddIfDoesntExist(chunksWeNeedToUpdate, new Vector3(pos.x, pos.y, pos.z - 1));
        }
    }

    void AddIfDoesntExist(List<ChunkCoord> toList, Vector3 pos)
    {
        if (!toList.Contains(GetChunkCoordFromVector3(pos)))
        {
            ChunkCoord chunkCoord = GetChunkCoordFromVector3(pos);

            // if it is in the whole map.
            if (chunkCoord.x >= 0 && chunkCoord.x < VoxelData.xWidth && chunkCoord.z >= 0 && chunkCoord.z < VoxelData.zWidth)
                toList.Add(chunkCoord);
        }
    }

    bool firstTimeLoadingUpdatedBlocks = true;

    // very bad programming practice but works
    public static int spawnedCubeCount = 0;

    public void StartUpdatingBlocksOnNewlyJoinedPlayer()
    {
        StartCoroutine(FinishUpdatingBlocksOnNewlyJoinedPlayer());
    }

    private IEnumerator FinishUpdatingBlocksOnNewlyJoinedPlayer()
    {
        float maxValue = blocksToUpdate.Count;
        float count = 0;

        foreach (ClientHandle.UpdatedBlock block in blocksToUpdate)
        {
            AddIfDoesntExist(chunksWeNeedToUpdate, block.position);

            // jos blockki on chunkin reunalla, lisää viereinen chunkki myös listaan 
            AddToChunksWeNeedToUpdateIfOnBorderOfTheChunk(block.position);

            if (!firstTimeLoadingUpdatedBlocks && block.solid == 0) // if we are breaking a block
            {
                byte[] colorBytes = BitConverter.GetBytes(GetVoxelColorFromGlobalVector3(block.position));

                //Debug.Log($"Spawned cubes rn: {spawnedCubeCount}, max decals; {Config.settings["max_decals"]}");
                if (spawnedCubeCount < Convert.ToInt32(Config.settings["max_decals"]))
                {
                    Color32 _color = new Color32(colorBytes[2], colorBytes[1], colorBytes[0], (byte)1);
                    GameObject spawnedCube = Instantiate(spawnableCube, block.position + new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity);
                    spawnedCube.GetComponent<MeshRenderer>().material.color = _color;
                    spawnedCubeCount++;
                }

            }
            positionWhereWeNeedToCallUpdateSurroundingChunks.Enqueue(GetChunkFromVector3(block.position).EditVoxelNoUpdate(block.position, block.color, block.solid));
            count++;
            if (count % 10 == 0)
            {
                UIManager.instance.loadingText.text = "Loading updated blocks (" + Mathf.Round(count / maxValue * 100) + "%)";
                yield return 0;
            }
        }

        UpdateOnlyChunksWeModified();

        if (firstTimeLoadingUpdatedBlocks)
        {
            ClientSend.LoadingUpdatedBlocksDone();
            SetupPingersAndUIEtc();
            Debug.Log("updated all blocks on newly joined player");
            firstTimeLoadingUpdatedBlocks = false;
        }

        // clear these so we don't have to render/set up extra voxels
        // when we do explosions
        blocksToUpdate.Clear();
        chunksWeNeedToUpdate.Clear();
        positionWhereWeNeedToCallUpdateSurroundingChunks.Clear();
    }

    public void UpdateAllChunks()
    {
        for (int x = 0; x < VoxelData.xWidth; x++)
        {
            for (int z = 0; z < VoxelData.zWidth; z++)
            {
                chunks[x, z].UpdateChunk();
            }
        }
    }

    public void UpdateOnlyChunksWeModified()
    {
        int count = 0;
        foreach (ChunkCoord chunkCoord in chunksWeNeedToUpdate)
        {
            Debug.Log("updating chunk number: " + ++count);

            chunks[chunkCoord.x, chunkCoord.z].UpdateChunk();
        }
    }

    void UpdateAllModifiedChunks()
    {
        // NOTE! assumes that everytime we assign a new coord to chunksWeNeedToUpdate, we also assign the corresponding position to positionWhereWeNeedToCallUpdateSurroundingChunks
        foreach (ChunkCoord coord in chunksWeNeedToUpdate)
        {
            chunks[coord.x, coord.z].UpdateChunk();
            Vector3 pos = positionWhereWeNeedToCallUpdateSurroundingChunks.Dequeue();
            chunks[coord.x, coord.z].UpdateSurroundingVoxels((int)pos.x, (int)pos.y, (int)pos.z);
        }
    }

    public void UpdateBlock(Vector3 pos, int color, byte blockID)
    {
        GetChunkFromVector3(pos).EditVoxel(pos, color, blockID);
    }

    public void AddUpdateBlocksToList(List<ClientHandle.UpdatedBlock> block)
    {
        blocksToUpdate.AddRange(block);
        Debug.Log("currently, blocksToUpdate.size = " + blocksToUpdate.Count);
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
        static UInt32[] flattenedColors = new UInt32[beginningBytes.Length * 4 + worldSizeInChunks * chunkWidthInVoxels * chunkWidthInVoxels * chunkHeightInVoxels];

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
                    flattenedChunk[count] = theseBytes[j];
                    flattenedColors[count] = theseBytes[j];
                    count++;
                }
            }
            UInt32[,,] colors3dArr = new UInt32[512, 512, 64];
            int[,,] map = vxlConverter.GetloadedVxlMap(path, out colors3dArr);

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
                                flattenedColors[count] = colors3dArr[y + chunkCoordX * chunkWidthInVoxels, x + chunkCoordZ * chunkWidthInVoxels, z];
                                count++;
                            }
                        }
                    }
                }
            }
        }

        public static byte[] LoadVxlFile(string name, out UInt32[] _flattenedColors)
        {

            GenerateChunkFromVxlFormat(name);
            _flattenedColors = flattenedColors;
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

        public static unsafe int[,,] GetloadedVxlMap(string path, out UInt32[,,] colors)
        {
            //map = File.ReadAllBytes("hallway.vxl");
            byte[] arr = new byte[512 * 512 * 64];

            arr = File.ReadAllBytes(path);

            fixed (byte* ptr = arr)
            {
                load_map(ptr, arr.Length);
            }

            colors = color;


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