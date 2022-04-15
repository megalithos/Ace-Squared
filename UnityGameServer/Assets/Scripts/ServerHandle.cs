using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ServerHandle
{
    public static void WelcomeReceived(int _fromClient, Packet _packet)
    {
        int _clientIdCheck = _packet.ReadInt();
        string _username = _packet.ReadString();

        Debug.Log($"{Server.clients[_fromClient].tcp.socket.Client.RemoteEndPoint} connected successfully and is now player {_fromClient}.");
        if (_fromClient != _clientIdCheck)
        {
            Debug.Log($"Player \"{_username}\" (ID: {_fromClient}) has assumed the wrong client ID ({_clientIdCheck})!");
        }

		if (string.IsNullOrEmpty(_username))
			Server.clients[_fromClient].name = "unnamed" + _fromClient.ToString();
		else
			Server.clients[_fromClient].name = _username;


		int id = _fromClient;
    }
	
	public static void InputState(int _fromClient, Packet _packet)
	{
		// handle getting input state from the server
		Player.ClientInputState state = new Player.ClientInputState
		{
			lookDir = _packet.ReadVector3(),
			horizontalInput = _packet.ReadFloat(),
			verticalInput = _packet.ReadFloat(),
			_rotation = _packet.ReadQuaternion(),
			_jumpRequest = _packet.ReadBool(),
			simulationFrame = _packet.ReadInt()
		};
		Server.clients[_fromClient].player.OnClientInputStateReceived(state);
	}

 //   public static void PlayerMovement(int _fromClient, Packet _packet)
 //   {
 //       float[] _inputs = new float[_packet.ReadInt()];
 //       for (int i = 0; i < _inputs.Length; i++)
 //       {
 //           _inputs[i] = _packet.ReadFloat();
 //       }
 //       Quaternion _rotation = _packet.ReadQuaternion();
	//	bool jumpRequest = _packet.ReadBool();

 //       Server.clients[_fromClient].player.SetInput(_inputs, _rotation);
	//}

    public static void PlayerShoot(int _fromClient, Packet _packet)
    {
        Vector3 _shootDirection = _packet.ReadVector3();

        Server.clients[_fromClient].player.Shoot(_shootDirection);
    }

	public static void PlayerEditBlock(int _fromClient, Packet _packet)
	{
		Vector3 facingDirection = _packet.ReadVector3();
		byte newBlockID = _packet.ReadByte();

		Server.clients[_fromClient].player.EditBlock(facingDirection, newBlockID);
	}

	/// <summary>
	/// Handler for client authorized movement. This will be called when player sends his own decided
	/// position to the server.
	/// </summary>
	/// <param name="_fromClient"></param>
	/// <param name="_packet"></param>
	public static void PlayerPosition(int _fromClient, Packet _packet)
	{
		Vector3 position = _packet.ReadVector3();
		Quaternion rotation = _packet.ReadQuaternion();
		Vector3 lookDir = _packet.ReadVector3();
		bool crouching = _packet.ReadBool();
		Vector3 velocity = _packet.ReadVector3();
		
        Server.clients[_fromClient].player.SetPositionEtc(position, rotation, lookDir, crouching, velocity);
        Server.clients[_fromClient].player.OnPlayerPositionReceived();
        // Debug.Log("Received player pos: " + position);
    }

	public static void PlayerSelectedItemChange(int _fromClient, Packet _packet)
	{
		int newID = _packet.ReadInt();
        Server.clients[_fromClient].player.HandleSelectedItemID(newID);
	}

	public static void PlayerPhysicsShoot(int _fromClient, Packet _packet)
	{
		Vector3 dirToShoot = _packet.ReadVector3();
		Vector3 shootFrom = _packet.ReadVector3();
		Quaternion instantiateRotation = _packet.ReadQuaternion();
		ServerSend.AmmoSimulation(Server.clients[_fromClient].player, dirToShoot, shootFrom, Server.clients[_fromClient].player.selectedItem, instantiateRotation);
		Server.clients[_fromClient].player.PhysicsShoot(dirToShoot, shootFrom, instantiateRotation);
	}

	public static void PlayerChatMessage(int _fromClient, Packet _packet)
	{
		int messageLength = _packet.ReadInt();

		// check so we only read 255 bytes at maximum.
		if (messageLength > 255)
			messageLength = 255;

		byte[] message = _packet.ReadBytes(messageLength);

		ServerSend.PlayerChatMessage(_fromClient, message);
	}

	public static void TeamChangeRequest(int _fromClient, Packet _packet)
	{
		int teamID = _packet.ReadInt();

		Server.clients[_fromClient].player.wishTeamID = teamID;

		// if the client chose invalid team, servermessage it
		if (teamID >= (int)Config.ValidTeams.COUNT)
		{
			ServerSend.ServerMessage(_fromClient, 0);
		}
	}

	public static void PlayerSelectedBlockColorChange(int _fromClient, Packet _packet)
	{
		int color = _packet.ReadInt();

		Debug.Log("player changed color to: " + color);

		Server.clients[_fromClient].player.selectedBlockColor = color;
	}

	public static void PlayerAnimationTrigger(int _fromClient, Packet _packet)
	{
		int animationID = _packet.ReadInt();

		ServerSend.PlayerAnimationTrigger(_fromClient, animationID);
	}

	public static void ClientHasMap(int _fromClient, Packet _packet)
	{
		bool clientHasMap = _packet.ReadBool();

		if (clientHasMap)
		{
			// code here
		}
		else
		{
			ServerSend.SendMapDataToClientWhoDoesntHaveTheMap(_fromClient);
		}
	}

	// this handle will be called when the client has loaded and downloaded the map
	// so spawn the player only after he has loaded the map
	public static void MapLoadingDone(int _fromClient, Packet _packet)
	{
		Debug.Log($"Client {_fromClient} has loaded the map. Sending wit to game.");
        ServerSend.SplitServerUpdatedBlocksIntoChunksAndSendToNewlyConnectedClient(_fromClient, Server.updatedBlocks);
    }

    public static void LoadingUpdatedBlocksDone(int _fromClient, Packet _packet)
    {
        Debug.Log("Client has loaded all updated blocks.");
        Server.clients[_fromClient].SendIntoGame(Server.clients[_fromClient].name);
    }

    public static void LoadedPlayerID(int _fromClient, Packet _packet)
	{
		/* since this will be called from everyone everytime they load a player
		 * -> don't do anything to those that have fully loaded already
		 * because the whole point of this is to know when a new player has loaded all players */
		if (Server.clients[_fromClient].mapLoadingDone)
			return;

		Server.clients[_fromClient].loadedPlayerCount++;

		if (Server.clients[_fromClient].loadedPlayerCount >= Server.instantiated_player_count)
		{
			Debug.Log(_fromClient + " has loaded all players");
			Server.clients[_fromClient].FinishSendingIntoGame();
		}
	}

    public static void CommandWithArgs(int _fromClient, Packet _packet)
    {
        string msg = _packet.ReadString();
        Debug.Log("Received command with args from client: " + msg);
        CommandParser.HandleCommandFromClient(_fromClient, msg);
    }

    public static void SelectedClassID(int _fromClient, Packet _packet)
    {
        int id = System.Convert.ToInt32(_packet.ReadByte());
        Server.clients[_fromClient].currentItemsData.HandleSelectedClassID(id);
    }

    public static void ReloadGun(int _fromClient, Packet _packet)
    {
        Server.clients[_fromClient].currentItemsData.HandleReloadGun();
    }

    public static void PingMessage(int _fromClient, Packet _packet)
    {
        ServerSend.PingMessageAck(_fromClient);
        Server.clients[_fromClient].lastTimeReceivedPingFromTheClient = Server.ElapsedSeconds;
    }

    public static void PingMessageAckAck(int _fromClient, Packet _packet)
    {
        LagCompensation.History.instance.OnPingMessageReceived(_fromClient);
    }
}
