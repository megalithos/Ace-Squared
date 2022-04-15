using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class RemoveOldClients : MonoBehaviour
{
    public static RemoveOldClients instance;
    private const int maximumKeepaliveTimeBeforeDisconnectingInSeconds = 5;
    private const int maximumKeepClientObjectTimeIfMapNotLoaded = 60; // seconds

    public void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(this);
    }

    public void Start()
    {
        Debug.Log("Started disconnection check looper.");
        StartCoroutine(Looper());
    }

    IEnumerator Looper()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(1f);
            //Debug.Log("Severclients.count=" + Server.clients.Count);
            CheckAllClientsIfDisconnected();
        }
    }

    public void CheckAllClientsIfDisconnected()
    {
        List<int> keys = Server.clients.Keys.ToList<int>();

        foreach (int key in keys)
        {
            Client client = Server.clients[key];
            //Debug.Log("diff: " + (Server.ElapsedSeconds - client.lastTimeReceivedPingFromTheClient).ToString());
            if (client.mapLoadingDone && Server.ElapsedSeconds - client.lastTimeReceivedPingFromTheClient >= maximumKeepaliveTimeBeforeDisconnectingInSeconds)
            {
                Debug.Log("Connection to player is dead. Disconnecting player: " + client.id);
                client.Disconnect(Config.server_msg.disconnect_reason_unknown);
            }
            else if (!client.mapLoadingDone)
            {
                // disconnect the client if he has not loaded the map in less than x seconds
                if (Server.ElapsedSeconds - client.timeWhenConnectionEstablished >= maximumKeepClientObjectTimeIfMapNotLoaded)
                {
                    client.Disconnect(Config.server_msg.disconnect_reason_unknown);
                }
            }
        }
    }
}
