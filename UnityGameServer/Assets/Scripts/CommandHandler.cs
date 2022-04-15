using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Runtime.InteropServices;

// handle the received commands.
public static class CommandParser
{
	private delegate string CommandMethod(List<string> args, int fromClient);
	static List<Command> commands = new List<Command>();

	class Command
	{
		public string stringToInvokeCommand;
		public readonly CommandMethod method;
		public readonly string usageTip;
		public readonly int parameterCount;

		public Command(string _stringToInvokeCommand, CommandMethod _method, int _parametercount, string _usageTip = "")
		{
			stringToInvokeCommand = _stringToInvokeCommand;
			method = _method;
			commands.Add(this);
			usageTip = _usageTip;
			parameterCount = _parametercount;
		}
	}

	static Mutex mutex = null;
	static string outputMessage = null;
    public static bool stopThread { get; set;  } = false; // flag if we should stop the thread
	//public static void CommandHandler()
	//{
	//	Debug.Log("Started command handler thread");
	//	while (!stopThread)
	//	{
	//		// wait for command, parse etc it
	//		// then send output
	//		try
	//		{
	//			if (mutex == null)
	//				mutex = Mutex.OpenExisting(Config.CONSOLE_COMMUNICATION_KEY + "mutex");
	//			using (MemoryMappedFile mmf = MemoryMappedFile.OpenExisting(Config.CONSOLE_COMMUNICATION_KEY))
	//			{
	//				// wait for change
	//				mutex.WaitOne();

	//				string command = "";

	//				using (MemoryMappedViewStream stream = mmf.CreateViewStream())
	//				{
	//					BinaryReader reader = new BinaryReader(stream);
	//					command = reader.ReadString();
	//					Debug.Log("handling cmd");
	//					HandleCommand(command);
	//				}

	//				// here we have to wait until the proper method has been called on the main thread
	//				// then that output must be written into the stream
	//				while (string.IsNullOrEmpty(outputMessage))
	//				{
	//					Thread.Sleep(100);
	//				}

	//				// now that the string/flag/output message/whatever has been set -> write it to the stream
	//				using (MemoryMappedViewStream stream = mmf.CreateViewStream())
	//				{
	//					BinaryWriter writer = new BinaryWriter(stream);
	//					writer.Write(outputMessage);
	//					outputMessage = null; // reset
	//				}
	//				mutex.ReleaseMutex();
	//				Thread.Sleep(1000); // timeout so we don't start reading ourselves from the stream right away
	//				Debug.Log("Releasing mutex");
	//			}
	//		}
	//		catch (Exception e)
	//		{
	//			Debug.Log("error" + e.Message);
	//		}
	//		Thread.Sleep(100);
	//	}
 //       Debug.Log("Thread quitted.");
	//}

	public static void WriteRest(string restOfTheMessage = "OK")
	{
		using (MemoryMappedFile mmf = MemoryMappedFile.OpenExisting(Config.CONSOLE_COMMUNICATION_KEY))
		{
		}
	}

	public static void Init()
	{
		/* Commands to add: ban, listplayers, admin add/remove, map*/
		//Command command = new Command("team", SwitchTeam, 1, "/team [id], for example: /team 1");
		Command command = new Command("test", TestIfWorking, 0, "test, test for working");
		Command command2 = new Command("kick", KickPlayerID, 1, "kick [id], kicks a player from the server.");
		Command command3 = new Command("help", HelpCommand, 0, "");
		Command command4 = new Command("?", HelpCommand, 0, "");
		Command command5 = new Command("stop", Shutdown, 0, "shut the server down");
		//Command command6 = new Command("list", ListPlayers, 0, "lists out all the connected players.");
		//new Command("listban", ListBannedPlayerIPs, 0, "lists out all banned ip addresses");
		new Command("ban", BanIPAddress, 1, "ban a player by id");
		//new Command("unbanip", UnbanIPAddress, 1, "unban an ip address");
		//new Command("adminadd", AddAdmin, 1, "adds an admin (by ip address) and invokes admin priviledges for that particular client");
		//new Command("adminremove", RemoveAdmin, 1, "removes an admin (by ip address) and revokes all admin permissions");
        new Command("god", GodToggle, 0, "toggles god mode on a particular player (by id)");
        new Command("vanish", VanishToggle, 0, "toggles vanish on particular player (by id)");
        new Command("ping", PingPlayer, 0, "gets the ping to the specific player (by id)");
    }

    public static void HandleCommandFromClient(int clientID, string cmd)
    {
        // first ensure that the client is not impersonating some admin player
        if (!Server.clients[clientID].isAdmin)
            return;

        Debug.Log("he is not admin, doing");
        HandleCommand(cmd, true, clientID);
    }

