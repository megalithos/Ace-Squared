using System.Net;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Random = UnityEngine.Random;
using IniParser;
using IniParser.Model;
using System.Security.Cryptography;
using System.IO;
using System.Linq;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Runtime.InteropServices;
using Newtonsoft;
using Newtonsoft.Json;

public class Config
{
	public static int TICKS_PER_SEC { get; private set; } // How many ticks per second
	public static float MS_PER_TICK { get; private set; } // How many milliseconds per tick
	[HideInInspector]
	public static int MAX_PLAYERS { get; private set; }
	[HideInInspector]
	public static int PORT { get; private set; } = 26950;
	[HideInInspector]
	public static int PORT2 { get; private set; } = 26951;
	[HideInInspector]
	public static string MASTER_SERVER_IP_ADDRESS { get; private set; } = "178.79.162.109";
	[HideInInspector]
	public static int MASTER_SERVER_PORT { get; private set; } = 8000;
	[HideInInspector]
	public static string MAP { get; private set; }
	[HideInInspector]
	public static string MAP_FILEPATH { get; private set; }
    [HideInInspector]
    public static string MAP_CFG_PATH { get; private set; }
	[HideInInspector]
	public static byte[] MAP_HASH { get; private set; }
	[HideInInspector]
	public static bool ENABLE_FALLING_BLOCKS { get; private set; }
	[HideInInspector]
	public static string SERVER_NAME { get; private set; }
	[HideInInspector]
	public static string MOTD { get; private set; } // MessageOfTheDay
	[HideInInspector]
	public static string CONSOLE_COMMUNICATION_KEY { get; private set; }
    [HideInInspector]
    public static int MAX_BYTES_IN_1_SECOND_BEFORE_DISCONNECTING { get; private set; } = 5000;
    [HideInInspector]
    public static bool ANTICHEAT_ENABLED { get; private set; }
    public static byte startingClassID = 2;
    [HideInInspector]
    public static bool BROADCAST_EXISTENCE_TO_MASTER = false;
    [HideInInspector]
    public static bool KICK_FOR_TOO_MANY_PACKETS;

    public static List<uint> bannedPlayerIPS = new List<uint>(); // hold banned player ips as uint
	public static List<uint> adminIPS = new List<uint>(); // ip addresses that are admins

	[HideInInspector]
	public static string PATH_FOR_BANNED_PLAYER_IPS { get; private set; } = Application.streamingAssetsPath + "/banned_players.txt";
	[HideInInspector]
	public static string PATH_FOR_ADMIN_LIST { get; private set; } = Application.streamingAssetsPath + "/admin_list.txt";

	public enum ValidTeams
	{
		green = 0,
		blue,
		COUNT // <- count of total number of different teams
	}


	/// <summary>
	/// For selecting default block color 
	/// </summary>
	public static Dictionary<ValidTeams, int> teamDefaultColors = new Dictionary<ValidTeams, int>()
	{
		{ValidTeams.green, 6487964},
		{ValidTeams.blue, 4882943},
	};
    public static Dictionary<Player.ValidItems, ItemCfg> defaultItemSettings = new Dictionary<Player.ValidItems, ItemCfg>();
	public static Dictionary<int, SpawnArea> spawnAreas;

	[HideInInspector]
	public static int waterDamage;
	public static System.Diagnostics.Process serverConsoleProcess;
	public static Thread commandHandlerThread;
    [HideInInspector]
    public const string VERSION = "0.3";

    public static void Init()
	{
		CommandParser.Init();
        InitItemSettings();
		LoadIPAddressesToList(PATH_FOR_BANNED_PLAYER_IPS, bannedPlayerIPS);
        LoadIPAddressesToList(PATH_FOR_ADMIN_LIST, adminIPS);

		//serverConsoleProcess = new System.Diagnostics.Process();
		//CONSOLE_COMMUNICATION_KEY = RandomString(16);
		//serverConsoleProcess.StartInfo.Arguments = CONSOLE_COMMUNICATION_KEY;
		//serverConsoleProcess.StartInfo.FileName = Application.streamingAssetsPath+"/ServerConsole.exe";
		//serverConsoleProcess.Start();
		//commandHandlerThread = new Thread(CommandParser.CommandHandler);
		//commandHandlerThread.Start();

		LoadServerSettingsFromIniFile();

		TICKS_PER_SEC = System.Convert.ToInt32(Mathf.Round(1f / Time.fixedDeltaTime));
		MS_PER_TICK = 1000f / TICKS_PER_SEC;
		Debug.LogFormat("Initialized server, which tick rate is" + TICKS_PER_SEC);
		ThrowWarningIfTickRateIsNotStandard();

		if (teamDefaultColors.Count != (int)ValidTeams.COUNT)
			Debug.LogError("Error!! You should have a default color for team there is..");
	}

