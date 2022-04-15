using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class Client : MonoBehaviour
{
    public static Client instance;
    public static int dataBufferSize = 4096;

    [HideInInspector]
    public string ip = "";
    [HideInInspector]
    public int port = 26950;
    [HideInInspector]
    public int myId = 0;
    public TCP tcp;
    public UDP udp;
    [HideInInspector]
    public string username = "";
    public int connectTime = 5; // time in seconds to try and connect to server. if not connected to the server during this period of time, abort the socket etc.
    public bool isAdmin { get; set; } = false;
    public int firstClassID;

    public static bool isConnected { get; protected set; } = false;
    private delegate void PacketHandler(Packet _packet);
    private static Dictionary<int, PacketHandler> packetHandlers;

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

    }

    public string GetIP()
    {
        return ip;
    }

    private void OnApplicationQuit()
    {
        Disconnect(); // Disconnect when the game is closed
    }

    /// <summary>Attempts to connect to the server.</summary> ClientSend.PingMessage();
    public void ConnectToServer(string name)
    {
        GameManager.instance.ResetDisconnectReason();

        InitializeClientData();

        username = name;

        tcp = new TCP(ip, port); // <- this ip will tell which ip we try to connect to
        udp = new UDP();
        UIManager.instance.loadingText.text = "Connecting to the server...";
        tcp.Connect(); // Connect tcp, udp gets connected once tcp is done
        ChatManager.instance.Clear();
        // start timer
        StartCoroutine(CheckIfReceivedCallbackFromGameServer());
    }

    // wait for x seconds and return back to the main menu if not received callback from the game server
    IEnumerator CheckIfReceivedCallbackFromGameServer()
    {
        yield return new WaitForSecondsRealtime(10f);
        if (!isConnected && UIManager.instance.state == UIManager.MenuState.loading_screen)
        {
            GameManager.instance.DisconnectAndReturnBackToLobby();
        }
    }

    public class TCP
    {
        public TcpClient socket;

        private NetworkStream stream;
        private Packet receivedData;
        private byte[] receiveBuffer;
        private string ip;
        private int port;
        private bool masterServerConnection;
        [HideInInspector]
        public bool isConnectedToMaster { get; private set; } = false;

        // constructor
        public TCP(string _ip, int _port, bool _masterServerConnection = false)
        {
            ip = _ip;
            port = _port;
            masterServerConnection = _masterServerConnection;

            Debug.Log($"Trying to establish  server connection... {ip}:{port}");
        }

        /// <summary>Attempts to connect to the server via TCP.</summary>
        public void Connect()
        {
            socket = new TcpClient
            {
                ReceiveBufferSize = dataBufferSize,
                SendBufferSize = dataBufferSize
            };

            receiveBuffer = new byte[dataBufferSize];
            socket.BeginConnect(ip, port, ConnectCallback, socket);
        }

        /// <summary>Initializes the newly connected client's TCP-related info.</summary>
        private void ConnectCallback(IAsyncResult _result)
        {
            if (!masterServerConnection)
            {
                isConnected = true;
                Debug.Log($"Connected to the game server {isConnected}");
            }
            else
            {
                Debug.Log("This is master server connection");
                isConnectedToMaster = true;
            }
            Debug.Log($"Connected to {((IPEndPoint)socket.Client.RemoteEndPoint).Address.ToString()}, isMasterConnection={masterServerConnection}");

            socket.EndConnect(_result);
            if (!socket.Connected)
            {
                return;
            }

            stream = socket.GetStream();

            receivedData = new Packet();

            stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
        }

        /// <summary>Sends data to the client via TCP.</summary>
        /// <param name="_packet">The packet to send.</param>
        public void SendData(Packet _packet)
        {
            try
            {
                if (socket != null)
                {
                    stream.BeginWrite(_packet.ToArray(), 0, _packet.Length(), null, null); // Send data to server
                }
            }
            catch (Exception _ex)
            {
                Debug.Log($"Error sending data to server via TCP: {_ex}");
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
                    //instance.Disconnect();
                    return;
                }

                byte[] _data = new byte[_byteLength];
                Array.Copy(receiveBuffer, _data, _byteLength);

                receivedData.Reset(HandleData(_data)); // Reset receivedData if all data was handled
                stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message);
                //Disconnect();
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
                ThreadManager.ExecuteOnMainThread(() =>
                {
                    using (Packet _packet = new Packet(_packetBytes))
                    {
                        int _packetId = _packet.ReadInt();
                        if (!masterServerConnection)
                            packetHandlers[_packetId](_packet); // Call appropriate method to handle the packet
                        else
                        {

                        }
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

        /// <summary>Disconnects from the server and cleans up the TCP connection.</summary>
        public void Disconnect()
        {
            Debug.Log("Closing tcp socket");
            if (socket != null) socket.Close();
            stream = null;
            receivedData = null;
            receiveBuffer = null;
            socket = null;
        }
    }

    public class UDP
    {
        public UdpClient socket;
        public IPEndPoint endPoint;

        public UDP()
        {
            endPoint = new IPEndPoint(IPAddress.Parse(instance.ip), instance.port);
        }

        /// <summary>Attempts to connect to the server via UDP.</summary>
        /// <param name="_localPort">The port number to bind the UDP socket to.</param>
        public void Connect(int _localPort)
        {
            socket = new UdpClient(_localPort);

            socket.Connect(endPoint);
            socket.BeginReceive(ReceiveCallback, null);

            using (Packet _packet = new Packet())
            {
                SendData(_packet);
            }
        }

        /// <summary>Sends data to the client via UDP.</summary>
        /// <param name="_packet">The packet to send.</param>
        public void SendData(Packet _packet)
        {
            try
            {
                _packet.InsertInt(instance.myId); // Insert the client's ID at the start of the packet
                if (socket != null)
                {
                    socket.BeginSend(_packet.ToArray(), _packet.Length(), null, null);
                }
            }
            catch (Exception _ex)
            {
            }
        }

        /// <summary>Receives incoming UDP data.</summary>
        private void ReceiveCallback(IAsyncResult _result)
        {
            try
            {
                byte[] _data = socket.EndReceive(_result, ref endPoint);
                socket.BeginReceive(ReceiveCallback, null);

                if (_data.Length < 4)
                {
                    instance.Disconnect();
                    return;
                }

                HandleData(_data);
            }
            catch
            {
                Disconnect();
            }
        }

        /// <summary>Prepares received data to be used by the appropriate packet handler methods.</summary>
        /// <param name="_data">The recieved data.</param>
        private void HandleData(byte[] _data)
        {
            using (Packet _packet = new Packet(_data))
            {
                int _packetLength = _packet.ReadInt();
                _data = _packet.ReadBytes(_packetLength);
            }

            ThreadManager.ExecuteOnMainThread(() =>
            {
                using (Packet _packet = new Packet(_data))
                {
                    int _packetId = _packet.ReadInt();
                    packetHandlers[_packetId](_packet); // Call appropriate method to handle the packet
                }
            });
        }

        /// <summary>Disconnects from the server and cleans up the UDP connection.</summary>
        public void Disconnect()
        {
            if (socket != null) socket.Close();
            endPoint = null;
            socket = null;
        }
    }

    /// <summary>Initializes all necessary client data.</summary>
    private void InitializeClientData()
    {
        packetHandlers = new Dictionary<int, PacketHandler>()
        {
            { (int)ServerPackets.welcome, ClientHandle.Welcome },
            { (int)ServerPackets.spawnPlayer, ClientHandle.SpawnPlayer },
            { (int)ServerPackets.playerPosition, ClientHandle.PlayerPosition },
            { (int)ServerPackets.playerRotation, ClientHandle.PlayerRotation },
            { (int)ServerPackets.playerDisconnected, ClientHandle.PlayerDisconnected },
            { (int)ServerPackets.playerHealth, ClientHandle.PlayerHealth },
            { (int)ServerPackets.playerRespawned, ClientHandle.PlayerRespawned },
            { (int)ServerPackets.blockUpdated, ClientHandle.BlockUpdated },
            { (int)ServerPackets.simulationState, ClientHandle.SimulationState },
            { (int)ServerPackets.playerMuzzleFlash, ClientHandle.PlayerMuzzleFlash },
            { (int)ServerPackets.playerSelectedItemChange, ClientHandle.PlayerSelectedItemChange },
            { (int)ServerPackets.chunkOfUpdatedBlocksForNewlyJoinedPlayer, ClientHandle.ChunkOfUpdatedBlocksForNewlyJoinedPlayer },
            { (int)ServerPackets.ammoSimulation, ClientHandle.AmmoSimulation },
            { (int)ServerPackets.killfeedUpdate, ClientHandle.KillfeedUpdate },
            { (int)ServerPackets.playerChatMessage, ClientHandle.PlayerChatMessage },
            { (int)ServerPackets.playerTeamChangeAcknowledgement, ClientHandle.PlayerTeamChangeAcknowledgement },
            { (int)ServerPackets.playerAnimationTrigger, ClientHandle.PlayerAnimationTrigger },
            { (int)ServerPackets.mapNameUsedByServer, ClientHandle.MapNameUsedByServer },
            { (int)ServerPackets.mapSend, ClientHandle.MapSend },
            { (int)ServerPackets.mapHashUsedByServer, ClientHandle.MapHashUsedByServer },
            { (int)ServerPackets.serverMessage, ClientHandle.ServerMessage },
            { (int)ServerPackets.pingMessageAck, ClientHandle.PingMessageAck },
            { (int)ServerPackets.disconnectReason, ClientHandle.DisconnectReason },
        };
        Debug.Log("Initialized packets.");
    }

    /// <summary>Disconnects from the server and stops all network traffic.</summary>
    public void Disconnect()
    {
        Debug.Log("Disconnected from server.");
        isConnected = false;
        tcp.Disconnect();
        udp.Disconnect();
        tcp = null;
        udp = null;
    }
}
