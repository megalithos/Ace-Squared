using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChatTextItem : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(SetActiveFalseAfterSeconds());
    }

    IEnumerator SetActiveFalseAfterSeconds()
    {
        yield return new WaitForSecondsRealtime(10);

        if (!ChatManager.instance.GetChatBoxActive())
            gameObject.SetActive(false);
    }
}
