using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlyhackDetection : MonoBehaviour
{
    private Player player;
    private Anticheat ac;
    private int lastTimeHitGround;
    private int onAirTimeAllowedWithoutKickInSeconds = 10;

    private void Awake()
    {
        player = GetComponent<Player>();
        ac = GetComponent<Anticheat>();
        lastTimeHitGround = Server.ElapsedSeconds;
        StartCoroutine(FlyhackViolationChecker());
    }

    public void OnPlayerHitGround()
    {
        //Debug.Log("Player hit the ground.");

        lastTimeHitGround = Server.ElapsedSeconds;
    }

    IEnumerator FlyhackViolationChecker()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(1);
            if (Server.ElapsedSeconds - lastTimeHitGround > onAirTimeAllowedWithoutKickInSeconds)
            {
                ac.TriggerPlayerKick();
            }
        }
    }

    public void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("worldterrain"))
        {
            OnPlayerHitGround();
        }
    }
}
