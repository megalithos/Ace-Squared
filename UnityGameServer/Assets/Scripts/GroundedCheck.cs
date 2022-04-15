using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GroundedCheck : MonoBehaviour
{
	void OnCollisionStay(Collision coll)
	{
		Debug.Log("collidion");
		if (coll.gameObject.tag == "worldterrain")
		{
			GetComponentInParent<Player>().isGrounded = true;
		}
	}
}