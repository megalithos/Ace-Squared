using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class TimedAction : MonoBehaviour
{
    public static TimedAction instance;

    public void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
            Destroy(this);
    }

    //public void SetNewTimedAction(Action action, float waitForSeconds, bool repeatConstantly)
    //{
    //    Debug.Log("set new timed action");
    //    StartCoroutine(Routine(action, waitForSeconds, repeatConstantly));
    //}

    //IEnumerator Routine(Action act, float waitForSeconds, bool repeatConstantly)
    //{
    //    Debug.Log("in routine()");
    //    while (true)
    //    {
    //        yield return new WaitForSecondsRealtime(waitForSeconds);
    //        Debug.Log("Executing timed action");
    //        act();

    //        if (!repeatConstantly)
    //            yield break;
    //    }
    //}
}