	public static void HandleCommand(string commandArgs, bool fromClient, int clientID)
	{
		// split the message by whitespace
		string[] args = commandArgs.Split(null);
		List<string> argsList = new List<string>();

		for (int i = 0; i < args.Length; i++)
		{
			if (!System.String.IsNullOrWhiteSpace(args[i]))
				argsList.Add(args[i]); // do not add empty
		}

		foreach (Command item in commands)
		{
			if (item.stringToInvokeCommand == argsList[0].ToLower())
			{
				try
				{
					if (argsList.Count - 1 == item.parameterCount)
					{
						// execute on main thread, because we might do something that accesses unity's classes
                        if (!fromClient)
                        {
                            ThreadManager.ExecuteOnMainThread(() => {
                                outputMessage = item.method(argsList, clientID);
                            });
                        }
                        else // if we get it from the client we don't want any output messages
                        {
                            ThreadManager.ExecuteOnMainThread(() => {
                                ServerSend.ServerMessage(clientID, Config.server_msg.custom, item.method(argsList, clientID));
                            });
                        }
						return;
					}
					else
					{
						outputMessage = "Invalid args. Syntax: " + item.usageTip;
						return;
					}
				}
				catch (Exception e)
				{
					outputMessage = "Invalid args. Syntax: " + item.usageTip;
					return;
				}
			}
		}
		outputMessage = "Invalid command. Type \"help\" or \"?\" to view available commands.";
	}

	static string TestIfWorking(List<string> args, int id)
	{
		Debug.Log("working");
		return "Connection is up and running.";
	}

	static string KickPlayerID(List<string> args, int id)
	{
		int playerID;
		try
		{
			playerID = Convert.ToInt32(args[1]);
		}
		catch (Exception) // handle when player puts string as 2nd argument instead of numbers
		{
			return "Error: Invalid id.";
		}

		if (Server.clients.ContainsKey(playerID) && Server.clients[playerID].mapLoadingDone) // ensure that the client has loaded the map
		{
			Server.clients[playerID].Disconnect(Config.server_msg.disconnect_reason_kick);
			return "OK";
		}
		else
			return "Error: There is no player associated with that id.";
	}

	static string HelpCommand(List<string> args, int id)
	{
		string str = " ";

		foreach (Command command in commands)
		{
			string thisCmdString = command.stringToInvokeCommand + " | " + command.usageTip + "\n";
			str += thisCmdString;
		}
		return str;
	}

	static string Shutdown(List<string> args, int id)
	{
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
               Application.Quit();
        #endif

        return "OK";
	}

	static string ListPlayers(List<string> args, int id)
	{
		string str = "ID\t\tNAME\tIP\t\tIS_ADMIN\tIS_GOD\tIS_VANISHED\n";
		foreach (KeyValuePair<int, Client> kvp in Server.clients)
		{
			if (kvp.Value.mapLoadingDone)
			{
                // might wanna clean this up later
				str += kvp.Value.id + "\t\t" + kvp.Value.name + "\t\t" +
                    Config.ConvertFromIntegerToIpAddress(kvp.Value.ipAddress) + "\t\t" + kvp.Value.isAdmin + "\t" +
                    kvp.Value.godmodeEnabled + "\t" + kvp.Value.vanishEnabled +"\n";
			}
		}
		return str;
	}

	static string ListBannedPlayerIPs(List<string> args, int id)
	{
		string str = "Banned players: " + Config.bannedPlayerIPS.Count.ToString() + "\n";

		foreach (uint i in Config.bannedPlayerIPS)
		{
			str += Config.ConvertFromIntegerToIpAddress(i) + "\n";
		}

		return str;
	}

	static string BanIPAddress(List<string> args, int id)
	{
		try
		{
            Server.BanPlayerByID(Convert.ToInt32(args[1]));
            return "OK";
		}
		catch(Exception e)
		{
			Debug.Log(e.Message);
		}
		return "Error.";
	}	

	static string UnbanIPAddress(List<string> args, int id)
	{
		try
		{
			uint ip = Config.ConvertFromIpAddressToInteger(args[1]);
			Config.bannedPlayerIPS.Remove(ip);
			Config.SaveIPAddressesToFile(Config.PATH_FOR_BANNED_PLAYER_IPS, Config.bannedPlayerIPS);
			return "OK";
		}
		catch(Exception e)
		{
			Debug.Log(e.Message);
		}
		return "Error.";
	}	