	static void LoadIPAddressesToList(string path, List<uint> lst)
	{
		if (!File.Exists(path))
		{
			return;
		}
		
		using (System.IO.StreamReader file = new System.IO.StreamReader(new System.IO.FileStream(path, System.IO.FileMode.Open)))
		{
			string ln;
			while ((ln = file.ReadLine()) != null)
			{
				try
				{
					lst.Add(ConvertFromIpAddressToInteger(ln));
				}
				catch (Exception) { }
			}
			file.Close();
		}
	}

	// will delete the previous file
	// and create new one with all of the currently banned players
	public static void SaveIPAddressesToFile(string path, List<uint> lst)
	{
		if (File.Exists(path))
			File.Delete(path);
		FileStream f = File.Create(path);
		f.Close();

		/*string lastLine = File.ReadAllLines(PATH_FOR_BANNED_PLAYER_IPS).LastOrDefault();
		char lastchar = 'a';
		if (lastLine != null) lastchar = lastLine.LastOrDefault();

		using (StreamWriter sw = File.AppendText(PATH_FOR_BANNED_PLAYER_IPS))
		{
			string prefix = "";
			if (!(lastchar == '\n'))
				prefix = "\n";

			sw.WriteLine(prefix + _ip);
		}*/

		using (StreamWriter sw = new StreamWriter(path))
		{
			foreach (uint i in lst)
			{
				sw.WriteLine(ConvertFromIntegerToIpAddress(i));
			}
		}
	}

	static void LoadServerSettingsFromIniFile()
	{
		// for parsing server_settings.ini, and setting certain values here to those.
		FileIniDataParser parser = new FileIniDataParser();
		IniData iniData = parser.ReadFile(Application.streamingAssetsPath + "/server_settings.ini");

		MAX_PLAYERS = System.Convert.ToInt32(iniData["main"]["max_players"]);
		waterDamage = System.Convert.ToInt32(iniData["main"]["water_damage"]);
		MAP = iniData["main"]["map"];
		MAP_FILEPATH = Application.streamingAssetsPath + "/maps/" + MAP + ".vxl";
		MAP_HASH = GetHashSha256(MAP_FILEPATH);
		ENABLE_FALLING_BLOCKS = System.Convert.ToBoolean(iniData["MAIN"]["enable_falling_blocks"]);
		SERVER_NAME = System.Convert.ToString(iniData["main"]["server_name"]);
		MOTD = System.Convert.ToString(iniData["main"]["motd"]);
        ANTICHEAT_ENABLED = Convert.ToBoolean(iniData["main"]["gandalf_anticheat_enabled"]);
        BROADCAST_EXISTENCE_TO_MASTER = Convert.ToBoolean(iniData["main"]["broadcast_to_master"]);
        KICK_FOR_TOO_MANY_PACKETS = Convert.ToBoolean(iniData["main"]["kick_for_too_many_packets"]);
        Debug.Log("max_players: " + MAX_PLAYERS);
		Debug.Log("port: " + PORT);
		Debug.Log("port2: " + PORT2);
		Debug.Log("water_damage:" + waterDamage);
		Debug.Log("selected map: " + MAP);
		Debug.Log("selected map path: " + MAP_FILEPATH);
		Debug.Log("Enable_fallingblocks: " + ENABLE_FALLING_BLOCKS);
		Debug.Log("Server_name: " + SERVER_NAME);
		Debug.Log("MOTD: " + MOTD);
        Debug.Log("GandalfAC enabled : " + ANTICHEAT_ENABLED);
        Debug.Log("Broadcast existence to master: " + BROADCAST_EXISTENCE_TO_MASTER);
        Debug.Log("KICK_FOR_TOO_MANY_PACKETS: " + KICK_FOR_TOO_MANY_PACKETS);
        MAP_CFG_PATH = Application.streamingAssetsPath + "/maps/" + MAP + ".json";
        LoadMapCFG();
    }

