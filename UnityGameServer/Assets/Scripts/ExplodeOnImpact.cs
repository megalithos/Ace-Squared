using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExplodeOnImpact : MonoBehaviour
{
	public int shooterSelectedItem = -1;
	public int shooterID = -1;
	public void Awake()
	{
		StartCoroutine(ExplodeIfHitNothing());
	}

	void OnCollisionEnter(Collision coll)
	{
		Explode();
	}

	IEnumerator ExplodeIfHitNothing()
	{
		yield return new WaitForSeconds(8f);
		Explode();
	}

	void Explode()
	{
		float x = Mathf.FloorToInt(gameObject.transform.position.x);
		float y = Mathf.FloorToInt(gameObject.transform.position.y);
		float z = Mathf.FloorToInt(gameObject.transform.position.z);
		Vector3 explosionPosition = new Vector3(x, y, z);

		// explode()
		Player.CreateExplosion(explosionPosition, Vector3.zero, shooterSelectedItem);

		// destroy()
		if (shooterSelectedItem == (int)Player.ValidItems.rocketLauncher)
		{
			Collider[] hits = Physics.OverlapSphere(
		  explosionPosition,
		  Player.explosionRadius * 2f);

			foreach (Collider coll in hits)
			{
				if (coll.tag == "PlayerCollider")
				{
					Debug.Log("hit player with rocket launcher");
					coll.GetComponent<Player>().TakeDamage(100f, Player.DeathSource.rocketLauncher, shooterID);
				}
					
			}
		}
		else if (shooterSelectedItem == (int)Player.ValidItems.mgl)
		{
			Collider[] hits = Physics.OverlapSphere(
		  explosionPosition,
		  Player.explosionRadius * 4f);

			foreach (Collider coll in hits)
			{
				if (coll.tag == "PlayerCollider")
				{
					Player player = coll.GetComponent<Player>();
					int damageToTake = 0;
					damageToTake = 100 - Mathf.RoundToInt((Vector3.Distance(player.transform.position, explosionPosition) /( Player.explosionRadius * 3f)) * 100);
					//Debug.Log("sending damage to take: " + damageToTake);
					player.TakeDamage(damageToTake, Player.DeathSource.mgl, shooterID);
				}
					
			}
		}
		Destroy(gameObject);
	}
}
