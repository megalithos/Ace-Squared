using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

public class ServerSend
{
    /// <summary>Sends a packet to a client via TCP.</summary>
    /// <param name="_toClient">The client to send the packet the packet to.</param>
    /// <param name="_packet">The packet to send to the client.</param>
    private static void SendTCPData(int _toClient, Packet _packet, bool sendSynchronously = false)
    {
		if (!Server.clients[_toClient].mapLoadingDone && !_packet.sendToPlayerWhoseMapIsNotLoaded)
		{
			return;
		}
		_packet.WriteLength();
		Server.clients[_toClient].tcp.SendData(_packet);
    }

    /// <summary>Sends a packet to a client via UDP.</summary>
    /// <param name="_toClient">The client to send the packet the packet to.</param>
    /// <param name="_packet">The packet to send to the client.</param>
    private static void SendUDPData(int _toClient, Packet _packet)
    {
		if (!Server.clients[_toClient].mapLoadingDone && !_packet.sendToPlayerWhoseMapIsNotLoaded)
		{
			return;
		}
        _packet.WriteLength();
        Server.clients[_toClient].udp.SendData(_packet);
    }

    /// <summary>Sends a packet to all clients via TCP.</summary>
    /// <param name="_packet">The packet to send.</param>
    private static void SendTCPDataToAll(Packet _packet)
    {
        _packet.WriteLength();
        foreach (KeyValuePair<int, Client> kvp in Server.clients)
        {
            //Debug.Log("Trying " + kvp.Key);
			if (!Server.clients[kvp.Key].mapLoadingDone && !_packet.sendToPlayerWhoseMapIsNotLoaded)
			{
				continue;
			}
            Server.clients[kvp.Key].tcp.SendData(_packet);
        }
    }
    /// <summary>Sends a packet to all clients except one via TCP.</summary>
    /// <param name="_exceptClient">The client to NOT send the data to.</param>
    /// <param name="_packet">The pasend.</param>
    private static void SendTCPDataToAll(int _exceptClient, Packet _packet)
    {
        _packet.WriteLength();
        foreach (KeyValuePair<int, Client> kvp in Server.clients)
        {
			if (!Server.clients[kvp.Key].mapLoadingDone && !_packet.sendToPlayerWhoseMapIsNotLoaded)
			{
				continue;
			}
            if (kvp.Key != _exceptClient)
            {
                Server.clients[kvp.Key].tcp.SendData(_packet);
            }
        }
    }

    /// <summary>Sends a packet to all clients via UDP.</summary>
    /// <param name="_packet">The packet to send.</param>
    private static void SendUDPDataToAll(Packet _packet)
    {
        _packet.WriteLength();
        foreach (KeyValuePair<int, Client> kvp in Server.clients)
        {
			if (!Server.clients[kvp.Key].mapLoadingDone && !_packet.sendToPlayerWhoseMapIsNotLoaded)
			{
				continue;
			}
            Server.clients[kvp.Key].udp.SendData(_packet);
        }
    }
    /// <summary>Sends a packet to all clients except one via UDP.</summary>
    /// <param name="_exceptClient">The client to NOT send the data to.</param>
    /// <param name="_packet">The packet to send.</param>
    private static void SendUDPDataToAll(int _exceptClient, Packet _packet)
    {
        _packet.WriteLength();

        foreach (KeyValuePair<int, Client> kvp in Server.clients)
        {
            if (!Server.clients[kvp.Key].mapLoadingDone && !_packet.sendToPlayerWhoseMapIsNotLoaded)
            {
                continue;
            }
            if (kvp.Key != _exceptClient)
            {
                Server.clients[kvp.Key].udp.SendData(_packet);
            }
        }
    }

