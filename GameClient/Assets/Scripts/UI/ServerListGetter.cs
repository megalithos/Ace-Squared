using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TCP = Client.TCP;
using System.IO;
using System.Net;
using System.Net.Sockets;

/// <summary>
/// Gets the serverlist from the masterserver.
/// </summary>
public class ServerListGetter : MonoBehaviour
{
	public static ServerListGetter instance;
	[HideInInspector]
	public TCP tcp { get; private set; }
    private bool canRequestServerList = true;
    private int cooldownForRequestingServerList = 20; // seconds

	void Awake()
	{
		if (instance == null)
			instance = this;
		else
			Destroy(this);
	}

	/// <summary>
	/// Try getting the server list from the master server.
	/// </summary>
	public void TryGettingServerListFromTheMaster()
	{
        if (canRequestServerList)
        {
            canRequestServerList = false;
            DownloadServerList(Config.SERVERLIST_URL, Config.SERVERLIST_SAVE_PATH);
            StartCoroutine(CooldownRequestingServerList());
            Debug.Log("Requested server list");
        }
    }

    IEnumerator CooldownRequestingServerList()
    {
        yield return new WaitForSecondsRealtime(cooldownForRequestingServerList);
        canRequestServerList = true;
    }

    public static void DownloadServerList(string remoteFilename, string localFilename)
    {
        WebClient client = new WebClient();
        client.DownloadFile(remoteFilename, localFilename);

        ServerListManager.instance.LoadServerListFromFile();
    }
}
