using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This script handles speedhack detection.
/// </summary>
class SpeedHackDetection: MonoBehaviour
{
    private Player player;
    private Anticheat ac;
    private float movedDistance = 0f;
    private float distanceMovedDuringLast = 0f;
    private float timeToCheckAfterInSeconds = .25f;
    private float highestMovementTreshold = 1.8f;
    private int timeAfterWhichToResetViolationCountInSeconds = 60;
    private int violationCount = 0;
    private int maxViolations = 12; // treshold after which to ban the player

    private void Awake()
    {
        player = GetComponent<Player>();
        StartCoroutine(Distance());
        StartCoroutine(ResetViolationCountAfterTime());
        ac = GetComponent<Anticheat>();
    }

    // Resets the violation count back to zero after certain amount of time.
    IEnumerator ResetViolationCountAfterTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(timeAfterWhichToResetViolationCountInSeconds);
            violationCount = 0;
        }
    }

    IEnumerator Distance()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(timeToCheckAfterInSeconds);
            float distanceMovedSinceLast = movedDistance - distanceMovedDuringLast;
           // Debug.Log($"distance moved in {timeToCheckAfterInSeconds}: " + distanceMovedSinceLast + ", violationcount: " + violationCount);

            if (distanceMovedSinceLast > highestMovementTreshold)
            {
                Violate();
                // banplayer for example
            }

            // pause execution if the player is dead
            while (player.isDead)
            {
                yield return new WaitForSeconds(.1f);
            }
            distanceMovedDuringLast = movedDistance;
        }
    }

    private void Violate()
    {
        violationCount++;

        if (violationCount > maxViolations)
        {
            ac.TriggerPlayerBan();
        }
    }

    public void AddMovedDistance(float amount)
    {
        movedDistance += amount;
    }
}