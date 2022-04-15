using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RocketSmokeParticleFollow : MonoBehaviour
{
	Queue<Vector3> positionsToFollow = new Queue<Vector3>();
	public GameObject objToFollow;

	int iterationsCount = 0;
	int updatesBehind = 2;
    void FixedUpdate()
	{
		
		if (iterationsCount > updatesBehind)
		{
			try
			{
				Vector3 pos = positionsToFollow.Dequeue();
				if (pos != null)
					transform.position = pos;
			}
			catch (System.Exception e)
			{

			}
		}

		if (objToFollow != null)
			positionsToFollow.Enqueue(objToFollow.transform.position);
		iterationsCount++;
	}
}
