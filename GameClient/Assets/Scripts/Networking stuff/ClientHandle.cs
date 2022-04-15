using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using System;
using System.IO;

public class ClientHandle : MonoBehaviour
{
    public static void Welcome(Packet _packet)
    {
        int _myId = _packet.ReadInt();
        Debug.Log("my id is " + _myId);
        string version = _packet.ReadString();
        int classID = _packet.ReadByte();
        bool isAdmin = _packet.ReadBool();

        if (version == Config.VERSION)
        {
            Client.instance.myId = _myId;
            Client.instance.firstClassID = classID;
            Debug.Log("Server gave us class id: " + Client.instance.firstClassID);

            ClientSend.WelcomeReceived();

            // Now that we have the client's id, connect UDP
            Client.instance.udp.Connect(((IPEndPoint)Client.instance.tcp.socket.Client.LocalEndPoint).Port);

            Client.instance.isAdmin = isAdmin;
        }
        else
        {
            GameManager.instance.disconnectReason = string.Format("Version mismatch. Our version is: {0}, whereas server is using version:{1}", Config.VERSION, version);
            GameManager.instance.DisconnectAndReturnBackToLobby();
        }
    }

    public static void SpawnPlayer(Packet _packet)
    {
        int _id = _packet.ReadInt();
        string _username = _packet.ReadString();
        Vector3 _position = _packet.ReadVector3();
        Quaternion _rotation = _packet.ReadQuaternion();
		int selectedItem = _packet.ReadInt();
		int teamID = _packet.ReadInt();
		int kills = _packet.ReadInt();
		int deaths = _packet.ReadInt();

		Debug.Log($"Spawning player ID:{_id}, \"{ _username}\" ");
        GameManager.instance.SpawnPlayer(_id, _username, _position, _rotation, selectedItem, teamID, kills, deaths);
    }

    public static void PlayerPosition(Packet _packet)
    {
        int _id = _packet.ReadInt();
        Vector3 _position = _packet.ReadVector3();
		Vector3 _velocity = _packet.ReadVector3();

		//Debug.Log("Setting pos for id: " + _id);
		GameManager.players[_id].SetPosition(_position);
		GameManager.players[_id].SetVelocity(_velocity);
    }

    public static void PlayerRotation(Packet _packet)
    {
        int _id = _packet.ReadInt();
        Quaternion _rotation = _packet.ReadQuaternion();
		Vector3 lookDir = _packet.ReadVector3();
		bool crouching = _packet.ReadBool();

		GameManager.players[_id].crouching = crouching;
		GameManager.players[_id].transform.rotation = _rotation;
		GameManager.players[_id].lookDir = lookDir;
	}

    public static void PlayerDisconnected(Packet _packet)
    {
        int _id = _packet.ReadInt();

		GameManager.instance.PlayerDisconnect(_id);
        Destroy(GameManager.players[_id].gameObject);
        GameManager.players.Remove(_id);
    }

	public void SetUsernameOnChange(string username)
	{
		Client.instance.username = username;
	}

    public static void PlayerHealth(Packet _packet)
    {
        int _id = _packet.ReadInt();
        float _health = _packet.ReadFloat();

        GameManager.players[_id].SetHealth(_health);
    }

    public static void PlayerRespawned(Packet _packet)
    {
        int _id = _packet.ReadInt();
		Vector3 spawnPosition = _packet.ReadVector3();
        GameManager.players[_id].Respawn(spawnPosition);
        Debug.Log("received player respawned");
	}

	public static void BlockUpdated(Packet _packet)
	{
		Vector3 pos = _packet.ReadVector3();
		int color = _packet.ReadInt();
		byte blockID = _packet.ReadByte();

		GameObject.Find("GameManager").GetComponent<World>().UpdateBlock(pos, color, blockID);
	}

	public static void PlayerMuzzleFlash(Packet _packet)
	{
		int _id = _packet.ReadInt();
		GameManager.players[_id].PlayMuzzleFlash();
	}

	public static void SimulationState(Packet _packet)
	{
		int _id = _packet.ReadInt();
		// handle received simulation state
		Player.SimulationState simulationState = new Player.SimulationState {
			position = _packet.ReadVector3(),
			velocity = _packet.ReadVector3(),
			simulationFrame = _packet.ReadInt()
		};

		// send it to local player
		GameManager.localPlayer.OnServerSimulationStateReceived(simulationState);
	}

	public static void PlayerSelectedItemChange(Packet _packet)
	{
		int newID = _packet.ReadInt();
		int _id = _packet.ReadInt();

		// 1. change state

		// 2. instantiate

		GameManager.players[_id].GetComponent<ItemManager>().UpdateHandItemForNonLocalPlayer(newID);
	}