	static string AddAdmin(List<string> args, int id)
	{
		try
		{
			uint ip = Config.ConvertFromIpAddressToInteger(args[1]);
			if (!Config.adminIPS.Contains(ip))
			{
				Config.adminIPS.Add(ip);
				Config.SaveIPAddressesToFile(Config.PATH_FOR_ADMIN_LIST, Config.adminIPS);

				foreach (Client client in Server.clients.Values)
				{
					// if the player client (by ip) is connected to the server -> add him as admin
					// and let him know that he is now an admin
					if (client.mapLoadingDone && client.ipAddress == ip)
					{
						client.isAdmin = true;

						// send him the message that he is now an admin
						ServerSend.ServerMessage(client.id, Config.server_msg.you_are_now_admin);
					}
				}
				return "OK";
			}
		}
		catch (Exception e)
		{
			Debug.Log(e.Message);
		}
		return "Error.";
	}

	static string RemoveAdmin(List<string> args, int id)
	{
		try
		{
			uint ip = Config.ConvertFromIpAddressToInteger(args[1]);
			Config.adminIPS.Remove(ip);
			Config.SaveIPAddressesToFile(Config.PATH_FOR_ADMIN_LIST, Config.adminIPS);

			foreach (Client client in Server.clients.Values)
			{
				// if the player client (by ip) is connected to the server -> add him as admin
				// and let him know that he is now an admin
				if (client.mapLoadingDone && client.ipAddress == ip)
				{
					client.isAdmin = false;

					// send him the message that he is now an admin
					ServerSend.ServerMessage(client.id, Config.server_msg.you_are_not_admin_anymore);
				}
			}
			return "OK";
		}
		catch(Exception e)
		{
			Debug.Log(e.Message);
		}
		return "Error.";
	}

    static string GodToggle(List<string> args, int id)
    {
        int playerID;
        try
        {
            playerID = id;
        }
        catch (Exception) // handle when player puts string as 2nd argument instead of numbers
        {
            return "Error: Invalid id.";
        }

        if (Server.clients.ContainsKey(playerID) && Server.clients[playerID].mapLoadingDone) // ensure that the client has loaded the map
        {
            Client refClient = Server.clients[playerID];
            refClient.godmodeEnabled = !refClient.godmodeEnabled;
            if (refClient.godmodeEnabled)
            {
                ServerSend.ServerMessage(playerID, Config.server_msg.god_mode_enabled);
                return "OK, godmode enabled for player " + playerID.ToString();
            }
            else
            {
                ServerSend.ServerMessage(playerID, Config.server_msg.god_mode_disabled);
                return "OK, godmode disabled for player " + playerID.ToString();
            }
                
        }
        else
            return "Error: There is no player associated with that id.";
    }

    static string VanishToggle(List<string> args, int id)
    {
        int playerID;
        try
        {
            playerID = id;
        }
        catch (Exception) // handle when player puts string as 2nd argument instead of numbers
        {
            return "Error: Invalid id.";
        }

        if (Server.clients.ContainsKey(playerID) && Server.clients[playerID].mapLoadingDone) // ensure that the client has loaded the map
        {
            Debug.Log("boom");
            Client refClient = Server.clients[playerID];
            refClient.vanishEnabled = !refClient.vanishEnabled;

            // if we enabled the vanish
            if (refClient.vanishEnabled)
            {
                ServerSend.PlayerDisconnected(playerID);
                ServerSend.ServerMessageToAll(Config.server_msg.usr_disconnected, Server.clients[playerID].name);
            }
            else // disable vanish on this player
            {
                // Send the player to all players
                foreach (Client _client in Server.clients.Values)
                {
                    if (_client.player != null)
                    {
                        // do not send to himself
                        if (_client.id != playerID)
                        {
                            ServerSend.SpawnPlayer(_client.id, refClient.player, refClient.player.selectedItem);
                            ServerSend.ServerMessageToAll(Config.server_msg.usr_connected, Server.clients[playerID].name);
                        }
                    }
                }
            }
            

            if (refClient.vanishEnabled)
                return "OK, vanish enabled for player " + playerID.ToString();
            else
                return "OK, vanish disabled for player " + playerID.ToString();

            return "OK";
        }
        else
            return "Error: There is no player associated with that id.";
    }

    static string PingPlayer(List<string> args, int id)
    {
        int playerID;
        try
        {
            playerID = id;
        }
        catch (Exception) // handle when player puts string as 2nd argument instead of numbers
        {
            return "Error: Invalid id.";
        }

        try
        {
            float ping = LagCompensation.History.instance.GetPingByPlayerID(playerID);
            ServerSend.ServerMessage(id, Config.server_msg.your_ping_is, ping.ToString());
            Debug.Log("admin requested ping");
            return "OK, Player (# " + playerID.ToString() + ") average ping is: " + ping;
        }
        catch (Exception)
        {

        }
        return "Error.";
    }
}
