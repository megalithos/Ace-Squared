using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;

public class ServerListItem : MonoBehaviour
{
    public Text nameText;
    public Text mapText;
    public Text playersText;
    public Text pingText;
    public int id;
    public Image imgWhichToChangeWhenSelected;
    public string ip;

    private Color startColor;

    void Awake()
    {
        startColor = imgWhichToChangeWhenSelected.color;
        ResetColor();
    }

    public void ChangeColorOnSelected()
    {
        ServerListManager.instance.SetNewSelected(id);
    }

    public void SetSelectColor()
    {
        imgWhichToChangeWhenSelected.color = startColor;
    }

    public void ResetColor()
    {
        imgWhichToChangeWhenSelected.color = new Color(0, 0, 0, 0);
    }
}