    static void LoadMapCFG()
    {
        spawnAreas = new Dictionary<int, SpawnArea>();
        if (File.Exists(MAP_CFG_PATH))
        {
            try
            {
                string text = File.ReadAllText(MAP_CFG_PATH);

                List<Vector3> vectorList = JsonConvert.DeserializeObject<List<Vector3>>(text);

                // check that we have twice the amount of points than teams
                if (vectorList.Count == (int)ValidTeams.COUNT * 2)
                {
                    if ((int)ValidTeams.COUNT > 2)
                    {
                        throw new Exception("setup error");
                    }

                    SpawnArea team1Area = new SpawnArea(vectorList[0], vectorList[1]);
                    SpawnArea team2Area = new SpawnArea(vectorList[2], vectorList[3]);

                    spawnAreas.Add((int)ValidTeams.green, team1Area);
                    spawnAreas.Add((int)ValidTeams.blue, team2Area);
                    return;
                }
            
            }
            catch (Exception)
            {

            }
        }
        // if it did not work, this code will be ran
        SpawnArea team1Area2 = new SpawnArea(Vector3.zero, Vector3.zero);
        SpawnArea team2Area2 = new SpawnArea(Vector3.zero, Vector3.zero);

        spawnAreas.Add((int)ValidTeams.green, team1Area2);
        spawnAreas.Add((int)ValidTeams.blue, team2Area2);
        SaveMapCFG();
    }

    static void SaveMapCFG()
    {
        // delete if it exists
        if (File.Exists(MAP_CFG_PATH))
            File.Delete(MAP_CFG_PATH);

        List<Vector3> lst = new List<Vector3>();
        List<Vector3> lst2 = spawnAreas[0].GetVectors();

        lst.Add(lst2[0]);
        lst.Add(lst2[1]);

        List<Vector3> lst3 = spawnAreas[1].GetVectors();
        lst.Add(lst3[0]);
        lst.Add(lst3[1]);

        File.WriteAllText(MAP_CFG_PATH, JsonConvert.SerializeObject(lst,Formatting.None,
                        new JsonSerializerSettings()
                        {
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                        }));
    }


    // hash a file specified by filename, and return the byte array representing the file.
    static byte[] GetHashSha256(string filename)
	{
		using (SHA256 sha = SHA256.Create())
		{
			using (FileStream fStream = new FileStream(filename, FileMode.Open))
			{
				byte[] hashValue = sha.ComputeHash(fStream);

				return hashValue;
			}
		}
	}

	// for each valid team have a spawn area, defined by 2 vectors.
	// calculate randomly spawn point in this area
	public class SpawnArea
	{
		// specify where the spawn area starts and ends (it is like a box)
		Vector3 start;
		Vector3 end;

		public SpawnArea(Vector3 _start, Vector3 _end)
		{
			start = _start; end = _end;
		}

		public Vector3 GetRandomSpawnPosition()
		{
			float x = Random.Range(start.x, end.x);
			float z = Random.Range(start.z, end.z);

			// get the y coordinate from raycasting
			RaycastHit _hit;

			if (Physics.Raycast(new Vector3(x, VoxelData.ChunkHeight * 2, z), Vector3.down, out _hit, Mathf.Infinity))
			{
				float y = _hit.point.y + 2;
				return new Vector3(x, y, z);
			}
			else // this code runs when the raycast does not hit anything
			{
				throw new System.Exception("Couldn't calculate spawn point.");
			}
		}

        public List<Vector3> GetVectors()
        {
            return new List<Vector3> { start, end };
        }
	}

	// Debug.LogWarning if the tickrate is not a standard tickrate.
	// for "standard" tickrates see the function's switch statement.
	// The tickrate can become non-standard, if the rounding is done incorrectly.
	static void ThrowWarningIfTickRateIsNotStandard()
	{
		switch (TICKS_PER_SEC)
		{
			case 30:
				break;
			case 60:
				break;
			case 128:
				break;
			case 144:
				break;
			default:
				Debug.LogWarning("Tickrate is some non-standard value. Be careful and preferably use 30, 60, 128 or 144 as tickrate.");
				break;
		}
	}

    /// <summary>
    /// Message ids of those messages that will be shown to users while they are in game.
    /// </summary>
    public enum server_msg
    {
        invalid_team = 0,
        usr_connected, // msg + user name
        usr_disconnected, // msg + user name
        usr_banned, // msg + user name
        usr_kicked_for_flyhack,
        disconnect_reason_kick, // msg
        disconnect_reason_ban, // msg
        disconnect_reason_serverfull, // msg
        disconnect_reason_not_whitelisted, // msg
        disconnect_reason_shutdown, // msg
        disconnect_reason_unknown,
        you_are_now_admin, // msg
        you_are_not_admin_anymore, // msg
        god_mode_enabled, // msg
        god_mode_disabled, // msg
        your_ping_is,
        custom,
        disconnect_reason_too_many_packets,
    }

