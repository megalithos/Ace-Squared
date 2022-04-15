using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;

public class ClientSend : MonoBehaviour
{
    /// <summary>Sends a packet to the server via TCP.</summary>
    /// <param name="_packet">The packet to send to the sever.</param>
    private static void SendTCPData(Packet _packet)
    {
		try
		{
			_packet.WriteLength();
			Client.instance.tcp.SendData(_packet);
		}
		catch (System.Exception e)
		{
			//Debug.Log(e.Message);
		}
    }

    /// <summary>Sends a packet to the server via UDP.</summary>
    /// <param name="_packet">The packet to send to the sever.</param>
    private static void SendUDPData(Packet _packet)
    {
        _packet.WriteLength();
        Client.instance.udp.SendData(_packet);
    }

    #region Packets
    /// <summary>Lets the server know that the welcome message was received.</summary>
    public static void WelcomeReceived()
    {
		Debug.Log("Sending welcome packet with username: " + Client.instance.username);
        using (Packet _packet = new Packet((int)ClientPackets.welcomeReceived))
        {
            _packet.Write(Client.instance.myId);
            _packet.Write(Client.instance.username);

            SendTCPData(_packet);
        }
    }

    /// <summary>Sends player input to the server.</summary>
    /// <param name="_inputs"></param>
    public static void PlayerMovement(float[] _inputs, bool jumpRequest = false)
    {
        using (Packet _packet = new Packet((int)ClientPackets.playerMovement))
        {
            _packet.Write(_inputs.Length);
			foreach (float _input in _inputs)
			{
				_packet.Write(_input);
			}
            _packet.Write(GameManager.players[Client.instance.myId].transform.rotation);
			_packet.Write(jumpRequest);
            SendUDPData(_packet);
        }
    }

	/// <summary>
	/// For sending player's position and rotation to the server. This is used in case the client has
	/// control of choosing player's position and rotation.
	/// </summary>
	/// <param name="pos"></param>
	/// <param name="rot"></param>
	public static void PlayerPosition(Vector3 pos, Quaternion rot, Vector3 camForward, bool crouching, Vector3 velocity)
	{
		using (Packet _packet = new Packet((int)ClientPackets.playerPosition))
		{
			_packet.Write(pos);
			_packet.Write(rot);
			_packet.Write(camForward);
			_packet.Write(crouching);
			_packet.Write(velocity);
			SendUDPData(_packet);
		}
	}

	//public static void PlayerMovement(bool[] _inputs)
	//{
	//	using (Packet _packet = new Packet((int)ClientPackets.playerMovement))
	//	{
	//		_packet.Write(_inputs.Length);
	//		foreach (bool _input in _inputs)
	//		{
	//			_packet.Write(_input);
	//		}
	//		_packet.Write(GameManager.players[Client.instance.myId].transform.rotation);

	//		SendUDPData(_packet);
	//	}
	//}

	public static void PlayerShoot(Vector3 _facing)
    {
        using (Packet _packet = new Packet((int)ClientPackets.playerShoot))
        {
            _packet.Write(_facing);

            SendTCPData(_packet);
        }
    }

	public static void PhysicsShoot(Vector3 _facing, Vector3 fromPos, Quaternion instantiateRot)
	{
		using (Packet _packet = new Packet((int)ClientPackets.playerPhysicsShoot))
		{
			_packet.Write(_facing);
			_packet.Write(fromPos);
			_packet.Write(instantiateRot);
			SendTCPData(_packet);
		}
	}

	public static void PlayerEditBlock(Vector3 _facing, byte newBlockID)
	{
		using (Packet _packet = new Packet((int)ClientPackets.playerEditBlock))
		{
			_packet.Write(_facing);
			_packet.Write(newBlockID);
			SendTCPData(_packet);
		}
	}
	
	public static void SendInputState(Player.ClientInputState inputState)
	{
		using (Packet _packet = new Packet((int)ClientPackets.inputState))
		{
			_packet.Write(inputState.lookDir);
			_packet.Write(inputState.horizontalInput);
			_packet.Write(inputState.verticalInput);
			_packet.Write(inputState._rotation);
			_packet.Write(inputState._jumpRequest);
			_packet.Write(inputState.simulationFrame);
			SendUDPData(_packet);
		}
	}

