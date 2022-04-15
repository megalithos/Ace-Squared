using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoclipCallback : MonoBehaviour
{
	Player player;
    private Anticheat ac;

    void Start()
	{
        ac = GetComponentInParent<Anticheat>();
		player = GetComponentInParent<Player>(); // add reference to the player script in parent
	}

	void OnTriggerEnter(Collider other)
	{
		// check if we are triggering with worldterrain
        if (other.CompareTag("worldterrain"))
        {
            if (!player.isDead)
                OnPlayerClippingThroughWalls();
        }
	}

	void OnPlayerClippingThroughWalls()
	{
        ac.TriggerPlayerKick();
	}
}
