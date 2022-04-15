using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class Client
{
    public static int dataBufferSize = 4096;

	public delegate void ClientConnected(int id);
	public static event ClientConnected OnClientConnectedEvent;

	public delegate void ClientConnectedAndMapLoaded(int id);
	public static event ClientConnectedAndMapLoaded OnClientConnectedAndMapLoadedEvent;

	public delegate void ClientDisconnected(int id);
	public static event ClientDisconnected OnClientDisconnectedEvent;

    public int id;
    public Player player;
    public CurrentItemsData currentItemsData;
    public TCP tcp;
    public UDP udp;
	public string name;
	[HideInInspector]
	public bool mapLoadingDone = false;
	[HideInInspector]
	public int loadedPlayerCount = 0;
	[HideInInspector]
	public uint ipAddress = 0;
	[HideInInspector]
	public bool isAdmin = false;
    [HideInInspector]
    public bool godmodeEnabled = false; // not taking any damage with takedamage()
    [HideInInspector]
    public bool vanishEnabled = false;
    [HideInInspector]
    public int lastTimeReceivedPingFromTheClient = 0;
    [HideInInspector]
    public int maximumKeepaliveTimeBeforeDisconnectingInSeconds = 3;
    public int timeWhenConnectionEstablished;

    // for disconnecting players if they send too many packets
    private int bytesRead = 0; // this will be incremented everytime we read bytes
    private int resetBytesReadAfterSeconds = 10;
    private int maxBytes = 5000; // how many bytes as treshold to disconnect the player who sends too many packets
    private bool resetBytesAfterReading = true;

    public Client(int _clientId)
    {
        id = _clientId;
        tcp = new TCP(id, this);
        udp = new UDP(id, this);
        timeWhenConnectionEstablished = Server.ElapsedSeconds;
    }

    int maxBytesRead = 0;
    public void ResetBytesRead()
    {
        //Debug.Log("Bytes read total: " + bytesRead + ", max: " + maxBytesRead);
        if (bytesRead > maxBytesRead)
            maxBytesRead = bytesRead;

        if (bytesRead >= maxBytes)
        {
            Debug.Log("MAX BYTE EXCEEDED; DISCONNECTING PLAYER: " + id + ", he sent " + bytesRead);
            Disconnect(Config.server_msg.disconnect_reason_too_many_packets);
        }

        //Debug.Log("client has sent bytes: " + bytesRead);

        bytesRead = 0;
    }

    private void IncrementBytesReadAndCheckForTooManyPackets(int howManyBytesToIncrementBy)
    {
        bytesRead += howManyBytesToIncrementBy;
    }

	public void ResetNecessaryFields()
	{
		mapLoadingDone = false;
		loadedPlayerCount = 0;
		ipAddress = 0;
		isAdmin = false;
        godmodeEnabled = false;
        vanishEnabled = false;
        name = "";
        bytesRead = 0;
	}

    public class TCP
    {
        public TcpClient socket;

        private readonly int id;
        private NetworkStream stream;
        private Packet receivedData;
        private byte[] receiveBuffer;
		Client parent;

        public TCP(int _id, Client _client)
        {
            id = _id;
			parent = _client;
        }

        /// <summary>Initializes the newly connected client's TCP-related info.</summary>
        /// <param name="_socket">The TcpClient instance of the newly connected client.</param>
        public void Connect(TcpClient _socket, bool connect_master_server = false)
        {
            socket = _socket;
            socket.ReceiveBufferSize = dataBufferSize;
            socket.SendBufferSize = dataBufferSize;

            stream = socket.GetStream();

            receivedData = new Packet();
            receiveBuffer = new byte[dataBufferSize];

            stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);

			if (!connect_master_server)
			{
				OnClientConnectedEvent(id);
				Debug.Log($"Sending map and welcome packets to client {id}");

				// handle banning here. so basically we do have a connection for a little while
				if (Config.bannedPlayerIPS.Contains(parent.ipAddress))
				{
                    Debug.Log("Banning player: " + id);
					parent.Disconnect(Config.server_msg.disconnect_reason_ban);
				}

                // if ip of this client is specified as admin, let him know that! (and make admin)
                if (Config.adminIPS.Contains(parent.ipAddress))
                {
                    ServerSend.ServerMessage(id, Config.server_msg.you_are_now_admin);
                    parent.isAdmin = true;
                }

                ServerSend.Welcome(id, "Welcome to the server!");
				ServerSend.MapWeAreUsing(id);
				ServerSend.MapHashOfTheMapWeAreUsing(id, Config.MAP_HASH);
			}
			else // we are using this tcp object to connect to the master server
			{
			}
        }

        /// <summary>Sends data to the client via TCP.</summary>
        /// <param name="_packet">The packet to send.</param>
        public void SendData(Packet _packet)
        {
            try
            {
                if (socket != null)
                {
					stream.BeginWrite(_packet.ToArray(), 0, _packet.Length(), null, null); // Send data to appropriate client
                }
            }
            catch (Exception _ex)
            {
                Debug.Log($"Error sending data to player {id} via TCP: {_ex}");
                //parent.Disconnect();
            }
        }

        /// <summary>Reads incoming data from the stream.</summary>
        private void ReceiveCallback(IAsyncResult _result)
        {
            try
            {
                int _byteLength = stream.EndRead(_result);
                if (_byteLength <= 0)
                {
                    return;
                }

                byte[] _data = new byte[_byteLength];
                Array.Copy(receiveBuffer, _data, _byteLength);

                receivedData.Reset(HandleData(_data)); // Reset receivedData if all data was handled
                stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            }
            catch (Exception _ex)
            {
                Debug.LogWarning(_ex);
				//if (Server.clients.TryGetValue(id, out Client client))
				//	Server.clients[id].Disconnect();
            }
        }

        /// <summary>Prepares received data to be used by the appropriate packet handler methods.</summary>
        /// <param name="_data">The recieved data.</param>
        private bool HandleData(byte[] _data)
        {
            int _packetLength = 0;

            receivedData.SetBytes(_data);

            if (receivedData.UnreadLength() >= 4)
            {
                // If client's received data contains a packet
                _packetLength = receivedData.ReadInt();
                if (_packetLength <= 0)
                {
                    // If packet contains no data
                    return true; // Reset receivedData instance to allow it to be reused
                }
            }

            while (_packetLength > 0 && _packetLength <= receivedData.UnreadLength())
            {
                // While packet contains data AND packet data length doesn't exceed the length of the packet we're reading
                byte[] _packetBytes = receivedData.ReadBytes(_packetLength);
                parent.IncrementBytesReadAndCheckForTooManyPackets(_packetLength);
                ThreadManager.ExecuteOnMainThread(() =>
                {
                    using (Packet _packet = new Packet(_packetBytes))
                    {
                        int _packetId = _packet.ReadInt();
                        Server.packetHandlers[_packetId](id, _packet); // Call appropriate method to handle the packet
                    }
                });

                _packetLength = 0; // Reset packet length
                if (receivedData.UnreadLength() >= 4)
                {
                    // If client's received data contains another packet
                    _packetLength = receivedData.ReadInt();
                    if (_packetLength <= 0)
                    {
                        // If packet contains no data
                        return true; // Reset receivedData instance to allow it to be reused
                    }
                }
            }

            if (_packetLength <= 1)
            {
                return true; // Reset receivedData instance to allow it to be reused
            }

            return false;
        }

        /// <summary>Closes and cleans up the TCP connection.</summary>
        public void Disconnect()
        {
            socket.Close();
            stream = null;
            receivedData = null;
            receiveBuffer = null;
            socket = null;
        }
    }

    public class UDP
    {
        public IPEndPoint endPoint;
        private Client parent;
        private int id;

        public UDP(int _id, Client _parent)
        {
            id = _id;
            parent = _parent;
        }

        /// <summary>Initializes the newly connected client's UDP-related info.</summary>
        /// <param name="_endPoint">The IPEndPoint instance of the newly connected client.</param>
        public void Connect(IPEndPoint _endPoint)
        {
            endPoint = _endPoint;
            Debug.Log("udp connection established");
        }

        /// <summary>Sends data to the client via UDP.</summary>
        /// <param name="_packet">The packet to send.</param>
        public void SendData(Packet _packet)
        {
            Server.SendUDPData(endPoint, _packet);
        }

        /// <summary>Prepares received data to be used by the appropriate packet handler methods.</summary>
        /// <param name="_packetData">The packet containing the recieved data.</param>
        public void HandleData(Packet _packetData)
        {
            int _packetLength = _packetData.ReadInt();
            byte[] _packetBytes = _packetData.ReadBytes(_packetLength);
            parent.IncrementBytesReadAndCheckForTooManyPackets(_packetLength);
            ThreadManager.ExecuteOnMainThread(() =>
            {
                using (Packet _packet = new Packet(_packetBytes))
                {
                    try
                    {
                        int _packetId = _packet.ReadInt();
                        //Debug.Log("Handling packet id:" + _packetId + " for client: " + id);
                        Server.packetHandlers[_packetId](id, _packet); // Call appropriate method to handle the packet
                    }
                    catch (Exception)
                    {
                        Debug.Log("Invalid packet (udp), disconnecting: "+ id);
                        parent.Disconnect(Config.server_msg.disconnect_reason_unknown);
                    }
                }
            });
        }

        /// <summary>Cleans up the UDP connection.</summary>
        public void Disconnect()
        {
            endPoint = null;
            //parent = null;
        }
    }

	/// <summary>Sends the client into the game and informs other clients of the new player.</summary>
	/// <param name="_playerName">The username of the new player.</param>
	public void SendIntoGame(string _playerName)
	{
		Debug.Log($"Sending player {id} to all players for spawning...");

		Server.clients[id].player = NetworkManager.instance.InstantiatePlayer();
		Server.clients[id].player.Initialize(id, Server.clients[id].name, this);

		// Send all players to the new player here so we don't get bugs about ammosimulation/itemmanager not working on client side
        // EXCEPT, DONT SEND IF HE IS VANISHED 
		foreach (Client _client in Server.clients.Values)
		{
			if (_client.player != null)
			{
				if (_client.id != id && !_client.vanishEnabled)
				{
					ServerSend.SpawnPlayer(id, _client.player, _client.player.selectedItem);
				}
			}
		}

		// Send the new player to all players (including himself)
		foreach (Client _client in Server.clients.Values)
		{
			if (_client.player != null)
			{
				ServerSend.SpawnPlayer(_client.player.id, this.player);
			}
		}
    }

	// once the player has loaded all of the players, finish
	public void FinishSendingIntoGame()
	{
		Debug.Log("Finishing sending into game for: " + id);
		
		foreach (Client _client in Server.clients.Values)
		{
			if (_client.player != null && _client.player.selectedItem >= 0)
				ServerSend.SendPlayerSelectedItemChangeToSingle(_client.player.selectedItem, id, _client.id);
		}

		ServerSend.PlayerTeamChangeAcknowledgement(id, 0); // set to team 0 as default (for now)

		// send chat message that this player joined
		ServerSend.ServerMessageToAll(Config.server_msg.usr_connected, this.name);

		// client connected to the server -> raise event
		OnClientConnectedAndMapLoadedEvent(id);
		mapLoadingDone = true;
		
        Server.clients[id].currentItemsData = Server.clients[id].player.GetComponent<CurrentItemsData>();
        Server.clients[id].lastTimeReceivedPingFromTheClient = Server.ElapsedSeconds;
    }

    /// <summary>Disconnects the client and stops all network traffic. (Also destroys player's client object and resets necessary fields.)</summary>
    public void Disconnect(Config.server_msg reasonOfDisconnect)
    {
		// if it is normal client
		if (id != -1)
		{
            // try because the client might have closed socket connections
            try { ServerSend.DisconnectReason(id, reasonOfDisconnect); } catch (Exception) { }
            
			Debug.Log("Disconnecting player with reason id: " + reasonOfDisconnect);


			Server.instantiated_player_count--;
			if (player != null)
			{
				UnityEngine.Object.Destroy(player.gameObject);
				player = null;
			}

			tcp.Disconnect();
			udp.Disconnect();
			ServerSend.PlayerDisconnected(id);
            ServerSend.ServerMessageToAll(Config.server_msg.usr_disconnected, name);
            Server.clients.Remove(this.id);
            OnClientDisconnectedEvent(this.id);
        }
		else
		{
			tcp.Disconnect();
			udp.Disconnect();
		}
    }
}
