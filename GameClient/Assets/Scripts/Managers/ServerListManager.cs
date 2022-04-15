using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using Newtonsoft;
using Newtonsoft.Json;

public class ServerListManager : MonoBehaviour
{
	public static ServerListManager instance { get; private set; }
	public GameObject serverListItemToInstantiate;
	public GameObject parentToParentInstantiatedServerListItemsTo;

	List<GameObject> serverListItems = new List<GameObject>();
    public static List<SavedServer> serverList = new List<SavedServer>();
    int currentlySelectedServerID = -1; // -1 for unselected

	void Awake()
	{
		if (instance == null)
		{
			instance = this;
		}
		else
			Destroy(this);
	}

    private void Start()
    {
        ServerListGetter.instance.TryGettingServerListFromTheMaster();
    }

    public void LoadServerListFromFile()
    {
        // destroy the old gameobjects
        for (int i = 0; i < serverListItems.Count; i++)
        {
            Destroy(serverListItems[i]);
        }

        string data = File.ReadAllText(Config.SERVERLIST_SAVE_PATH);
        
        serverList.Clear(); // clear from previous records
        serverList = JsonConvert.DeserializeObject<List<SavedServer>>(data);
        
        serverListItems.Clear();
        Debug.Log("server list updated.");
        InstantiateNewServerListItems();
    }


    void InstantiateNewServerListItems()
	{
		int counter = 0;
		foreach (SavedServer savedServer in serverList)
		{
			GameObject instantiatedObj = Instantiate(serverListItemToInstantiate, parentToParentInstantiatedServerListItemsTo.GetComponent<Transform>());
			serverListItems.Add(instantiatedObj);
			ServerListItem item = instantiatedObj.GetComponent<ServerListItem>();
			item.mapText.text = savedServer.map_name;
			item.nameText.text = savedServer.server_name;
			item.pingText.text = savedServer.ping.ToString();
			item.ip = savedServer.ip;
			item.playersText.text = string.Format("{0}/{1}",savedServer.player_count.ToString(), savedServer.max_players.ToString());
			item.id += counter++;
		}
	}

	/// <summary>
	/// This method gets called when the user presses the connect button.
	/// Will connect us to the selected server.
	/// </summary>
	public void ConnectToSelectedServer()
	{
        if (UIManager.instance.usernameField.text.Contains("|"))
        {
            string usernameInputFieldText = UIManager.instance.usernameField.text;

            string _ip = null;
            int i = usernameInputFieldText.IndexOf("|");
            
            _ip = usernameInputFieldText.Substring(i + 1);
            
            Client.instance.ip = _ip;

            string userName = usernameInputFieldText.Substring(0, i);
            UIManager.instance.SetState(UIManager.MenuState.loading_screen);
            Client.instance.ConnectToServer(userName);
            Debug.Log("trying to connect to ip: " + _ip);
            
            return;
        }

		Debug.Log("Connecting to selected server...");
        string ip_addr = serverList[currentlySelectedServerID].ip;
        UIManager.instance.SetSelectedServer(serverList[currentlySelectedServerID]);
        ConnectToServer(ip_addr, serverList[currentlySelectedServerID]);
	}

    /// <summary>Attempts to connect to the server.</summary>
     void ConnectToServer(string ip_addr, SavedServer ss)
     {
		Debug.Log("trying to connect to ip: " + ip_addr);
		UIManager.instance.SetState(UIManager.MenuState.loading_screen);
        Client.instance.ip = ip_addr;
        Client.instance.ConnectToServer(UIManager.instance.usernameField.text);
    }
	
	public void SetNewSelected(int id)
	{
		// reset previously selected button color
		if (currentlySelectedServerID >= 0)
		{
			serverListItems[currentlySelectedServerID].GetComponent<ServerListItem>().ResetColor();
		}

		currentlySelectedServerID = id;

		serverListItems[currentlySelectedServerID].GetComponent<ServerListItem>().SetSelectColor();
	}
}
public class SavedServer
{
    public string server_name;
    public string map_name;
    public string ip;
    public string motd;
    public byte player_count;
    public byte max_players;
    public byte ping;

    public int add_time;

    public SavedServer(string _server_name, string _map_name, string _ip, byte _player_count, byte _ping, int _add_time, byte _max_players, string _motd)
    {
        server_name = _server_name;
        map_name = _map_name;
        ip = _ip;
        player_count = _player_count;
        ping = _ping;
        add_time = _add_time;
        max_players = _max_players;
        motd = _motd;
    }

    public override string ToString()
    {
        return server_name + ", " + map_name + ", " + player_count + ", " + ping + ", " + add_time + "," + max_players + ", " + motd;
    }
}