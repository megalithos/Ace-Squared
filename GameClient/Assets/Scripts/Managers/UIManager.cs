using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using Text = TMPro.TextMeshProUGUI;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    public GameObject startMenu;
	public GameObject mainMenu;
	public GameObject serverBrowserMenu;
	public GameObject hudMenu;
	public GameObject loadingScreenMenu;
	public GameObject disconnectPopup;
	public GameObject optionsMenu;
	public GameObject pauseMenu;

	public TMPro.TextMeshProUGUI motdText;
	public TMPro.TextMeshProUGUI loadingText;
	public TMPro.TMP_InputField usernameField;
	[SerializeField] private Text reasonOfDisconnectingText;
	[SerializeField] private GameObject connectingText;
	private Text fpsText;
	public TMPro.TextMeshProUGUI classSelectorText;
	public bool canAttemptConnectingToServer  = true;
	public GameObject deathMenu;
	private SavedServer currentServer;
    public Text versionText;

	public enum MenuState
	{
		main_menu,
		options_menu,
		credits_menu,
		pause_menu,
		server_browser,
		loading_screen,
		in_game,
	}

	public MenuState state { get; private set; }
	public Queue<MenuState> prevState { get; private set; } = new Queue<MenuState>();
	static int maxPrevStateQueueSize = 2; // hold only 2 previous items

	List<GameObject> enabledMenuGameObjects = new List<GameObject>();

	Dictionary<MenuState, GameObject> activeGameObjectAtState;

	/* Some way of associating ui state with
	 * objects that need to be disabled. 
	 * 
	 * When entered new menu state, loop through all objects that were previously enabled.
	 * If they are not active in the new state, disable them.
	 * 
	 * When entering new menu state, also enable all ui objects that need to be enabled there.
	 * */

	private void Awake()
    {
		state = MenuState.main_menu;
		if (instance == null)
        {
            instance = this;
        }
        else 
        {
            Debug.Log("Instance already exists, destroying object!");
            Destroy(this);
        }
		
		DisableDeathMenu();

		// associate menu game objects with ids
		activeGameObjectAtState = new Dictionary<MenuState, GameObject>
		{
			{ MenuState.main_menu, mainMenu },
			{ MenuState.server_browser, serverBrowserMenu },
			{ MenuState.in_game, hudMenu }, // null for none
			{ MenuState.loading_screen, loadingScreenMenu},
			{ MenuState.options_menu, optionsMenu },
			{ MenuState.pause_menu, pauseMenu },
		};

		// just disable everything
		foreach (KeyValuePair<MenuState, GameObject> kvp in activeGameObjectAtState)
		{
			kvp.Value.SetActive(false);
		}

		UpdateMenus();
		ToggleEnabledDisconnectedPopup(false);
        versionText.text = "Version: " + Config.VERSION;
	}

	void Update()
	{
		if ((state == MenuState.in_game || state == MenuState.pause_menu) && Input.GetKeyDown(KeyCode.Escape))
		{
			if (state != MenuState.pause_menu)
			{
				// enable menu
				SetState(MenuState.pause_menu);
				Cursor.lockState = CursorLockMode.None;
				GameManager.localPlayer.menuOpen = true;
			}
			else
			{
				// disable menu
				SetState(MenuState.in_game);
				Cursor.lockState = CursorLockMode.Locked;
				GameManager.localPlayer.menuOpen = false;
			}
		}
	}

	public void SetClassSelectorText(string txt)
	{
		classSelectorText.text = "selected class: " + txt;
	}

	public void DisableDeathMenu()
	{
		deathMenu.SetActive(false);
	}

	public void EnableDeathMenu()
	{
		deathMenu.SetActive(true);
	}

	public void QuitGame()
	{
		Debug.Log("Quitting program");
		Application.Quit();
	}

	public void SetState(MenuState _state)
	{
		if (state == _state)
			return;

		
		state = _state;
		Debug.Log("Set state to " + state);
		UpdateMenus();

		// if we want to do something more than just update menus
		// such as edit some of the showing text in those menus
		switch (state)
		{
			case MenuState.loading_screen:
                // tags are specified in inspector {0} and {1}
                try
                {
                    motdText.text = string.Format(motdText.text, currentServer.server_name, currentServer.motd);
                    loadingText.text = "Waiting...";
                }
                catch (Exception) { }
				
				break;
		}

		prevState.Enqueue(state);

		// dequeue
		if (prevState.Count > 2)
			prevState.Dequeue();
	}

	/// <summary>
	/// Disable all previous menu objects and enable current ones.
	/// </summary>
	void UpdateMenus()
	{
		// disable
		foreach (KeyValuePair<MenuState, GameObject> kvp in activeGameObjectAtState)
		{
			// from all menu objects, disable those that are active and should
			// not be active.
			if (kvp.Value.activeSelf == true && state != kvp.Key)
			{
				kvp.Value.SetActive(false);
			}
		}

		// enable
		activeGameObjectAtState[state].SetActive(true);
	}

	/// <summary>
	/// Enter server browser menu. Callback for pressing server browser button.
	/// </summary>
	public void EnterServerBrowserMenu()
	{
		SetState(MenuState.server_browser);
	}

	public void EnterMainMenu()
	{
		SetState(MenuState.main_menu);
	}

	public void SetSelectedServer(SavedServer ss)
	{
		currentServer = ss;
	}

	public void ChangeDisconnectedReasonText(string text)
	{
		reasonOfDisconnectingText.text = text;
	}

	// enable=true we enable it
	// false = we disable it
	public void ToggleEnabledDisconnectedPopup(bool enable)
	{
		disconnectPopup.SetActive(enable);
	}

	public void EnterOptionsMenu()
	{
		SetState(MenuState.options_menu);
	}

	public void EnterOptionsMenuFromPauseMenu()
	{
		fromPauseMenu = true;
		SetState(MenuState.options_menu);
	}

	// for button
	public void EnterInGameFromPauseMenu()
	{
		SetState(MenuState.in_game);
		Cursor.lockState = CursorLockMode.Locked;
		GameManager.localPlayer.menuOpen = false;
	}

	// set flag
	bool fromPauseMenu = false;
	// id is equivalent to MenuState ids
	public void ExitOptionsMenu()
	{
		if (fromPauseMenu)
			SetState(MenuState.pause_menu);
		else
			SetState(MenuState.main_menu);

		fromPauseMenu = false;
	}

	public void EnterInGameFromLoadingScreen()
	{
		SetState(MenuState.in_game);
		if (deathMenu.activeSelf == true)
			DisableDeathMenu();
	}

    // callback for ui button
    public void OpenDiscordLink()
    {
        Application.OpenURL(Config.DISCORD_URL);
    }

    public void OpenBuyMeACoffeeLink()
    {
        Application.OpenURL(Config.BUY_ME_A_COFFEE_URL);
    }
}
