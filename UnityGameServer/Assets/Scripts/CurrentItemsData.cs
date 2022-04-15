using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ValidItems = Player.ValidItems;
using ItemCfg = Config.ItemCfg;

/// <summary>
/// Hold about the item player has selected.
/// </summary>
public class CurrentItemsData : MonoBehaviour
{
    #region Class stuff
    Class[] classes = new Class[]{
        new Class("scout", new int[3] { (int)ValidItems.spade, (int)ValidItems.bluePrint, (int)ValidItems.sniperRifle }),
        new Class("miner", new int[4] { (int)ValidItems.spade, (int)ValidItems.bluePrint, (int)ValidItems.mp5_smg, (int)ValidItems.c4 }),
        new Class("assault", new int[4] { (int)ValidItems.spade, (int)ValidItems.bluePrint, (int)ValidItems.m249, (int)ValidItems.mgl }),
        new Class("kona", new int[4] { (int)ValidItems.spade, (int)ValidItems.bluePrint, (int)ValidItems.rocketLauncher, (int)ValidItems.doubleBarrel })
    };

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

        public bool IsItemInClass(int id)
        {
            for (int i = 0; i < holdableItemIDs.Length; i++)
            {
                if (holdableItemIDs[i] == id)
                    return true;
            }
            return false;
        }
    }

    private Class currentlySelectedClass;
    public byte currentlySelectedClassID { get; private set; }
    private Player player;
    private Anticheat ac;

    public void HandleSelectedClassID(int id)
    {
        if (id < classes.Length && id >= 0)
        {
            currentlySelectedClass = classes[id];
            currentlySelectedClassID = (byte)id;
        }
        else
        {
            Debug.Log("Client submitted invalid class code.");
        }
        
        Debug.Log("Selected class: " + id);
    }
    #endregion

    public void Awake()
    {
        currentlyHoldableItemsData = new Dictionary<ValidItems, ItemCfg>();
        currentlySelectedClass = classes[Config.startingClassID];
        ResetBulletCountEtc();
        player = GetComponent<Player>();
        ac = GetComponent<Anticheat>();
    }

    // so we can access the currently selected item data easily (bullet count etc.)
    public Dictionary<ValidItems, ItemCfg> currentlyHoldableItemsData;

    public void ResetBulletCountEtc()
    {
        currentlyHoldableItemsData.Clear();

        int[] holdableItemIDs = currentlySelectedClass.GetHoldableItemIDs();

        for (int i = 0; i < holdableItemIDs.Length; i++)
        {
            ItemCfg cfg = (ItemCfg)Config.defaultItemSettings[(ValidItems)holdableItemIDs[i]].Clone(); // add default values since we are resetting bullet count etc.
            currentlyHoldableItemsData.Add((ValidItems)holdableItemIDs[i], cfg);
        }

        Debug.Log("Reseted bullet count etc...");
    }

    // for checking if the client sent invalid item for using; should be only possible through packet injection or
    // client modding
    public bool IsItemInHoldableItemsForCurrentClass(int id)
    {
        return currentlySelectedClass.IsItemInClass(id);
    }

    public void HandleReloadGun()
    {
        StartCoroutine(ReloadCurrentWeapon());
    }

    /// <summary>
    /// Reloads the currently selected gun of this player. (sets bullet/mag/etc data correctly)
    /// </summary>
    /// <returns></returns>
    IEnumerator ReloadCurrentWeapon()
    {
        ItemCfg _ref = currentlyHoldableItemsData[(ValidItems)player.selectedItem];
        yield return new WaitForSecondsRealtime(_ref.reloadTimeInSeconds*0.9f);

        if (_ref.magsLeft > 0)
        {
            _ref.magsLeft--;
            _ref.bulletsLeft = _ref.bulletsInMag;
        }
        else
        {
            // player reloading even though he should have no mags left -> ban
            Debug.Log("[AC] ALERT!: player "+player.id+"is reloading mags without any mags left!");
            ac.TriggerPlayerBan();
        }
        yield break;
    }
}
