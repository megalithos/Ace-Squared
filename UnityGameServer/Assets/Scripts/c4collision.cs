using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class c4collision : MonoBehaviour
{
	Rigidbody rb;
	private Vector3 posCur;
	private Quaternion rotCur;
	bool grounded = false;
	[HideInInspector]
	public int shooterID = -1;

	[HideInInspector]
	public bool thrown = false;
	bool collisionHappened = false;
	AudioSource audioSrc;

	private void Awake()
	{
		rb = GetComponent<Rigidbody>();
		StartCoroutine(DestroyIfHitNothingIn20s());
	}

	IEnumerator DestroyIfHitNothingIn20s()
	{
		if (gameObject != null)
			yield return new WaitForSeconds(20f);
		Destroy(gameObject);
	}

	private void OnCollisionEnter(Collision other)
	{
		if (other.transform.CompareTag("worldterrain"))
		{
			Collide();
		}
	}

	void Collide()
	{
		if (collisionHappened)
			return;

		rb.isKinematic = true;
		collisionHappened = true;
		transform.rotation = rotCur;
		Debug.Log("beeping");
		//transform.position += dist * -transform.up * 0.35f;
		RaycastHit hit;
		if (Physics.Raycast(new Ray(transform.position, -transform.up), out hit, .5f) == true && !hit.transform.CompareTag("LocalPlayer") && !hit.transform.CompareTag("Player"))
		{
			transform.position += hit.distance * 0.7f * -transform.up;
		}
		StartCoroutine(Beep());
	}

	void SetCorrectRotation()
	{
		if (!thrown)
			return;

		//declare a new Ray. It will start at this object's position and it's direction will be straight down from the object (in local space, that is)

		Ray[] raysToCast = new Ray[]
		{
			new Ray(transform.position, -transform.up),
			new Ray(transform.position, transform.up),
			new Ray(transform.position, transform.forward),
		};

		//decalre a RaycastHit. This is neccessary so it can get "filled" with information when casting the ray below.
		RaycastHit hit;
		//cast the ray. Note the "out hit" which makes the Raycast "fill" the hit variable with information. The maximum distance the ray will go is 1.5
		if (Physics.Raycast(raysToCast[0], out hit, .5f) == true && !hit.transform.CompareTag("LocalPlayer") && !hit.transform.CompareTag("Player"))
		{
			Debug.Log("raycast hitl");
			//draw a Debug Line so we can see the ray in the scene view. Good to check if it actually does what we want. Make sure that it uses the same values as the actual Raycast. In this case, it starts at the same position, but only goes up to the point that we hit.
			//Debug.DrawLine(transform.position, hit.point, Color.green);
			//store the roation and position as they would be aligned on the surface
			rotCur = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
			posCur = new Vector3(transform.position.x, hit.point.y, transform.position.z);
		}
	}

	private void Update()
	{
		SetCorrectRotation();
	}

	public void Throw()
	{
		thrown = true;
		Debug.Log("throwed c4");
	}

	int totalBeeps = 4;
	float timeBetweenBeeps = 1f; // in seconds
	float lightFlashTime = 0.1f;
	IEnumerator Beep()
	{
		for (int i = 0; i < totalBeeps; i++)
		{
			
			yield return new WaitForSeconds(lightFlashTime);
			

			yield return new WaitForSeconds(timeBetweenBeeps);
		}

		// explode
		Vector3 vectorToReturn = new Vector3(Mathf.Round(-transform.up.normalized.x), Mathf.Round(-transform.up.normalized.y), Mathf.Round(-transform.up.normalized.z));
		Debug.Log("returning " + vectorToReturn);
		Player.CreateExplosion(transform.position, vectorToReturn, shooterID, (int)Player.ValidItems.c4, Player.ExplosionType.sphericalTunnel);
		Destroy(gameObject);
	}
}