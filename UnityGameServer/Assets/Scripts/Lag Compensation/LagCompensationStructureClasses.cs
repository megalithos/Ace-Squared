using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LagCompensation
{
	// for each tick and for each player have one of these PlayerSnapshots
	public struct PlayerSnapshot
	{
		public Vector3 pos { get; private set; }
		public Quaternion rot { get; private set; }
		public Quaternion headRot { get; private set; }
		public bool crouching { get; private set; }

		// constructor
		public PlayerSnapshot(Vector3 _pos, Quaternion _rot, Quaternion _headRot, bool _crouching)
		{
			pos = _pos;
			rot = _rot;
			headRot = _headRot;
			crouching = _crouching;
		}
	}

	/* single object of PlayerLag holds ping record and playersnapshots for like past 20 ticks or so 
	 * */
	public class PlayerLag : MonoBehaviour
	{
		public const int LAG_HISTORY_MAX = 20;

		float ping;
		public int sentAtMillis = -1; // set (-1) as default

		List<float> tickLagHistory = new List<float>();
		public List<PlayerSnapshot> playerSnapshotHistory { get; private set; }  = new List<PlayerSnapshot>();

		// add a snapshot of the player's coordinate and rotation information.
		public void AddPlayerSnapshot(Player player)
		{
			PlayerSnapshot playerSnapshot = new PlayerSnapshot(player.transform.position, player.transform.rotation, player.headToRotate.transform.rotation, player.crouching);

			playerSnapshotHistory.Add(playerSnapshot);

			// if the list size is too large, remove element 0 and also remove it's value from accumulated tick lag
			if (playerSnapshotHistory.Count > LAG_HISTORY_MAX)
			{
				playerSnapshotHistory.RemoveAt(0);
			}
		}

		float accumulatedTickLag = 0;
		public void AddTickLag(float d)
		{
			tickLagHistory.Add(d);

			accumulatedTickLag += d;

			// if the list size is too large, remove element 0 and also remove it's value from accumulated tick lag
			if (tickLagHistory.Count > LAG_HISTORY_MAX)
			{
				accumulatedTickLag -= tickLagHistory[0];
				tickLagHistory.RemoveAt(0);
			}
		}

		public float AverageLagAsPing
		{
			get
			{
				// Use ping as an approximation until TickLagHistory is populated
				if (tickLagHistory.Count < LAG_HISTORY_MAX)
					return ping / Config.MS_PER_TICK;

				return accumulatedTickLag / LAG_HISTORY_MAX;
			}
		}

		public int AverageLagAsTicks
		{
			get
			{
				if (tickLagHistory.Count < LAG_HISTORY_MAX)
					return Convert.ToInt32(Mathf.Round(ping / Config.MS_PER_TICK));

                // accumulatedticklag/lag_history_max is the avg ping
                // divide by config.ms_per_tick
				return Convert.ToInt32(Mathf.Round(accumulatedTickLag / LAG_HISTORY_MAX / Config.MS_PER_TICK));
			}
		}
	}
}
