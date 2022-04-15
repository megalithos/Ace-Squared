using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Text;
using UDP = Client.UDP;

/// <summary>
/// Broadcast the master-server every x seconds of our existence so
/// it can add us to the server list.
/// </summary>
public class BroadcastUDP : MonoBehaviour
{
	public static BroadcastUDP instance;
    int broadcastAfterMillis = 295000;
	public UdpClient udpClient;
    private static Thread t;
    public static bool stopThread = false;

	enum MasterserverPackets
	{
		BROADCAST = 0,
	}

	private void Awake()
	{
		if (instance == null)
			instance = this;
		else
			Destroy(this);
	}

	private void Start()
	{
        //StartCoroutine(Broadcaster());
        if (Config.BROADCAST_EXISTENCE_TO_MASTER)
        {
            t = new Thread(Broadcaster);
            t.Start();
        }
	}

	void Broadcaster()
	{
		while (!stopThread)
		{
			BroadcastExistence();

            Thread.Sleep(broadcastAfterMillis);
		}
	}

	/// <summary>
	/// start tcp endpoint, send query to server with appropriate server information
	/// close tcp connection
	/// </summary>
	void BroadcastExistence()
	{
		// Stuff that we need to send:
		// server_name
		// map_name
		// players_count
		//Debug.Log("Trying to broadcast our existence to the master_server...");

		try
		{
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram,
            ProtocolType.Udp);

            IPAddress serverAddr = IPAddress.Parse("178.79.162.109");

            IPEndPoint endPoint = new IPEndPoint(serverAddr, Config.MASTER_SERVER_PORT);

            string message = "Hello";
            //stream.BeginWrite(_packet.ToArray(), 0, _packet.Length(), null, null); // Send data to appropriate client
            /*using (Packet _packet = new Packet(2)) // 2 is id for broadcasting
            {
                _packet.Write(Config.SERVER_NAME);
                _packet.Write(Config.MAP);
                _packet.Write(Server.player_count);
                _packet.Write(Config.MAX_PLAYERS);
                _packet.Write(Config.MOTD);


                _packet.WriteLength();
                tcp.SendData(_packet);
            }*/

            Packet _packet = new Packet(0);
            _packet.Write(Config.SERVER_NAME);
			_packet.Write(Config.MAP);
			_packet.Write(Server.player_count);
			_packet.Write(Config.MAX_PLAYERS);
			_packet.Write(Config.MOTD);
            _packet.WriteLength();
            _packet.InsertInt(0);
            sock.SendTo(_packet.ToArray(), endPoint);
            //Debug.Log("sent msg: " + message);

        }
        catch (SocketException e)
		{
			Debug.Log("Master server refused connection. Seems like server is on but refusing the connection. Is the master server program running?");
			Debug.Log("Full details: " + e.Message);
		}
		catch (Exception e)
		{
			Debug.Log(e.Message);
		}
	}

    public byte[] addByteToArray(byte[] bArray, byte newByte)
    {
        byte[] newArray = new byte[bArray.Length + 1];
        bArray.CopyTo(newArray, 1);
        newArray[0] = newByte;
        return newArray;
    }

}
