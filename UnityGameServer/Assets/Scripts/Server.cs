using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using LagCompensation;

public class Server
{
	public static string bannedPlayersFilepath = UnityEngine.Application.streamingAssetsPath + "/banned_ip_list.txt";
	public static int MaxPlayers { get; private set; }
	public static int Port { get; private set; }
	public static Dictionary<int, Client> clients = new Dictionary<int, Client>();
	public delegate void PacketHandler(int _fromClient, Packet _packet);
	public static Dictionary<int, PacketHandler> packetHandlers;
	public static Vector3 spawnPosition = new Vector3(239.9905f, 20, 239.9905f);
	public static Vector3 deathPosition = new Vector3(1000, 1000, 1000);
	public static int player_count = 0;

	// store information about every block that has been modified in comparison to the world file.
	// reason to store the information is that when a new player joins we can send them the list
	public static HashSet<UpdatedBlock> updatedBlocks = new HashSet<UpdatedBlock>();

    private static TcpListener tcpListener;
    private static UdpClient udpListener;

	#region lag compensation fields
	static History history = new History();

	static Stopwatch tickWatch = new Stopwatch();
	public static int instantiated_player_count = 0;

	// get the time elapsed in millis since server's first tick
	public static int ElapsedMillis
	{
		get
		{
			return (int)tickWatch.ElapsedMilliseconds;
		}
	}

    public static int ElapsedSeconds
    {
        get
        {
            return (int)(tickWatch.ElapsedMilliseconds / 1000);
        }
    }

	public static int PresentTick
	{
		get
		{
			return (int)(tickWatch.ElapsedMilliseconds / Config.MS_PER_TICK);
		}
	}
	#endregion

	/// <summary>Starts the server.</summary>
	/// <param name="_maxPlayers">The maximum players that can be connected simultaneously.</param>
	/// <param name="_port">The port to start the server on.</param>
	public static void Start()
	{
		tickWatch.Start();

		MaxPlayers = Config.MAX_PLAYERS;
		Port = Config.PORT;

		Debug.Log("Starting server...");
		InitializeServerData();

		tcpListener = new TcpListener(IPAddress.Any, Port);
		tcpListener.Start();
		tcpListener.BeginAcceptTcpClient(TCPConnectCallback, null);

		udpListener = new UdpClient(Port);
		udpListener.BeginReceive(UDPReceiveCallback, null);

		Debug.Log($"Server started on port {Port}.");

		Client.OnClientConnectedEvent += IncrementPlayerCountOnClientConnected;
		Client.OnClientDisconnectedEvent += DecrementPlayerCountOnClientDisconnected;
        Client.OnClientDisconnectedEvent += RemoveFromClientsDictionary;
	}

    public static void RemoveFromClientsDictionary(int id)
    {
        
    }

	public static void IncrementPlayerCountOnClientConnected(int a)
	{
		player_count++;
		Debug.Log("Player joined. Player count= " + player_count);
	}
	public static void DecrementPlayerCountOnClientDisconnected(int a)
	{
		player_count--;
		Debug.Log("Player left. Player count = " + player_count);
	}

    /// <summary>Handles new TCP connections.</summary>
    private static void TCPConnectCallback(IAsyncResult _result)
    {
        TcpClient _client = tcpListener.EndAcceptTcpClient(_result);
        tcpListener.BeginAcceptTcpClient(TCPConnectCallback, null);

		// format the ip string by removing all whitespace and trimming the char (':') and the port number.
		string ipToCheckIfBanned = _client.Client.RemoteEndPoint.ToString().Replace(" ", String.Empty);
		int index = ipToCheckIfBanned.IndexOf(":");
		if (index > 0)
		{
			ipToCheckIfBanned = ipToCheckIfBanned.Substring(0, index);
		}

        Debug.Log($"Incoming connection from {ipToCheckIfBanned}");
		uint ipAsUINT = Config.ConvertFromIpAddressToInteger(ipToCheckIfBanned);
        
        // get client with smallest id
        int id = 0;
        for (int i = 0; i < clients.Count + 1; i++)
        {
            if (clients.ContainsKey(i))
            {
                continue;
            }
            id = i;
            break;
        }

        if (id > Config.MAX_PLAYERS)
        {
            Debug.Log("Server is full!");
            return;
        }

        ThreadManager.ExecuteOnMainThread(
            ()=>
            {
                clients.Add(id, new Client(id));
                Client outClient;
                clients.TryGetValue(id, out outClient);

                if (outClient == null)
                    throw new Exception("outClient is null!");


                outClient.ipAddress = ipAsUINT;
                outClient.tcp.Connect(_client);
                outClient.lastTimeReceivedPingFromTheClient = Server.ElapsedSeconds;
            }
            );
    }