    #region Packets
    /// <summary>Sends a welcome message to the given client.</summary>
    /// <param name="_toClient">The client to send the packet to.</param>
    /// <param name="_msg">The message to send.</param>
    public static void Welcome(int _toClient, string _msg)
    {
        using (Packet _packet = new Packet((int)ServerPackets.welcome))
        {
			_packet.Write(_toClient);
            _packet.Write(Config.VERSION);
            _packet.Write(Config.startingClassID);
			_packet.sendToPlayerWhoseMapIsNotLoaded = true;
            _packet.Write(Server.clients[_toClient].isAdmin);

			Debug.Log("sending welcome packket");
            SendTCPData(_toClient, _packet);
        }
    }

    /// <summary>Tells a client to spawn a player.</summary>
    /// <param name="_toClient">The client that should spawn the player.</param>
    /// <param name="_player">The player to spawn.</param>
    public static void SpawnPlayer(int _toClient, Player _player, int selectedItem = -1)
    {
        using (Packet _packet = new Packet((int)ServerPackets.spawnPlayer))
        {
            _packet.Write(_player.id);
            _packet.Write(Server.clients[_player.id].name);
            _packet.Write(_player.transform.position);
            _packet.Write(_player.transform.rotation);
			_packet.Write(selectedItem);
			_packet.Write(_player.teamID);
			_packet.Write(_player.kills);
			_packet.Write(_player.deaths);
			_packet.sendToPlayerWhoseMapIsNotLoaded = true; 
            SendTCPData(_toClient, _packet);
        }
        Debug.Log("sent player spawn to player: " + _toClient);
    }

    /// <summary>Sends a player's updated position to all clients except the one who moved because of
	/// client prediction so it does not get bugged. We are anyways sending simulationstate to the player so we don't need
	/// to send it here.</summary>
    /// <param name="_player">The player whose position to update.</param>
    public static void PlayerPosition(Player _player)
    {
        using (Packet _packet = new Packet((int)ServerPackets.playerPosition))
        {
            _packet.Write(_player.id);
            _packet.Write(_player.transform.position);
			_packet.Write(_player.velocity);

			SendUDPDataToAll(_player.id, _packet);
        }
    }

	// send player position to only one player
	public static void PlayerPositionSingle(Player _player)
	{
		using (Packet _packet = new Packet((int)ServerPackets.playerPosition))
		{
			_packet.Write(_player.id);
			_packet.Write(_player.transform.position);

			SendTCPData(_player.id, _packet);
		}
	}

	/// <summary>Sends a player's updated rotation to all clients except to himself (to avoid overwriting the local player's rotation).</summary>
	/// <param name="_player">The player whose rotation to update.</param>
	public static void PlayerRotation(Player _player)
    {
        using (Packet _packet = new Packet((int)ServerPackets.playerRotation))
        {
            _packet.Write(_player.id);
            _packet.Write(_player.transform.rotation);
			_packet.Write(_player.lookDir);
			_packet.Write(_player.crouching);
			SendUDPDataToAll(_player.id, _packet);
        }
    }

	public static void SendSimulationState(Player _player, Player.SimulationState simulationState)
	{
		using (Packet _packet = new Packet((int)ServerPackets.simulationState))
		{
			_packet.Write(_player.id);
			_packet.Write(simulationState.position);
			_packet.Write(simulationState._velocity);
			_packet.Write(simulationState.simulationFrame);

			SendUDPData(_player.id, _packet);
		}
	}


	public static void PlayerDisconnected(int _playerId)
    {
        using (Packet _packet = new Packet((int)ServerPackets.playerDisconnected))
        {
            _packet.Write(_playerId);

            // no point in sending to the client that disconnected.
            // and should allow pretty easy "vanish" operations.
            SendTCPDataToAll(_playerId, _packet);
        }
    }

    public static void PlayerHealth(Player _player)
    {
        using (Packet _packet = new Packet((int)ServerPackets.playerHealth))
        {
            _packet.Write(_player.id);
            _packet.Write(_player.health);

            SendTCPDataToAll(_packet);
        }
    }

