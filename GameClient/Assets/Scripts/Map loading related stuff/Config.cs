using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Security.Cryptography;
using System;
using Newtonsoft;
using Newtonsoft.Json;

public class Config : MonoBehaviour
{
	public static string MAP_FOLDER_PATH { get; private set; } = Application.streamingAssetsPath + "/maps/";
	public static string MAP_PATH { get; private set; }
	public static string MAP_NAME { get; private set; }
	[HideInInspector]
	public static string MASTER_SERVER_IP_ADDRESS { get; private set; } = "178.79.162.109";
	[HideInInspector]
	public static int MASTER_SERVER_PORT { get; private set; } = 8000;
	[HideInInspector]
	public static string PATH_FOR_USER_SETTINGS { get; private set; }
    [HideInInspector]
    public const string VERSION = "0.3";
    [HideInInspector]
    public const string DISCORD_URL = "http://discord.gg/Xv8XQh3qPg";
    [HideInInspector]
    public const string SERVERLIST_URL = "http://178.79.162.109/list.json";
    [HideInInspector]
    public static string SERVERLIST_SAVE_PATH { get; private set; } = Application.streamingAssetsPath + "/serverlist.json";
    [HideInInspector]
    public const string BUY_ME_A_COFFEE_URL = "https://www.buymeacoffee.com/acesquareddev";

    // this is the format of saving all user settings.
    // btw true = 1 and false = 1 of course.
    [HideInInspector]
	public static Dictionary<string, float> settings = new Dictionary<string, float>();
	[HideInInspector]
	public static Dictionary<string, float> defaultSettings = new Dictionary<string, float> {
		{"sensitivity", 800},
		{"ads_sensitivity", 800},
		{"scoped_sensitivity", 1},
		{"fullscreen_enabled", 0},
		{"fps_text_enabled", 0},
		{"debug_info_enabled", 0},
        {"max_decals", 0},
        {"chunk_view_distance", 15},
		//{"chunk_view_distance", },
		//{"block_decal_count",},
	};


	public static void HandleMapHashChecking(byte[] mapHashSentByServer, string mapName)
	{
		MAP_PATH = MAP_FOLDER_PATH + MAP_NAME + ".vxl";
		Debug.Log($"Map path:{MAP_PATH}");
        
		// if the method returns null it means that we didn't find the map
		if (CheckForMapHash(mapHashSentByServer) == null)
		{
			UIManager.instance.loadingText.text = "Requesting the map from the server...";
			// inform the server that we do not have the map
			ClientSend.ClientHasMap(false);
			Debug.Log($"Map {MAP_NAME} was not found in the maps directory. Requesting server for the map...");
		}
		else
		{
			UIManager.instance.loadingText.text = "Found map.";
			Debug.Log($"Found the map used by server... Map name is {MAP_NAME}");
			World.instance.StartInitRoutine();
		}
	}

	private void Awake()
	{
		PATH_FOR_USER_SETTINGS = Application.persistentDataPath + "/user_settings.json";
		LoadUserSettings();
    }

	/// <summary>
	/// Load the user settings from a json file. If the file does not exist or is corrupted (when it can't be deserialized)
	/// we will use default settings.
	/// </summary>
	public static void LoadUserSettings()
	{
		string path = Config.PATH_FOR_USER_SETTINGS;
		if (File.Exists(path))
		{
			string text = File.ReadAllText(path);

			settings = JsonConvert.DeserializeObject<Dictionary<string,float>>(text);
			if (settings == null)
				settings = defaultSettings;
		}
		else
		{
			settings = defaultSettings;
		}

		foreach (string s in settings.Keys)
		{
			Debug.Log(s);
		}
		Screen.fullScreen = Convert.ToBoolean(Config.settings["fullscreen_enabled"]);

		// now check that all they keys are same in default settings and also in the loaded
		foreach (string key in defaultSettings.Keys)
		{
			if (!settings.ContainsKey(key))
				settings.Add(key, defaultSettings[key]);
		}
	}

	/// <summary>
	/// Save user settings as .json
	/// </summary>
	public static void SaveUserSettings()
	{
		string path = Config.PATH_FOR_USER_SETTINGS;

		// delete the file if it exists
		if (File.Exists(path))
			File.Delete(path);

		string _settings = JsonConvert.SerializeObject(settings);
		File.WriteAllText(path, _settings);
	}

	/// <summary>
	/// Compare the hash sent by the server to all maps' (.vxl files) hashes.
	/// Simply put: check if we have the map or not.
	/// </summary>
	/// <param name="mapHashSentByServer">The hash sent by the server.</param>
	static string CheckForMapHash(byte[] mapHashSentByServer)
	{
		Debug.Log("Checking for map hashes...");
		FileInfo[] files = new DirectoryInfo(MAP_FOLDER_PATH).GetFiles();

		using (SHA256 sha = SHA256.Create())
		{
			foreach (FileInfo fInfo in files)
			{
				try
				{
					using (FileStream fStream = fInfo.Open(FileMode.Open))
					{
						byte[] hashValue = sha.ComputeHash(fStream);

						if (IsEqual(hashValue, mapHashSentByServer))
						{
							return fInfo.Name;
						}
					}
				}
				catch (Exception e)
				{
					Debug.LogError($"Something went wrong with checking map hashes... Error message: {e.Message}");
				}
			}
		}
		return null;
	}

	/// <summary>
	/// Return true, if two byte arrays match each other by elements.
	/// </summary>
	/// <param name="a"></param>
	/// <param name="b"></param>
	/// <returns></returns>
	static bool IsEqual(byte[] a, byte[] b)
	{
		int i;
		if (a.Length == b.Length)
		{
			i = 0;
			while (i < a.Length && (a[i]== b[i]))
			{
				i++;
			}
			if (i == a.Length)
			{
				return true;
			}
		}
		return false;
	}

	public static void SetMapName(string _name)
	{
		MAP_NAME = _name;
	}
}
