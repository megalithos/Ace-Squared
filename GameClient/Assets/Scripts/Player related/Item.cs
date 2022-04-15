using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item : MonoBehaviour, System.ICloneable
{
	public Vector3 position;
	public GameObject prefab;
	public KeyCode keyToPress;
	static List<GameObject> prefabArr = new List<GameObject>();
	ItemManager itemManager;

	public bool isGun;
	//public int bulletsLeft;
	public int bulletsInMag;
	public int magsLeft;
	public float fireRate;
	public float reloadTimeInSeconds;
	public bool isFullAuto;
	public string killfeedName;
	private int id;
	public AudioClip gunShootSound;
	AudioSource audioSrc;
	public float gunRecoilUp;
	public float gunRecoilSide;
	public bool playBulletTrailOnFire = true;
	[Tooltip("Deviation for calculating spread. Set zero if you don't want any spread.")]
	public float maxDeviation;
	[HideInInspector]
	public Animator anim;

	void Awake()
	{
		prefabArr.Add(prefab);
		itemManager = GetComponentInParent<ItemManager>();
		audioSrc = GetComponentInParent<AudioSource>();

		if (audioSrc == null)
			Debug.Log("cant find audiosrc");

		if (fireRate == 0)
			fireRate = 1f; // so user doesn't have to set it to 1f, if it is semi auto sniper etc.

		// for spade and maybe others, if need to play animations such as
		// spade swing when breaking blocks
		anim = GetComponent<Animator>();

		// for the spade, if it is null, search for the Animator component in children
		if (anim == null)
		{
			anim = GetComponentInChildren<Animator>();
		}
	}

	public object Clone()
	{
		return this.MemberwiseClone();
	}

	/// <summary>
	/// Assumes that first Item object instantiated is at index 0, second at index 1, ...
	/// </summary>
	/// <param name="id"></param>
	/// <returns></returns>
	public static GameObject PrefabByID(int id)
	{
		return prefabArr[id];
	}

	public void PlayShootingSound()
	{
		if (gunShootSound != null)
			audioSrc.PlayOneShot(gunShootSound);
		else
			Debug.Log("gunShootSound is not referenced");
	}

	#region Getters_and_Setters
	public int GetID()
	{
		return id;
	}

	public void SetID(int _id)
	{
		id = _id;
	}

	public GameObject GetPrefab()
	{
		return prefab;
	}

	public Vector3 GetPos()
	{
		return position;
	}
	#endregion
}