    public static void PlayerRespawned(Player _player, Vector3 posToSpawnTo)
    {
        using (Packet _packet = new Packet((int)ServerPackets.playerRespawned))
        {
            _packet.Write(_player.id);
			_packet.Write(posToSpawnTo);
            SendTCPDataToAll(_packet);
        }
    }

	// when block is udpated, send the updated block data to players so
	// they can re-render it and surrounding faces
	public static void BlockUpdated(Server.UpdatedBlock block)
	{
		using (Packet _packet = new Packet((int)ServerPackets.blockUpdated))
		{
			_packet.Write(block.position);
			_packet.Write(block.color);
			_packet.Write(block.solid);
			_packet.sendToPlayerWhoseMapIsNotLoaded = true;

			SendTCPDataToAll(_packet);
		}
	}

	public static void PlayerMuzzleFlash(Player _player)
	{
		using (Packet _packet = new Packet((int)ServerPackets.playerMuzzleFlash))
		{
			_packet.Write(_player.id);
			SendUDPDataToAll(_player.id, _packet);
		}
	}


	/// <summary>
	/// This method is used for the following reason: if player places and/or destroys blocks in the world,
	/// the next player that joins will need to update those blocks too.
	/// </summary>
	/// <param name="_toClient">ID of client to send the updated blocks.</param>
	/// <param name="block">Updated block to send to the player.</param>
	// !!! be careful with this one, might cause crash if every/almost every block is edited on the map
	// this is one point that you should optimize at some point
	// -> make it so that it does not spam many packets, but sends the packets as larger chunks
	public static void SendChunkOfUpdatedBlocks(HashSet<Server.UpdatedBlock> chunkOfDataToSend, int _toClient, bool moreToSend, bool sendToAll)
	{
		// first write int containing length, then
		// write all n members to the packet and send with tcp (importantly ofc)
		using (Packet _packet = new Packet((int)ServerPackets.chunkOfUpdatedBlocksForNewlyJoinedPlayer))
		{
			_packet.sendToPlayerWhoseMapIsNotLoaded = true;
            Debug.Log("trynna send");
			if (!moreToSend)
			{
				_packet.Write(-1); // tell the client we have nothing more to send

				if (!sendToAll)
					SendTCPData(_toClient, _packet);
				else
					SendTCPDataToAll(_packet);
			}
			else
			{
				_packet.Write(chunkOfDataToSend.Count);

				foreach (Server.UpdatedBlock block in chunkOfDataToSend)
				{
					_packet.Write(block.position);
					_packet.Write(block.color);
					_packet.Write(block.solid);
				}

				if (!sendToAll)
					SendTCPData(_toClient, _packet);
				else
					SendTCPDataToAll(_packet);
			}
			
		}
	}

	public static void SendPlayerSelectedItemChange(int newID, int _exceptClient, bool sendToNewPlayer = false)
	{
		using (Packet _packet = new Packet((int)ServerPackets.playerSelectedItemChange))
		{
			_packet.Write(newID);
			_packet.Write(_exceptClient); // so the client knows which player update
			SendTCPDataToAll(_exceptClient, _packet);
		}
	}

	public static void SendPlayerSelectedItemChangeToSingle(int newID, int _toClient, int _clientWhoseSelectedItemHasChanged)
	{
		using (Packet _packet = new Packet((int)ServerPackets.playerSelectedItemChange))
		{
			_packet.Write(newID);
			_packet.Write(_clientWhoseSelectedItemHasChanged); // so the client knows which player update
			SendTCPData(_toClient, _packet);
		}
	}

	//
	public static void AmmoSimulation(Player _player, Vector3 dir, Vector3 fromPos, int itemID, Quaternion rot)
	{
		using (Packet _packet = new Packet((int)ServerPackets.ammoSimulation))
		{
			_packet.Write(_player.id);
			_packet.Write(dir);
			_packet.Write(fromPos);
			_packet.Write(itemID);
			_packet.Write(rot);
			SendTCPDataToAll(_player.id, _packet);
		}
	}

