using System.Net.NetworkInformation;
using UnityEngine;
using Text = TMPro.TextMeshProUGUI;

public class HUDManager : MonoBehaviour
{
    public GameObject fpsTextObj;
    public TMPro.TextMeshProUGUI ammoText;
    private TMPro.TextMeshProUGUI fpsText;
    public Text pingText;
    public GameObject scopeOverlay;
    int frameCount = 0;
    float dt = 0.0f;
    float fps = 0.0f;
    float updateRate = 4.0f;  // 4 updates per sec.
    public TMPro.TextMeshProUGUI hpText;
    public GameObject scoreBoard;
    public static HUDManager instance;
    public GameObject debugInfoHolder; // parent to all debug stuff like coordinates etc.
    public Text debugInfoCoordinateText;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(this);

        fpsText = fpsTextObj.GetComponent<TMPro.TextMeshProUGUI>();
        SetScopeOverlayActive(false);
        scoreBoard.SetActive(false);

        fpsText.gameObject.SetActive(System.Convert.ToBoolean(Config.settings["fps_text_enabled"]));
        debugInfoHolder.SetActive(System.Convert.ToBoolean(Config.settings["debug_info_enabled"]));
    }

    void Update()
    {
        frameCount++;
        dt += Time.deltaTime;
        if (dt > 1.0 / updateRate)
        {
            fps = frameCount / dt;
            frameCount = 0;
            dt -= 1.0f / updateRate;
        }
        fpsText.text = "fps: " + Mathf.Round(fps).ToString();

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            scoreBoard.SetActive(true);
        }
        else if (Input.GetKeyUp(KeyCode.Tab))
        {
            scoreBoard.SetActive(false);
        }

        if (debugInfoHolder.activeSelf && GameManager.localPlayer != null)
        {

            Vector3 playerTransform = GameManager.localPlayer.transform.position;
            debugInfoCoordinateText.text = string.Format("coords: ({0}, {1}, {2})", playerTransform.x, playerTransform.y, playerTransform.z);
        }
    }

    public void UpdateAmmoText(int bulletsLeft, int bulletsLeftTotal)
    {
        ammoText.text = bulletsLeft.ToString() + "|" + bulletsLeftTotal.ToString();
    }


    public void SetScopeOverlayActive(bool myBool)
    {
        scopeOverlay.SetActive(myBool);
    }

    public bool GetScopeOverlayActive()
    {
        return scopeOverlay.activeSelf;
    }

    public void SetHpText(int amount)
    {
        hpText.text = amount.ToString();
    }

    public void PingCompletedCallback(object sender, PingCompletedEventArgs e)
    {
        if (e.Cancelled || e.Error != null)
            return;
        Debug.Log("received ping");
        Debug.Log(e.Reply.RoundtripTime);
    }
}
