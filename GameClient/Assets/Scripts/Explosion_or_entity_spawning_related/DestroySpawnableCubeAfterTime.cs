using System.Collections;
using UnityEngine;

public class DestroySpawnableCubeAfterTime : MonoBehaviour
{
    public int interpolationFramesCount = 4500; // Number of frames to completely interpolate between the 2 positions
    int elapsedFrames = 0;

    public void Start()
    {
        StartCoroutine(ScaleOverTime(10f));
    }

    IEnumerator ScaleOverTime(float time)
    {
        Vector3 originalScale = transform.localScale;
        Vector3 destinationScale = new Vector3(0f, 0f, 0f);

        float currentTime = 0.0f;

        do
        {
            transform.localScale = Vector3.Lerp(originalScale, destinationScale, currentTime / time);
            currentTime += Time.deltaTime;
            yield return null;
        } while (currentTime <= time);
        World.spawnedCubeCount--;
        Destroy(gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.CompareTag("worldterrain"))
        {
        }
    }
}