	/// <summary>
	/// Send the newID to the server. Basically for telling the server: "hey, I just selected this item (gun etc.)". So server can send that
	/// forward to other clients and they can instantiate the correct prefab on correct player.
	/// </summary>
	/// <param name="newID"></param>
	public static void PlayerSelectedItemChange(int newID)
	{
		using (Packet _packet = new Packet((int)ClientPackets.playerSelectedItemChange))
		{
			_packet.Write(newID);
			SendTCPData(_packet);
		}
	}

	public static void PlayerChatMessage(byte[] encodedMessage)
	{
		using (Packet _packet = new Packet((int)ClientPackets.playerChatMessage))
		{
			// first write the length of the message, so receiver knows how many bytes to read
			_packet.Write(encodedMessage.Length);

			// then write the message as bytes.
			_packet.Write(encodedMessage);

			SendTCPData(_packet);
		}
	}

	public static void TeamChangeRequest(int id)
	{
		using (Packet _packet = new Packet((int)ClientPackets.teamChangeRequest))
		{
			_packet.Write(id);
			SendTCPData(_packet);
		}
	}

	public static void PlayerSelectedBlockColorChange(int color)
	{
		using (Packet _packet = new Packet((int)ClientPackets.playerSelectedBlockColorChange))
		{
			_packet.Write(color);
			SendTCPData(_packet);
		}
	}

	public static void PlayerAnimationTrigger(int animationID)
	{
		using (Packet _packet = new Packet((int)ClientPackets.playerAnimationTrigger))
		{
			_packet.Write(animationID);
			SendTCPData(_packet);
		}
	}

	// inform the server if we have the map (used by the server) or no
	public static void ClientHasMap(bool boolean)
	{
		using (Packet _packet = new Packet((int)ClientPackets.clientHasMap))
		{
			_packet.Write(boolean);
			SendTCPData(_packet);
		}
	}

	public static void MapLoadingDone()
	{
		using (Packet _packet = new Packet((int)ClientPackets.mapLoadingDone))
		{
			byte someByte = 0;
			_packet.Write(someByte);
			SendTCPData(_packet);
		}
	}

    public static void LoadingUpdatedBlocksDone()
    {
        Debug.Log("sending loaded updated blocks");
        using (Packet _packet = new Packet((int)ClientPackets.loadingUpdatedBlocksDone))
        {
            byte someByte = 0;
            _packet.Write(someByte);
            SendTCPData(_packet);
        }
    }

    public static void RequestServerListFromMaster(Client.TCP tcp)
	{
		using (Packet _packet = new Packet(4)) // 4 is the message id that tells the server that we want the server list
		{
			byte someByte = 0;
			_packet.Write(someByte);
			_packet.WriteLength();
			tcp.SendData(_packet);
		}
	}

	public static Task ReceivedServerList(Client.TCP tcp)
	{
		using (Packet _packet = new Packet(5)) // 5 is the message id that tells the master server that we got the server list
		{
			byte someByte = 0;
			_packet.Write(someByte);
			_packet.WriteLength();
			tcp.SendData(_packet);
			return null;
		}
	}

	// inform the server that we have loaded player id
	public static void LoadedPlayerID(int id)
	{
		using (Packet _packet = new Packet((int)ClientPackets.loadedPlayerID))
		{
			_packet.Write(id);
			Debug.Log("Sending loaded player id" + id);
			SendTCPData(_packet);
		}
	}

    public static void CommandWithArgs(string msg)
    {
        using (Packet _packet = new Packet((int)ClientPackets.commandWithArgs))
        {
            _packet.Write(msg);
            SendTCPData(_packet);
        }
    }

    public static void SelectedClassID(int id)
    {
        using (Packet _packet = new Packet((int)ClientPackets.selectedClassID))
        {
            _packet.Write((byte)id);
            SendTCPData(_packet);
        }
    }

    public static void ReloadGun()
    {
        using (Packet _packet = new Packet((int)ClientPackets.reloadGun))
        {
            byte a = 0;
            _packet.Write(a);
            SendTCPData(_packet);
        }
    }

    public static void PingMessage()
    {
        using (Packet _packet = new Packet((int)ClientPackets.pingMessage))
        {
            byte a = 0;
            _packet.Write(a);
            SendTCPData(_packet);
        }
    }

    public static void PingMessageAckAck()
    {
        using (Packet _packet = new Packet((int)ClientPackets.pingMessageAckAck))
        {
            byte a = 0;
            _packet.Write(a);
            SendTCPData(_packet);
        }
    }
    #endregion
}
