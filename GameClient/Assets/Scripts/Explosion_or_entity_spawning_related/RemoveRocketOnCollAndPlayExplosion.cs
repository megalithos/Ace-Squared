using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RemoveRocketOnCollAndPlayExplosion : MonoBehaviour
{
	bool collisionHappened = false;
	Vector3 stayAtPos;
	Rigidbody rb;
	MeshRenderer mr;
	public GameObject explosionPrefab;
	public AudioClip explosionSound;
	AudioSource audioSrc;
	public List<GameObject> disableOnCollisionList = new List<GameObject>();

	public void Awake()
	{
		rb = GetComponent<Rigidbody>();
		mr = GetComponent<MeshRenderer>();
		audioSrc = GetComponent<AudioSource>();
		StartCoroutine(ExplodeIfHitNothing());
	}

	IEnumerator ExplodeIfHitNothing()
	{
		yield return new WaitForSeconds(8f);
		Explode();
	}

	void OnCollisionEnter(Collision coll)
	{
		if (coll.gameObject == GameManager.localPlayer.gameObject)
			return;

		if (!collisionHappened)
			Explode();

	}

	void Explode()
	{

		collisionHappened = true;
		GameObject instantiatedExplosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
		instantiatedExplosion.GetComponent<AudioSource>().PlayOneShot(explosionSound);
		stayAtPos = transform.position;
		rb.useGravity = false;
		rb.freezeRotation = true;
		if (mr != null)
			mr.forceRenderingOff = true;
		rb.velocity = Vector3.zero;
		StartCoroutine(DestroyAfterTime());
	}

	int countDestroyed = 0;
	void FixedUpdate()
	{
		if (!collisionHappened)
			return;

		foreach (GameObject obj in disableOnCollisionList)
		{
			if (obj != null && Vector3.Distance(obj.transform.position, gameObject.transform.position) < 0.5f)
			{
				Destroy(obj);
				countDestroyed++;
			}
		}

		if (countDestroyed == disableOnCollisionList.Count)
			Destroy(gameObject);
		// destroy this when reached target.
	}

	IEnumerator DestroyAfterTime()
	{
		yield return new WaitForSeconds(5f);

		Destroy(gameObject);
	}
}
