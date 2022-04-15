using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager instance;

    public GameObject playerPrefab;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Debug.Log("Instance already exists, destroying object!");
            Destroy(this);
        }
    }

    private void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        Server.Start();
    }

    private void OnApplicationQuit()
    {
        Server.Stop();
        CommandParser.stopThread = true; // flag the thread to get disabled
        BroadcastUDP.stopThread = true;
        ServerSend.ServerMessageToAll(Config.server_msg.disconnect_reason_shutdown);
		Config.serverConsoleProcess.Kill();
    }

    public Player InstantiatePlayer()
    {
		Server.instantiated_player_count++;
        return Instantiate(playerPrefab, Config.spawnAreas[0].GetRandomSpawnPosition(), Quaternion.identity).GetComponent<Player>();
    }
}