    /// <summary>Receives incoming UDP data.</summary>
    private static void UDPReceiveCallback(IAsyncResult _result)
    {
        try
        {
            IPEndPoint _clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
            
            byte[] _data = udpListener.EndReceive(_result, ref _clientEndPoint);
            udpListener.BeginReceive(UDPReceiveCallback, null);

			if (_data.Length < 4)
            {
                return;
            }

            using (Packet _packet = new Packet(_data))
            {
                int _clientId = _packet.ReadInt();

                if (clients[_clientId].udp.endPoint == null)
                {
                    // If this is a new connection
                    clients[_clientId].udp.Connect(_clientEndPoint);
                    return;
                }

                if (clients[_clientId].udp.endPoint.ToString() == _clientEndPoint.ToString())
                {
                    // Ensures that the client is not being impersonated by another by sending a false clientID
                    clients[_clientId].udp.HandleData(_packet);
                }
            }
        }
        catch (Exception _ex)
        {
        }
    }

    /// <summary>Sends a packet to the specified endpoint via UDP.</summary>
    /// <param name="_clientEndPoint">The endpoint to send the packet to.</param>
    /// <param name="_packet">The packet to send.</param>
    public static void SendUDPData(IPEndPoint _clientEndPoint, Packet _packet)
    {
        try
        {
            if (_clientEndPoint != null)
            {
                udpListener.BeginSend(_packet.ToArray(), _packet.Length(), _clientEndPoint, null, null);
            }
        }
        catch (Exception _ex)
        {
        }
    }

    /// <summary>Initializes all necessary server data. -1 is id for master_server.</summary>
    private static void InitializeServerData()
    {
		packetHandlers = new Dictionary<int, PacketHandler>()
		{
			{ (int)ClientPackets.welcomeReceived, ServerHandle.WelcomeReceived },
          //  { (int)ClientPackets.playerMovement, ServerHandle.PlayerMovement },
            { (int)ClientPackets.playerShoot, ServerHandle.PlayerShoot },
			{ (int)ClientPackets.playerEditBlock, ServerHandle.PlayerEditBlock },
			{ (int)ClientPackets.inputState, ServerHandle.InputState },
			{ (int)ClientPackets.playerPosition, ServerHandle.PlayerPosition },
			{ (int)ClientPackets.playerSelectedItemChange, ServerHandle.PlayerSelectedItemChange },
			{ (int)ClientPackets.playerPhysicsShoot, ServerHandle.PlayerPhysicsShoot },
			{ (int)ClientPackets.playerChatMessage, ServerHandle.PlayerChatMessage },
			{ (int)ClientPackets.teamChangeRequest, ServerHandle.TeamChangeRequest },
			{ (int)ClientPackets.playerSelectedBlockColorChange, ServerHandle.PlayerSelectedBlockColorChange },
			{ (int)ClientPackets.playerAnimationTrigger, ServerHandle.PlayerAnimationTrigger },
			{ (int)ClientPackets.clientHasMap, ServerHandle.ClientHasMap },
			{ (int)ClientPackets.mapLoadingDone, ServerHandle.MapLoadingDone },
			{ (int)ClientPackets.loadedPlayerID, ServerHandle.LoadedPlayerID },
            { (int)ClientPackets.commandWithArgs, ServerHandle.CommandWithArgs },
            { (int)ClientPackets.selectedClassID, ServerHandle.SelectedClassID },
            { (int)ClientPackets.reloadGun, ServerHandle.ReloadGun },
            { (int)ClientPackets.pingMessage, ServerHandle.PingMessage },
            { (int)ClientPackets.pingMessageAckAck, ServerHandle.PingMessageAckAck },
            { (int)ClientPackets.loadingUpdatedBlocksDone, ServerHandle.LoadingUpdatedBlocksDone },
        };
        Debug.Log("Initialized packets."); 

	}

    public static void Stop()
    {
        tcpListener.Stop();
        udpListener.Close();
    }
    
    /// <summary>
    /// Bans a player by ID. Gets the client's ip address who is going to be banned,
    /// bans that client and adds to ban list (also disconnects from the server)
    /// </summary>
    /// <param name="id"></param>
    public static void BanPlayerByID(int id)
    {
        uint ip = 0;

        // loop through all clients (could be costly)
        foreach (Client client in Server.clients.Values)
        {
            // check if the id's match
            if (client.id == id)
            {
                ip = client.ipAddress;
                break;
            }
        }

        if (!Config.bannedPlayerIPS.Contains(ip))
        {
            Config.bannedPlayerIPS.Add(ip);
            Config.SaveIPAddressesToFile(Config.PATH_FOR_BANNED_PLAYER_IPS, Config.bannedPlayerIPS);

            // check if the player we just banned is connected
            // if he is then kick him
            foreach (Client client in Server.clients.Values)
            {
                if (client.mapLoadingDone && Config.bannedPlayerIPS.Contains(client.ipAddress))
                {
                    client.Disconnect(Config.server_msg.disconnect_reason_ban);
                }
            }
        }
    }

    // this struct is for storing information about updated blocks.
    // when a player edits an block, we need to know the block position (in global space), and the new id.
    public struct UpdatedBlock
	{
		public Vector3 position;
		public int color;
		public byte solid;

		public UpdatedBlock(Vector3 pos, int _color, byte _solid)
		{
			position = pos;
			color = _color;
			solid = _solid;
		}

		public bool Equals(UpdatedBlock other)
		{
			return position == other.position;
		}
	}
}