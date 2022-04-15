using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveBulletToDestination : MonoBehaviour
{
	Vector3 destination;
	bool destinationSet = false;

	public void SetDestination(Vector3 dir, float dist)
	{
		destination = transform.position + dir.normalized * dist;
		destinationSet = true;
	}

    // Update is called once per frame
    void Update()
    {
        if (destinationSet)
		{
			//transform.position = Vector3.Lerp(transform.position, destination, 10f * Time.deltaTime);
			transform.position = Vector3.MoveTowards(transform.position, destination, Time.deltaTime * 100f);

			//if (Vector3.Distance(transform.position, destination) < 0.2f)
			//{
			//	Destroy(gameObject);
			//}
		}
	}
}