	/// <summary>
	/// Does not send anything with tcp or udp directly. Handles management for sending larger chunk updates than only 1 block.
	/// </summary>
	/// <param name="_id"></param>
	public static void SplitServerUpdatedBlocksIntoChunksAndSendToNewlyConnectedClient(int _id, HashSet<Server.UpdatedBlock> dic, bool sendToAll = false)
	{
		int howManyUpdatesBlocksToSendPerChunk = 20;

		HashSet<Server.UpdatedBlock> chunkOfDataToSend = new HashSet<Server.UpdatedBlock>();
		
		foreach (Server.UpdatedBlock block in dic)
		{
			// not looped through, since we are inside the loop
			chunkOfDataToSend.Add(new Server.UpdatedBlock(block.position, block.color, block.solid)); // add to the send list

			if (chunkOfDataToSend.Count == 20)
			{
				ServerSend.SendChunkOfUpdatedBlocks(chunkOfDataToSend, _id, true, sendToAll);

				// clear send list
				chunkOfDataToSend.Clear();
			}
		}
		// send the rest
		ServerSend.SendChunkOfUpdatedBlocks(chunkOfDataToSend, _id, true, sendToAll);

		//finally send int -1, to tell that we have nothing more to send
		ServerSend.SendChunkOfUpdatedBlocks(null, _id, false, sendToAll);
	}

	public static void KillfeedUpdate(int shooterID, Player.DeathSource src, bool headshot, int killedPlayerID)
	{
		using (Packet _packet = new Packet((int)ServerPackets.killfeedUpdate))
		{
			_packet.Write(System.Convert.ToByte(shooterID));
			_packet.Write(System.Convert.ToByte(src));
			_packet.Write(headshot);
			_packet.Write(System.Convert.ToByte(killedPlayerID));

			SendTCPDataToAll(_packet);
		}
	}

	public static void PlayerChatMessage(int senderID, byte[] message)
	{
		using (Packet _packet = new Packet((int)ServerPackets.playerChatMessage))
		{
			_packet.Write(senderID);
			_packet.Write(message.Length);
			_packet.Write(message);

			SendTCPDataToAll(_packet);
		}
	}

	/// <summary>
	/// Let everyone know that this player changed team. Useful on the client side so we only have to send this information once and
	/// the client can use the information to for example display player's name on the correct side of the scoreboard
	/// based on team.
	/// </summary>
	/// <param name="changerID"></param>
	/// <param name="teamID"></param>
	public static void PlayerTeamChangeAcknowledgement(int changerID, int teamID)
	{
		using (Packet _packet = new Packet((int)ServerPackets.playerTeamChangeAcknowledgement))
		{
			_packet.Write(changerID);
			_packet.Write(teamID);

			SendTCPDataToAll(_packet);
		}
	}

	public static void PlayerAnimationTrigger(int senderID, int animationID)
	{
		using (Packet _packet = new Packet((int)ServerPackets.playerAnimationTrigger))
		{
			_packet.Write(senderID);
			_packet.Write(animationID);
			SendTCPDataToAll(senderID, _packet);
		}
	}

	// Write the map's name to the packet and send it
	public static void MapWeAreUsing(int _toClient)
	{
		using (Packet _packet = new Packet((int)ServerPackets.mapNameUsedByServer))
		{
			_packet.Write(Config.MAP);
			_packet.sendToPlayerWhoseMapIsNotLoaded = true;
			SendTCPData(_toClient, _packet);
		}
	}

