using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public static Dictionary<int, PlayerManager> players = new Dictionary<int, PlayerManager>();
    public static Player localPlayer;

    public delegate void OnPlayerKilled(int killerID, int weaponID, bool killedByHeadshot, int killedID);
    public delegate void OnPlayerChatReceived(string senderUsername, string message);
    public event OnPlayerKilled OnPlayerKilled_;
    public event OnPlayerChatReceived OnPlayerChatReceived_;
    public delegate void OnPlayerSpawned(string spawnPlayerUsername, int kills, int deaths, int id, int teamID);
    public event OnPlayerSpawned OnPlayerSpawnedEvent;
    public delegate void OnTeamChange(int changerID, int toTeam);
    public event OnTeamChange OnTeamChangeEvent;
    public delegate void OnPlayerDisconnected(int id);
    public event OnPlayerDisconnected OnPlayerDisconnectedEvent;
    public delegate void OnServerMessageReceived(string message);
    public event OnServerMessageReceived OnServerMessageReceivedEvent;

    public GameObject localPlayerPrefab;
    public GameObject playerPrefab;

    static Stopwatch stopwatch = new Stopwatch();
    [HideInInspector]
    public static int elapsedSeconds
    {
        get { return (System.Convert.ToInt32(stopwatch.ElapsedMilliseconds) / 1000); }
        private set { elapsedSeconds = value; }
    }
    [HideInInspector]
    public int lastTimeReceivedPingFromTheServer = 0;
    private int maximumKeepaliveTimeBeforeDisconnectingInSeconds = 10;
    private string defaultDisconnectReason = "Unknown.";
    [HideInInspector]
    public string disconnectReason;

    private void Awake()
    {
        ResetDisconnectReason();

        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Debug.Log("Instance already exists, destroying object!");
            Destroy(this);
        }
        stopwatch.Start();
    }

    public void ResetDisconnectReason()
    {
        disconnectReason = defaultDisconnectReason;
    }

    public void DestroySpawnedPlayers()
    {
        if (localPlayer != null && localPlayer.gameObject != null)
        {
            Destroy(localPlayer.gameObject);
            localPlayer = null;
        }
        foreach (KeyValuePair<int, PlayerManager> kvpitem in players)
        {
            Destroy(kvpitem.Value.gameObject);
        }
        players.Clear();
    }

    /// <summary>Spawns a player.</summary>
    /// <param name="_id">The player's ID.</param>
    /// <param name="_name">The player's name.</param>
    /// <param name="_position">The player's starting position.</param>
    /// <param name="_rotation">The player's starting rotation.</param>
    public void SpawnPlayer(int _id, string _username, Vector3 _position, Quaternion _rotation, int selectedItem, int teamID, int kills, int deaths)
    {
        GameObject _player;
        if (_id == Client.instance.myId) // if we are spawning the local player
        {
            _player = Instantiate(localPlayerPrefab, _position, _rotation);
            localPlayer = _player.GetComponent<Player>();
            World.instance.player = _player.GetComponent<Transform>();
            World.instance.ResetViewDistanceChunks();
            World.instance.playerSpawned = true;
        }
        else
        {
            _player = Instantiate(playerPrefab, _position, _rotation);
            //_player.GetComponent<ItemManager>().UpdateHandItemForNonLocalPlayer(selectedItem);
        }

        _player.GetComponent<PlayerManager>().Initialize(_id, _username, teamID);
        players.Add(_id, _player.GetComponent<PlayerManager>());
        OnPlayerSpawnedEvent(_username, kills, deaths, _id, teamID);
        Debug.Log("Loaded player number " + _id);
        ClientSend.LoadedPlayerID(_id);
    }

    public void KillfeedUpdate(byte shooterID, byte deathSource, bool headshot, byte killedPlayerID)
    {
        OnPlayerKilled_(shooterID, System.Convert.ToInt32(deathSource), headshot, killedPlayerID); // when calling this event, all who are subscribed to it will execute their functions
    }

    // when we receive player chat, invoke the event.
    public void PlayerChat(int senderID, byte[] msg)
    {
        string senderUsername = players[System.Convert.ToInt32(senderID)].username;
        string stringMessage = Encoder.DecodeByteArrayToString(msg);

        OnPlayerChatReceived_(senderUsername, stringMessage);
    }

    public void ChangeTeam(int changerID, int toTeam)
    {
        players[changerID].SetTeamID(toTeam);

        OnTeamChangeEvent(changerID, toTeam);
    }

    public void PlayerDisconnect(int id)
    {
        OnPlayerDisconnectedEvent(id);
    }

    public void OnServerMessage(string msg)
    {
        OnServerMessageReceivedEvent(msg);
    }

    public void PingMessageReceived()
    {
        Debug.Log("received ping from the server");
        lastTimeReceivedPingFromTheServer = elapsedSeconds;
    }

    public void DisconnectAndReturnBackToLobby()
    {
        if (UIManager.instance.state == UIManager.MenuState.server_browser)
            return;
        Client.instance.Disconnect();

        stopPinger = true;
        shouldStopCheckingForDeadConnection = true;
        Debug.Log("Disconnecting and returning back to lobby");

        // what we need to do here:
        // remove all players, disconnect tcp + udp
        // go back to menu
        // pop up alert
        DestroySpawnedPlayers();

        Cursor.lockState = CursorLockMode.None;
        if (ScoreboardManager.instance != null) ScoreboardManager.instance.ResetScoreBoard();
        Debug.Log("Disconnect reason: " + disconnectReason);

        UIManager.instance.ToggleEnabledDisconnectedPopup(true);
        UIManager.instance.ChangeDisconnectedReasonText(disconnectReason);
        UIManager.instance.SetState(UIManager.MenuState.server_browser);
        World.instance.ClearAllMapData();
    }

    public bool shouldStopCheckingForDeadConnection = false;
    public IEnumerator CheckForDeadConnection()
    {
        while (!shouldStopCheckingForDeadConnection)
        {
            Debug.Log($"isConnected={Client.isConnected}, diff={elapsedSeconds - lastTimeReceivedPingFromTheServer}");
            if (Client.isConnected && elapsedSeconds - lastTimeReceivedPingFromTheServer >= maximumKeepaliveTimeBeforeDisconnectingInSeconds)
            {
                Debug.Log("Seems like connection to the server is dead. Disconnecting.");
                //disconnectReason = "Connection is dead.";
                DisconnectAndReturnBackToLobby();
            }
            yield return new WaitForSeconds(1);
        }
        yield break;
    }

    public void StartPinger()
    {
        stopPinger = false;
        StartCoroutine(Pinger());
    }

    public bool stopPinger = false;
    IEnumerator Pinger()
    {
        while (!stopPinger)
        {
            yield return new WaitForSecondsRealtime(1f);
            ClientSend.PingMessage();
        }
    }
}