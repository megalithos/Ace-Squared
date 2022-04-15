using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyParticleSystemAfterPlaying : MonoBehaviour
{
	ParticleSystem ps;
	private void Awake()
	{
		ps = GetComponent<ParticleSystem>();
	}

	private void FixedUpdate()
	{
		if (!ps.IsAlive())
		{
			Destroy(gameObject);
		}
	}
}