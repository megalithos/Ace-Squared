using UnityEngine;

public class Killfeed : MonoBehaviour
{
    [SerializeField]
    GameObject killfeeItemPrefab;
    public static Killfeed instance;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else Destroy(this);
        GameManager.instance.OnPlayerKilled_ += OnKillfeedUpdateReceived;
    }

    private void OnEnable()
    {
        Clear();
    }

    void OnKillfeedUpdateReceived(int killerID, int weaponID, bool killedByHeadshot, int killedID)
    {
        GameObject obj = Instantiate(killfeeItemPrefab, this.transform);
        obj.GetComponent<KillfeedItem>().Setup(GameManager.players[killerID].username, weaponID, killedByHeadshot, GameManager.players[killedID].username);
    }

    public void Clear()
    {
        foreach (Transform child in transform)
        {
            GameObject.Destroy(child.gameObject);
        }
    }
}
