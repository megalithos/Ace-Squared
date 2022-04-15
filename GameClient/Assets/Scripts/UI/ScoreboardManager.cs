using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScoreboardManager : MonoBehaviour
{
	public static ScoreboardManager instance;
	public GameObject ScoreboardItemPrefab;
	public GameObject[] playerStatContainers = new GameObject[2];

	Dictionary<int, ScoreboardItem> idItemDictionary = new Dictionary<int, ScoreboardItem>();

	public void Awake()
	{
		if (instance == null)
			instance = this;
		else
			Destroy(this);
	}

	private void Start()
	{
		GameManager.instance.OnPlayerSpawnedEvent += OnPlayerSpawned;
		GameManager.instance.OnPlayerKilled_ += OnPlayerKilled;
		GameManager.instance.OnTeamChangeEvent += OnPlayerTeamChange;
		GameManager.instance.OnPlayerDisconnectedEvent += OnPlayerDisconnected;
	}

	public void ResetScoreBoard()
	{
		List<int> toRemove = new List<int>();
		foreach (int item in idItemDictionary.Keys)
		{
			toRemove.Add(item);
		}

		foreach (var a in toRemove)
		{
			Destroy(idItemDictionary[a].gameObject);
		}
		idItemDictionary.Clear();
	}

	void OnPlayerDisconnected(int id)
	{
		Destroy(idItemDictionary[id].GetComponent<ScoreboardItem>().gameObject);
		idItemDictionary.Remove(id);
	}

	void OnPlayerSpawned(string spawnPlayerUsername, int kills, int deaths, int id, int teamID)
	{
		GameObject instantiated = Instantiate(ScoreboardItemPrefab, playerStatContainers[teamID].transform);

		// get reference to the script from the instantiated object
		ScoreboardItem item = instantiated.GetComponent<ScoreboardItem>();

		item.killsText.text = kills.ToString();
		item.nameText.text = spawnPlayerUsername;
		item.deathsText.text = deaths.ToString();
		item.idText.text = id.ToString();

		idItemDictionary.Add(id, item);
		SortScoreboardByKills();
	}

	// Change the player's (who switched team), scoreboardItem's parent to the correct team
	// So it will be on the right side of the scoreboard
	void OnPlayerTeamChange(int changerID, int toTeam)
	{
		idItemDictionary[changerID].gameObject.transform.parent = playerStatContainers[toTeam].transform;
		SortScoreboardByKills();
	}

	void OnPlayerKilled(int killerID, int weaponID, bool killedByHeadshot, int killedID)
	{
		if (killerID != killedID)
			// add +1 kill to the killer's text
			idItemDictionary[killerID].killsText.text = (System.Convert.ToInt32(idItemDictionary[killerID].killsText.text) + 1).ToString();
		else // if the player suicides, remove one kill from his kills
			idItemDictionary[killerID].killsText.text = (System.Convert.ToInt32(idItemDictionary[killerID].killsText.text) - 1).ToString();


		// add +1 to the killed player's death text
		idItemDictionary[killedID].deathsText.text = (System.Convert.ToInt32(idItemDictionary[killedID].deathsText.text) + 1).ToString();
		SortScoreboardByKills();
	}

	// simply sort the scoreboard so that the player with most kills is the upmost player
	// in each of the stat containers
	void SortScoreboardByKills()
	{
		foreach (GameObject obj in playerStatContainers)
		{
			List<KeyValuePair<int, int>> myList = new List<KeyValuePair<int, int>>();

			foreach (Transform childTransform in obj.transform)
			{
				KeyValuePair<int, int> toAdd = new KeyValuePair<int, int>(System.Convert.ToInt32(childTransform.GetComponent<ScoreboardItem>().idText.text), System.Convert.ToInt32(childTransform.GetComponent<ScoreboardItem>().killsText.text));
				myList.Add(toAdd);
			}

			myList.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));

			foreach (KeyValuePair<int,int> kvp in myList)
			{
				idItemDictionary[kvp.Key].gameObject.transform.SetAsFirstSibling();
			}
		}
	}
}

