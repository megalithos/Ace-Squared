using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Anticheat : MonoBehaviour
{
    [SerializeField] private bool isEnabled = true;
    private Player player;
    private SpeedHackDetection shd;
    private NoclipCallback noclipCallback;
    private FlyhackDetection flyhackDetection;

    public void Start()
    {
        player = GetComponent<Player>();

        shd = GetComponent<SpeedHackDetection>();
        noclipCallback = GetComponentInChildren<NoclipCallback>();
        flyhackDetection = GetComponent<FlyhackDetection>();
    }
       
    public void AddMovedDistance(float amount)
    {
        shd.AddMovedDistance(amount);
    }

    /// <summary>
    /// Flags suspicious player.
    /// </summary>
    /// <param name="id"></param>
    public void FlagPlayer(int id)
    {

    }

    public void TriggerPlayerBan()
    {
        if (Server.clients.TryGetValue(player.id, out Client c) && Config.ANTICHEAT_ENABLED)
        {
            // we dont wanna ban admins
            if (!c.isAdmin)
            {
                Server.BanPlayerByID(player.id);
            }
        }
    }

    public void TriggerPlayerKick()
    {
        if (Server.clients.TryGetValue(player.id, out Client c) && Config.ANTICHEAT_ENABLED)
        {
            // we dont wanna ban admins
            if (!c.isAdmin)
            {
                Server.clients[player.id].Disconnect(Config.server_msg.disconnect_reason_kick);
            }
        }
    }
}