    public static Dictionary<server_msg, string> serverMessageStrings = new Dictionary<server_msg, string>
    {
        {server_msg.usr_connected, "{0} connected."},
        {server_msg.usr_disconnected, "{0} disconnected." },
        {server_msg.usr_banned, "{0} has been banished by Gandalf." },
        {server_msg.disconnect_reason_kick, "You have been kicked." },
        {server_msg.disconnect_reason_ban, "You have been banned." },
        {server_msg.disconnect_reason_serverfull, "Server is full." },
        {server_msg.disconnect_reason_shutdown, "The server has been shut down." },
        {server_msg.disconnect_reason_unknown, "Unknown." },
        {server_msg.usr_kicked_for_flyhack, "{0} has been kicked for flyhacking."},
        {server_msg.you_are_now_admin, "You are now an admin." },
        {server_msg.you_are_not_admin_anymore, "You are not admin anymore." },
        {server_msg.god_mode_enabled, "Godmode enabled." },
        {server_msg.god_mode_disabled, "Godmode disabled." },
        {server_msg.your_ping_is, "Your ping is {0}" },
        {server_msg.custom, "{0}" },
        {server_msg.disconnect_reason_too_many_packets, "Too many packets." },
    };

    private static System.Random random = new System.Random();
	public static string RandomString(int length)
	{
		const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
		return new string(Enumerable.Repeat(chars, length)
		  .Select(s => s[random.Next(s.Length)]).ToArray());
	}

	public static uint ConvertFromIpAddressToInteger(string ipAddress)
	{
		var address = IPAddress.Parse(ipAddress);
		byte[] bytes = address.GetAddressBytes();

		// flip big-endian(network order) to little-endian
		if (BitConverter.IsLittleEndian)
		{
			Array.Reverse(bytes);
		}

		return BitConverter.ToUInt32(bytes, 0);
	}

	public static string ConvertFromIntegerToIpAddress(uint ipAddress)
	{
		byte[] bytes = BitConverter.GetBytes(ipAddress);

		// flip little-endian to big-endian(network order)
		if (BitConverter.IsLittleEndian)
		{
			Array.Reverse(bytes);
		}

		return new IPAddress(bytes).ToString();
	}

    public class ItemCfg : ICloneable
    {
        public  int bulletsInMag;
        public  int magsLeft;
        public  float fireRate;
        public  float reloadTimeInSeconds;
        public int bulletsLeft;

        public ItemCfg(int _bulletsInMag, int _magsLeft, float _fireRate, float _reloadTimeInSeconds)
        {
            bulletsInMag = _bulletsInMag;
            magsLeft = _magsLeft;
            fireRate = _fireRate;
            reloadTimeInSeconds = _reloadTimeInSeconds;
            bulletsLeft = _bulletsInMag;
        }
        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }

    /// <summary>
    /// Sets up the default item settings
    /// </summary>
    static void InitItemSettings()
    {
        defaultItemSettings.Add(Player.ValidItems.c4, new ItemCfg(1,1,0, 2.5f));
        defaultItemSettings.Add(Player.ValidItems.doubleBarrel, new ItemCfg(2, 6, 10, 2.5f));
        defaultItemSettings.Add(Player.ValidItems.m249, new ItemCfg(150, 2, 10, 5f));
        defaultItemSettings.Add(Player.ValidItems.mgl, new ItemCfg(6, 1, 10, 4f));
        defaultItemSettings.Add(Player.ValidItems.mp5_smg, new ItemCfg(30,3,10,4));
        defaultItemSettings.Add(Player.ValidItems.rocketLauncher, new ItemCfg(1,4,1,2.5f));
        defaultItemSettings.Add(Player.ValidItems.sniperRifle, new ItemCfg(1,10,10,2.5f));
        defaultItemSettings.Add(Player.ValidItems.spade, new ItemCfg(0, 0, 0, 0)); // for fixing bug
        defaultItemSettings.Add(Player.ValidItems.bluePrint, new ItemCfg(0, 0, 0, 0)); // for fixing bug
    }
}