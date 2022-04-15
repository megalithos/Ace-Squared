using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KillfeedItem : MonoBehaviour
{
	TMPro.TextMeshProUGUI killfeedText;
	enum DeathSource
	{
		spade,
		bluePrint,
		mp5_smg, // thing to place blocks with
		sniperRifle,
		m249,
		rocketLauncher,
		doubleBarrel,
		c4,
		mgl,
		fallOutOfMap,
		fallDamage,
		wall
	}

	public void Awake()
	{
		killfeedText = GetComponentInChildren<TMPro.TextMeshProUGUI>();
		StartCoroutine(DestroyThisAfterAWhile());
	}

    public void Setup(string killerUsername, int weaponID, bool killedByHeadshot, string killedUsername)
	{
		string formatString = "{0}|{1}|{2}{3}";
		string weaponName = "";

		if (weaponID < ItemManager.lst.Count)
			weaponName = ItemManager.lst[weaponID].GetComponent<Item>().killfeedName;
		else
		{
			switch(weaponID)
			{
				case (int)DeathSource.fallOutOfMap:
					weaponName = "Void";
					break;
				case (int)DeathSource.fallDamage:
					weaponName = "fall";
					break;
				case (int)DeathSource.wall:
					weaponName = "wall";
					break;
			}
		}

		string headshotString = "";
		if (killedByHeadshot)
			headshotString = "HS|";

		killfeedText.text = string.Format(formatString, killerUsername, weaponName, headshotString, killedUsername);
	}

	IEnumerator DestroyThisAfterAWhile()
	{
		yield return new WaitForSecondsRealtime(6f);
		Destroy(gameObject);
	}
}