	public static void ChunkOfUpdatedBlocksForNewlyJoinedPlayer(Packet _packet)
	{
		int len = _packet.ReadInt();

		Debug.Log("Received chunk of updated blocks ( for newly joined player ) ..");
		if (len != -1)
		{
			List<UpdatedBlock> listOfUpdatedBlocks = new List<UpdatedBlock>();

			for (int i = 0; i < len; i++)
			{
				UpdatedBlock myUpdatedBlock = new UpdatedBlock(_packet.ReadVector3(), _packet.ReadInt(), _packet.ReadByte());

				listOfUpdatedBlocks.Add(myUpdatedBlock);
			}

			// once we have added all updated blocks to the list, send the list to world
			GameObject.Find("GameManager").GetComponent<World>().AddUpdateBlocksToList(listOfUpdatedBlocks);
		}
		else
		{
            Debug.Log("caling");
			GameObject.Find("GameManager").GetComponent<World>().StartUpdatingBlocksOnNewlyJoinedPlayer();
		}
	}

	public static void AmmoSimulation(Packet _packet)
	{
		int _id = _packet.ReadInt();
		Vector3 dir = _packet.ReadVector3();
		Vector3 fromPos = _packet.ReadVector3();
		int itemID = _packet.ReadInt();
		Quaternion rot = _packet.ReadQuaternion();
		Debug.Log("received ammo simulation");

		GameManager.players[_id].GetComponent<ItemManager>().SendBulletTrail(dir, fromPos, itemID, rot);
		GameManager.players[_id].GetComponent<ItemManager>().PlayItemSoundForCurrentlySelectedItem();
	}

	public static void KillfeedUpdate(Packet _packet)
	{
		byte shooterID = _packet.ReadByte();
		byte deathSource = _packet.ReadByte();
		bool headshot = _packet.ReadBool();
		byte killedPlayerID = _packet.ReadByte();

		Debug.Log(shooterID + " " + deathSource + " " + headshot + " " + killedPlayerID);

		GameManager.instance.KillfeedUpdate(shooterID, deathSource, headshot, killedPlayerID);
	}

	public static void PlayerChatMessage(Packet _packet)
	{
		int senderID = _packet.ReadInt();
		int messageLength = _packet.ReadInt();
		byte[] message = _packet.ReadBytes(messageLength);

		// no need to check if server is sending over 255 bytes lengthy array, since the server is ofc
		// trusted lul.
		GameManager.instance.PlayerChat(senderID, message);
	}

	public static void PlayerTeamChangeAcknowledgement(Packet _packet)
	{
		int changerID = _packet.ReadInt();
		int teamID = _packet.ReadInt();

		Debug.Log("received ack for team change request to team nro: " + teamID);

		GameManager.instance.ChangeTeam(changerID, teamID);
	}

	public static void PlayerAnimationTrigger (Packet _packet)
	{
		int fromClient = _packet.ReadInt();
		int animationID = _packet.ReadInt();

		Debug.Log("Received animation trigger for player: " + fromClient + ", with anim_id: " + animationID);

		GameManager.players[fromClient].GetComponent<PlayerManager>().PlayAnimation(animationID);
	}

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

	public static void MapNameUsedByServer(Packet _packet)
	{
		string mapName = _packet.ReadString();
		Config.SetMapName(mapName);
	}

	public static void MapHashUsedByServer(Packet _packet)
	{
		int length = _packet.ReadInt();
		byte[] hash = _packet.ReadBytes(length);

		Debug.Log($"Received hash for the MAP ({Config.MAP_NAME}) used by server...");
		UIManager.instance.loadingText.text = "Received map hash...";
		Config.HandleMapHashChecking(hash, Config.MAP_NAME);
	}

	// handler for server sending the map.
	public static void MapSend(Packet _packet)
	{
		int length = _packet.ReadInt();
		byte[] mapData = _packet.ReadBytes(length);

		Debug.Log("Received map. Saving it to the map folder...");

		UIManager.instance.loadingText.text = "Saving downloaded map...";

		// Write the map data to the correct folder with correct name
		File.WriteAllBytes(Config.MAP_PATH, mapData);

		Debug.Log($"Loading map \"{Config.MAP_NAME}\"...");
        World.instance.StartInitRoutine();
    }

	/// <summary>
	/// Handler for receiving 'string' messages from the server.
	/// </summary>
	public static void ServerMessage(Packet _packet)
	{
        string message = _packet.ReadString();

		GameManager.instance.OnServerMessage(message);
	}

    public static void PingMessageAck(Packet _packet)
    {
        _packet.ReadByte();

        GameManager.instance.PingMessageReceived();
        ClientSend.PingMessageAckAck();
    }

    public static void DisconnectReason(Packet _packet)
    {
        string reason = _packet.ReadString();
        GameManager.instance.disconnectReason = reason;
    }
}