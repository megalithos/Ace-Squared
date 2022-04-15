using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace LagCompensation
{
	public class History : MonoBehaviour
	{
		public static History instance;
        [SerializeField] bool debugTickPingInConsole = true;

		public void Start()
		{
			if (instance == null)
			{
				instance = this;
			}
			else
			{
				Destroy(this);
			}

			Client.OnClientConnectedAndMapLoadedEvent += OnClientConnectedEvent;
			Client.OnClientDisconnectedEvent += OnClientDisconnectEvent;
            //StartCoroutine(PingAllPlayers());
		}

		public Dictionary<int, PlayerLag> playerLag { get; private set; } = new Dictionary<int, PlayerLag>();

		// basically every tick -> loop through all players that are connected to the server and add snapshot of the player
		void FixedUpdate()
		{
			foreach (KeyValuePair<int, PlayerLag> kvp in playerLag)
			{
				Player player = Server.clients[kvp.Key].player;

				if (player != null)
				{
					kvp.Value.AddPlayerSnapshot(player);
				}
			}
		}

        public float GetPingByPlayerID(int _id)
        {
            if (playerLag.ContainsKey(_id))
            {
                return playerLag[_id].AverageLagAsPing;
            }
            else
                return -1; // error code
        }

		// ping player when he connects to the server...
		void OnClientConnectedEvent(int _id)
		{
			// we don't want here anything to do with master server
			if (_id == -1)
				return;

			Console.WriteLine("Client id: " + ", connected to the server... Pinging him...");

			PlayerLag playerLagToAdd = new PlayerLag();
			playerLagToAdd.sentAtMillis = Server.ElapsedMillis; // save the elapsed millis to the playerlag object so we can deduct the player's ping when it will callback
			playerLag.Add(_id, playerLagToAdd);
		}

		// when client disconnects, remove him from the lists so we don't use ram when we don't need to.
		void OnClientDisconnectEvent(int _id)
		{
			// we don't want here anything to do with master server
			if (_id == -1)
				return;
			Debug.Log(_id + " disconnected from the server, so deallocating it's corresponding playerLag object. (removing it)");

			playerLag.Remove(_id);
		}

		public void OnPingMessageReceived(int fromPlayer)
		{
            //Debug.Log("Received ping ack ack");
			// ensure the playerLag has the key fromPlayer before doing anything
			if (playerLag.ContainsKey(fromPlayer))
			{
				int playerPingInMillis = (Server.ElapsedMillis - playerLag[fromPlayer].sentAtMillis) / 2;
				playerLag[fromPlayer].AddTickLag(playerPingInMillis);
			}
        }

        public void SetSentAtMillis(int id)
        {
            if (playerLag.ContainsKey(id))
            {
                playerLag[id].sentAtMillis = Server.ElapsedMillis;
            }
        }

        // Rewind to the certain tick. (Takes int as input to know which tick we want to rewind to.)
        // Looks for the TickRecord element of ticks list at that point and loops through all players and
        // puts them in those positions that they were at that point of time.
        public void RewindInTime(int playerID)
		{
			int playerLagInTicks = playerLag[playerID].AverageLagAsTicks;

            if (debugTickPingInConsole)
                Debug.Log("Rewinding " + playerLagInTicks + " behind..." + "for player" + playerID + ", whose ping is " + playerLag[playerID].AverageLagAsPing + " ms");

            foreach (KeyValuePair<int, PlayerLag> kvp in playerLag)
			{
				if (playerLag.ContainsKey(kvp.Key)) // ensure that we are not trying to rewind a disconnected player
				{
					Player refPlayer = Server.clients[kvp.Key].player;

					int tickToRollBackTo = (PlayerLag.LAG_HISTORY_MAX - 1) - playerLagInTicks;

					if (tickToRollBackTo < 0)
					{
						tickToRollBackTo = 0; // ensure that we use the oldest player snapshot in case the player is lagging heavily

						Debug.Log("Player (" + kvp.Key + ") seems to be lagging heavily. Using tick to roll back to as maximum");
					}

					// ensure the list's count is higher or equal to the tick we want to rollback to
					// to make sure we are not trying to access index that is out of range
					if (playerLag[kvp.Key].playerSnapshotHistory.Count >= tickToRollBackTo)
						SetPlayerPositionsToThoseInSnapshot(refPlayer, playerLag[kvp.Key].playerSnapshotHistory[tickToRollBackTo]);
				}
			}
		}

		// when we rewind in time, before that save all positions to this table
		// List<PlayerSnapshot> playerSnapshotsToUnrewindTo = new List<PlayerSnapshot>();
		public void UnRewind()
		{
			//Debug.Log("UnRewinding player positions...");
			foreach (KeyValuePair<int, PlayerLag> kvp in playerLag)
			{
				if (playerLag.ContainsKey(kvp.Key))
				{
					Player refPlayer = Server.clients[kvp.Key].player;

					PlayerSnapshot snapshot = playerLag[kvp.Key].playerSnapshotHistory[playerLag[kvp.Key].playerSnapshotHistory.Count - 1]; // use the highest index (newest one) to rewind players' positions

					SetPlayerPositionsToThoseInSnapshot(refPlayer, snapshot);
				}
			}
		}

		public void SetPlayerPositionsToThoseInSnapshot(Player playerRef, PlayerSnapshot snap)
		{
			playerRef.transform.position = snap.pos;
			playerRef.transform.rotation = snap.rot;
			playerRef.headToRotate.transform.rotation = snap.headRot;
			playerRef.crouching = snap.crouching;
		}
	}

}
