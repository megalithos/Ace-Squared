using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// hold data about which class can choose what
public class ClassManager : MonoBehaviour
{
	/////////////////////////////////////////////////////////////////////////
	/////////////////////////////////////////////////////////////////////////
	Class[] classes = new Class[]{
		new Class("scout", new int[3] { (int)ValidItems.spade, (int)ValidItems.bluePrint, (int)ValidItems.sniperRifle }),
		new Class("miner", new int[4] { (int)ValidItems.spade, (int)ValidItems.bluePrint, (int)ValidItems.mp5_smg, (int)ValidItems.c4 }),
		new Class("assault", new int[4] { (int)ValidItems.spade, (int)ValidItems.bluePrint, (int)ValidItems.m249, (int)ValidItems.mgl }),
		new Class("kona", new int[4] { (int)ValidItems.spade, (int)ValidItems.bluePrint, (int)ValidItems.rocketLauncher, (int)ValidItems.doubleBarrel })
	};
	int selectedClassID;
	int previousClassID;
	PlayerManager playerManager;
	UIManager _UIManager;
	ItemManager itemManager;
    public static ClassManager instance;

	// for selecting class, set keybinds
	readonly Dictionary<KeyCode, int> keybindsForClassSelection = new Dictionary<KeyCode, int>(){
		{KeyCode.Alpha1, 0}, // select 1st item (primary weapon)
		{KeyCode.Alpha2, 1}, // select 2nd item (secondary weapon)
		{KeyCode.Alpha3, 2}, // ...
		{KeyCode.Alpha4, 3}
	};
	string nameToDisplayEachFrame;
	/////////////////////////////////////////////////////////////////////////
	/////////////////////////////////////////////////////////////////////////
	void Awake()
	{
        if (instance == null)
        {
            instance = this;
        }
        else Destroy(this);

		playerManager = GetComponent<PlayerManager>();
		selectedClassID = Client.instance.firstClassID;
		_UIManager = GameObject.FindWithTag("UIManager").GetComponent<UIManager>();
		itemManager = GetComponent<ItemManager>();
		nameToDisplayEachFrame = classes[selectedClassID].GetName();
	}

	private void OnEnable()
	{
		_UIManager.SetClassSelectorText(nameToDisplayEachFrame);
	}

	private void Update()
	{
		if (!playerManager.isDead)
			return;

		// if we are dead, be able to select class
		foreach (KeyCode key in keybindsForClassSelection.Keys)
		{
			if (Input.GetKeyDown(key))
			{
				// update screen
				if (classes.Length > selectedClassID)
				{
					if (keybindsForClassSelection[key] < classes.Length)
					{
                        // if we reselected the class
                        if (keybindsForClassSelection[key] == previousClassID)
                            return;

						selectedClassID = keybindsForClassSelection[key];

						nameToDisplayEachFrame = classes[selectedClassID].GetName();
						_UIManager.SetClassSelectorText(nameToDisplayEachFrame);
                        ClientSend.SelectedClassID(selectedClassID);
					}
				}
			}
		}

		previousClassID = selectedClassID;
	}

	/// <summary>
	/// Get corresponding item id from keybind id so that everytime keybind equals to corresponding item.
	/// For example: number 1 on the keyboard is always spade.
	/// </summary>
	/// <param name="id"></param>
	/// <returns></returns>
	public int GetItemID(int id)
	{
		int[] arr = GetValidItemsForCurrentClass();

		if (id < arr.Length && arr != null)
			return arr[id];
		else
			return -1;
	}
	#region getters and setters
	public int GetSelectedClass()
	{
		return selectedClassID;
	}

	public int GetPreviousClassID()
	{
		return previousClassID;
	}

	public int GetPrimaryWeaponID()
	{
		return GetValidItemsForCurrentClass()[itemManager.keybinds[KeyCode.Alpha3]];
	}

	int[] GetValidItemsForCurrentClass()
	{
		if (selectedClassID < classes.Length)
			return classes[selectedClassID].GetHoldableItemIDs();
		else
			return null;
	}

	#endregion
}

struct Class
{
	int ID;
	string className;
	int holdableItemIDCount;
	int[] holdableItemIDs;
	static int totalClassObjectsCreated = 0;

	public Class(string _className, int[] _holdableItemIDs)
	{
		ID = totalClassObjectsCreated;
		totalClassObjectsCreated++;

		className = _className;
		holdableItemIDCount = _holdableItemIDs.Length;
		holdableItemIDs = _holdableItemIDs;
	}

	public int[] GetHoldableItemIDs()
	{
		return holdableItemIDs;
	}

	public string GetName()
	{
		return className;
	}
}