	// send the map's hash to the client
	public static void MapHashOfTheMapWeAreUsing(int _toClient, byte[] mapHash)
	{
		Debug.Log($"Sending map hash to client {_toClient}!");
		using (Packet _packet = new Packet((int)ServerPackets.mapHashUsedByServer))
		{
			_packet.Write(mapHash.Length);	
			_packet.Write(mapHash);
			_packet.sendToPlayerWhoseMapIsNotLoaded = true;
			SendTCPData(_toClient, _packet);
		}
	}
	/// <summary>
	/// Send the map file to the client who informed us that he does not have it.
	/// </summary>
	/// <param name="_toClient"></param>
	public static void SendMapDataToClientWhoDoesntHaveTheMap(int _toClient)
	{
		Debug.Log($"Sending map hash to client {_toClient}");
		using (Packet _packet = new Packet((int)ServerPackets.mapSend))
		{
			byte[] map = File.ReadAllBytes(Config.MAP_FILEPATH);

			_packet.Write(map.Length);
			_packet.Write(map);
			_packet.sendToPlayerWhoseMapIsNotLoaded = true;
			SendTCPData(_toClient, _packet);
		}
	}

	/// <summary>
	/// Send the message_id  to the client. Client has a table to know what the message means. Server can
	/// optionally pass in a message string, which the client will get if sent.
	/// </summary>
	/// <param name="_toClient"></param>
	/// <param name="id"></param>
	/// <param name="message"></param>
	public static void ServerMessage(int _toClient, Config.server_msg messageID, string usrName = null, bool sendSynchronously = false)
	{
        string msg = Config.serverMessageStrings[messageID];

        if (!string.IsNullOrEmpty(usrName))
        {
            msg = string.Format(msg, usrName);
        }

		using (Packet _packet = new Packet((int)ServerPackets.serverMessage))
		{
            _packet.Write(msg);

			SendTCPData(_toClient, _packet, sendSynchronously);
		}
	}

	/// <summary>
	/// Send the message_id  to the client. Client has a table to know what the message means. Server can
	/// optionally pass in a message string, which the client will get if sent.
	/// </summary>
	/// <param name="_toClient"></param>
	/// <param name="id"></param>
	/// <param name="message"></param>
	public static void ServerMessageToAll(Config.server_msg msg, string optionalInput = null)
	{
        string message = Config.serverMessageStrings[msg];

        if (!string.IsNullOrEmpty(optionalInput))
        {
            message = string.Format(message,optionalInput);
        }

		Debug.Log($"Sending message (string:{message}) to all clients.");
		using (Packet _packet = new Packet((int)ServerPackets.serverMessage))
		{
            _packet.Write(message);
			SendTCPDataToAll(_packet);
		}
	}

	/// <summary>
	/// Not like the others serversend methods. This will send a broadcast
	/// message to the master_server including information about this server_program such as; how many players
	/// are connected, what is the server's name and which map it's using. So this does not send anything
	/// to the actual client!!
	/// </summary>
	/// <param name="tcp">The tcp object associated with the tcp connection to the master server.</param>
	public static void BroadcastExistenceToMaster(Client.TCP tcp)
	{

		using (Packet _packet = new Packet(2)) // 2 is id for broadcasting
		{
			_packet.Write(Config.SERVER_NAME);
			_packet.Write(Config.MAP);
			_packet.Write(Server.instantiated_player_count);
			_packet.Write(Config.MAX_PLAYERS);
			_packet.Write(Config.MOTD);


			_packet.WriteLength();
			tcp.SendData(_packet);
		}
	}

    public static void PingMessageAck(int toClient)
    {
        LagCompensation.History.instance.SetSentAtMillis(toClient);
        using (Packet _packet = new Packet((int)ServerPackets.pingMessageAck))
        {
            byte b = 0;
            _packet.Write(b);
            SendTCPData(toClient, _packet);
        }
    }

    public static void DisconnectReason(int _toClient, Config.server_msg messageID)
    {
        string msg = Config.serverMessageStrings[messageID];

        using (Packet _packet = new Packet((int)ServerPackets.disconnectReason))
        {
            _packet.Write(msg);

            SendTCPData(_toClient, _packet);
        }
    }
    #endregion
}
