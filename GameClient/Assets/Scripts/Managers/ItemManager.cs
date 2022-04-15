using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ValidItems
{
    spade,
    bluePrint,
    mp5_smg,
    sniperRifle,
    m249,
    rocketLauncher,
    doubleBarrel,
    c4,
    mgl
}

// script controlling what we are holding in our hand and when.
public class ItemManager : MonoBehaviour
{

    private int currentlySelected = -1;
    public Transform cam;
    public Transform rightHandParentForNonLocalPlayer;

    private PlayerManager playerManager;
    private GameObject currentlySelectedObj;
    private GameObject mainCam;
    private HUDManager hudManager;
    private Player player;
    private ClassManager classManager;
    public List<GameObject> items = new List<GameObject>();
    public List<GameObject> nonLocalPlayerItems = new List<GameObject>();
    [Space(10)]
    public GameObject rocketBulletPrefab;
    public AudioClip reloadingSound;
    public AudioClip hitmarkSound;
    public GameObject breakBlockPrefab;
    public GameObject mglGrenadePrefab;
    public GameObject placeBlockPrefab;
    Vector3 camPosForNonLocalPlayer = new Vector3(0f, 2.201f, 0f);

    float fov;
    [HideInInspector]
    public AudioSource audioSrc;
    Item currentlySelectedItem;
    GameObject gameManager;
    World world;

    public static List<GameObject> lst;

    // for seleting items/weapons
    public readonly Dictionary<KeyCode, int> keybinds = new Dictionary<KeyCode, int>(){
        {KeyCode.Alpha1, 0}, // select 1st item (primary weapon)
		{KeyCode.Alpha2, 1}, // select 2nd item (secondary weapon)
		{KeyCode.Alpha3, 2}, // ...
		{KeyCode.Alpha4, 3}
    };

    void Start()
    {
        audioSrc = GameManager.localPlayer.GetComponent<AudioSource>();
        playerManager = GetComponent<PlayerManager>();
        player = GetComponent<Player>();
        AssignItemIDs();
        classManager = GetComponent<ClassManager>();
        hudManager = GameObject.FindWithTag("HudManager").GetComponent<HUDManager>();
        gameManager = GameObject.Find("GameManager");

        world = gameManager.GetComponent<World>();
        //hudManager.UpdateAmmoText(currentlySelectedWeaponData.bulletsLeft, currentlySelectedWeaponData.magsLeft);
        if (playerManager.isLocalPlayer)
            mainCam = GameObject.Find("Main Camera");
        else
        {

        }

        foreach (GameObject obj in nonLocalPlayerItems)
        {
            SetLayerRecursively(obj, 0);
        }

        if (playerManager.isLocalPlayer)
            fov = mainCam.GetComponent<Camera>().fieldOfView;

        if (lst == null && playerManager.isLocalPlayer) // we wanna get these from localplayer so we don't get incorrect values
            lst = items;
    }

    bool firstTimeResettingBulletsAlive = true;
    int previouslySelectedID = -1; Vector3 kickbackPos = Vector3.zero;
    //bool canSwitchItem = true; // for making a small cooldown when changing items, and hopefully preventing item duplication when instantiated on local player
    //float itemSwitchCooldownTime = 0.2f;

    void Update()
    {
        if (playerManager == null)
            return;

        // we don't wanna run these things if we are not the local player or we are dead. (e.g. checking if mouse left is pressed)
        if (!playerManager.isLocalPlayer || playerManager.isDead)
            return;

        if (playerManager.isLocalPlayer && player.GetMenuOpen())
            return;

        // update ammoText
        if (currentlySelectedItem != null && currentLifeBulletData.ContainsKey(currentlySelected))
            hudManager.UpdateAmmoText(currentLifeBulletData[currentlySelected].bulletsLeft, currentLifeBulletData[currentlySelected].magsLeft);

        // item changing
        foreach (KeyCode key in keybinds.Keys) // loop through weapon switching keybinds every frame
        {
            int itemID = classManager.GetItemID(keybinds[key]);
            if (Input.GetKeyDown(key) && itemID >= 0)
            {
                RefreshItem(itemID);
                if (reloadRoutine != null)
                    StopCoroutine(reloadRoutine);
                reloading = false;
                if (audioSrc.isPlaying)
                    audioSrc.Stop();

                mainCam.GetComponent<Camera>().fieldOfView = fov;
            }
        }

        // reload
        if (Input.GetKeyDown(KeyCode.R) && !reloading && currentLifeBulletData[currentlySelected].magsLeft > 0 && currentlySelectedItem.isGun && currentLifeBulletData[currentlySelected].bulletsLeft != currentlySelectedItem.bulletsInMag)
        {
            Debug.Log("sending reload");
            if (reloadRoutine != null)
                StopCoroutine(reloadRoutine);

            reloadRoutine = Reload();
            StartCoroutine(reloadRoutine);
        }

        if (currentlySelectedItem != null && currentlySelectedItem.isGun)
        {
            if (currentlySelected == (int)ValidItems.rocketLauncher)
                ShootRocketLauncher();
            else if (currentlySelected == (int)ValidItems.mgl)
                ShootMgl();
            else if (currentlySelectedItem != null && currentlySelected == (int)ValidItems.c4)
            {
                ThrowC4();
            }
            else
                Shoot();
        }
        else if (currentlySelectedItem != null && currentlySelected == (int)ValidItems.spade)
        {
            MineBlock();
        }
        else if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("Currently selected: " + currentlySelected);
            switch (currentlySelected)
            {
                case (int)ValidItems.bluePrint: // case where we are holding block (to place blocks)
                    ClientSend.PlayerEditBlock(mainCam.transform.forward, 1);
                    break;
            }
        }

        if (currentlySelectedObj != null)
        {
            // when shot, set slerp pos to kickbackPos
            currentlySelectedObj.transform.localPosition = Vector3.Slerp(currentlySelectedObj.transform.localPosition, currentlySelectedObj.transform.localPosition + kickbackPos, 10f * Time.deltaTime);

            // when slerp is close enough to that, start lerping back to startPos
            if (Vector3.Distance(currentlySelectedObj.transform.localPosition, currentlySelectedObj.transform.localPosition + kickbackPos) < 1f)
            {
                kickbackPos = Vector3.zero;
            }
        }
        SetBreakAndPlaceBlockPositions(mainCam.transform.forward);
    }

    public void SetSelectedItem(int id)
    {
        if (id >= 0) // if we wanna set it to non local players
        {
            UpdateHandItemForNonLocalPlayer(id);
        }
    }

    Dictionary<int, BulletData> currentLifeBulletData = new Dictionary<int, BulletData>();

    [HideInInspector]
    public bool alreadySelectedWeaponThisLife = false;
    public void RefreshItem(int itemID)
    {
        // remove currently the previous obj
        if (currentlySelectedObj != null)
        {
            Destroy(currentlySelectedObj);
        }

        // no matter which item we choose, we want to reset any pending reloads

        Item item = items[itemID].GetComponent<Item>();

        // self explainatory
        currentlySelected = itemID;

        // instantiate the correct item's prefab
        currentlySelectedObj = Instantiate(item.prefab, cam, false);

        SetLayerRecursively(currentlySelectedObj, LayerMask.NameToLayer("HandItemLayer"));
        if (currentLifeBulletData.ContainsKey(currentlySelected) && currentlySelected == (int)ValidItems.c4 && currentLifeBulletData[currentlySelected].bulletsLeft == 0)
        {
            currentlySelectedObj.SetActive(false);
        }

        // set currentlySelectedItem's reference to currently selected item prefab's Item component
        currentlySelectedItem = currentlySelectedObj.GetComponent<Item>();

        // notify other clients that we changed weapon
        ClientSend.PlayerSelectedItemChange(currentlySelected);

        // if the item is gun, add reference to it's muzzle flash
        if (item.isGun)
        {
            nextTimeToFire = 0f;
            playerManager.muzzleFlash = currentlySelectedObj.GetComponentInChildren<ParticleSystem>();

            RefreshAmmoEtc();
        }
        else if (currentlySelected == (int)ValidItems.spade)
        {
            if (breakBlock == null)
            {
                breakBlock = Instantiate(breakBlockPrefab);
            }
        }
        else if (currentlySelected == (int)ValidItems.bluePrint)
        {
            if (placeBlock == null)
            {
                placeBlock = Instantiate(placeBlockPrefab);
            }
        }

        if (previouslySelectedID == (int)ValidItems.spade && currentlySelected != (int)ValidItems.spade)
        {
            Destroy(breakBlock);
            breakBlock = null;
        }
        else if (previouslySelectedID == (int)ValidItems.bluePrint && currentlySelected != (int)ValidItems.bluePrint)
        {
            Destroy(placeBlock);
            placeBlock = null;
        }

        previouslySelectedID = currentlySelected;
    }

    public void RefreshAmmoEtc()
    {
        if (!currentLifeBulletData.ContainsKey(currentlySelected) && currentlySelectedItem != null)
        {
            // we have not yet chosen that weapon this life so reset it's ammo
            currentLifeBulletData.Add(currentlySelected, new BulletData(currentlySelected, currentlySelectedItem.bulletsInMag, currentlySelectedItem.bulletsInMag, currentlySelectedItem.magsLeft));
        }
    }

    public void ClearCurrentLifeBulletDataDictionary()
    {
        currentLifeBulletData.Clear();
    }

    #region shootingLogic
    bool reloading = false;
    float nextTimeToFire = 0f;
    delegate bool ConditionForFiring(int a);

    bool Fire(ConditionForFiring condMethod, int a)
    {
        return condMethod(a);
    }
    public GameObject bulletPrefab;
    public bool cancelNextReload = false;
    public float maxDeviation = 5f;

    void MineBlock()
    {

        ConditionForFiring cond;

        if (currentlySelectedItem.isFullAuto)
        {
            cond = Input.GetMouseButton;
        }
        else
        {
            cond = Input.GetMouseButtonDown;
        }

        if (Fire(cond, 0) && Time.time >= nextTimeToFire && !reloading) // this is ran everytime we shoot a bullet
        {
            ClientSend.PlayerEditBlock(mainCam.transform.forward, 0);
            ClientSend.PlayerAnimationTrigger((int)AnimationTypes.spade_swing);
            nextTimeToFire = Time.time + 1f / currentlySelectedItem.fireRate;
            PlaySpadeSwingAndSound();
        }
    }

    void ThrowC4()
    {
        ConditionForFiring cond;

        if (currentlySelectedItem.isFullAuto)
        {
            cond = Input.GetMouseButton;
        }
        else
        {
            cond = Input.GetMouseButtonDown;
        }

        if (Fire(cond, 0) && Time.time >= nextTimeToFire && !reloading && currentLifeBulletData[currentlySelected].bulletsLeft > 0) // this is ran everytime we shoot a bullet
        {
            nextTimeToFire = Time.time + 1f / currentlySelectedItem.fireRate;
            //currentlySelectedItem.PlayShootingSound();

            currentLifeBulletData[currentlySelected].bulletsLeft--;

            // destroy
            Vector3 loc = currentlySelectedObj.transform.position;
            Quaternion rot = currentlySelectedObj.transform.rotation;
            Vector3 scale = currentlySelectedObj.transform.localScale;

            currentlySelectedObj.SetActive(false);

            // instantiate to hand in the same location and rotation
            GameObject instantiatedC4 = Instantiate(currentlySelectedItem.prefab, mainCam.transform.position, rot);
            instantiatedC4.transform.localScale = scale;
            Rigidbody rb = instantiatedC4.GetComponent<Rigidbody>();
            instantiatedC4.SetActive(true);
            SetLayerRecursively(instantiatedC4, LayerMask.NameToLayer("c4Layer"));
            // addforce to it
            rb.isKinematic = false;
            rb.AddForce(cam.transform.forward * 150f);
            instantiatedC4.GetComponent<c4OnCollision>().Throw();

            ClientSend.PhysicsShoot(mainCam.transform.forward, mainCam.transform.position, rot);
        }

        // update hud ammo count
        if (currentLifeBulletData.ContainsKey(currentlySelected))
            hudManager.UpdateAmmoText(currentLifeBulletData[currentlySelected].bulletsLeft, currentLifeBulletData[currentlySelected].magsLeft);
    }

    void Shoot()
    {

        ConditionForFiring cond;

        if (currentlySelectedItem.isFullAuto)
        {
            cond = Input.GetMouseButton;
        }
        else
        {
            cond = Input.GetMouseButtonDown;
        }

        if (Fire(cond, 0) && Time.time >= nextTimeToFire && !reloading && currentLifeBulletData[currentlySelected].bulletsLeft > 0) // this is ran everytime we shoot a bullet
        {
            //////////////////////////////////////////////////
            // for calculating spread
            Vector3 forwardVector = Vector3.forward;
            float deviation = Random.Range(0f, currentlySelectedItem.maxDeviation);
            float angle = Random.Range(0f, 360f);
            forwardVector = Quaternion.AngleAxis(deviation, Vector3.up) * forwardVector;
            forwardVector = Quaternion.AngleAxis(angle, Vector3.forward) * forwardVector;
            forwardVector = mainCam.transform.rotation * forwardVector;
            /////////////////////////////////////////////////

            player.AddRecoil(currentlySelectedItem.gunRecoilUp, currentlySelectedItem.gunRecoilSide);
            nextTimeToFire = Time.time + 1f / currentlySelectedItem.fireRate;
            currentlySelectedItem.PlayShootingSound();
            playerManager.PlayMuzzleFlash();

            if (currentlySelectedItem.playBulletTrailOnFire)
                SendBulletTrail(forwardVector, playerManager.muzzleFlash.transform.position, currentlySelected);


            ClientSend.PlayerShoot(forwardVector);
            currentLifeBulletData[currentlySelected].bulletsLeft--;


            kickbackPos = Vector3.back * 0.5f;
        }

        // update hud ammo count
        hudManager.UpdateAmmoText(currentLifeBulletData[currentlySelected].bulletsLeft, currentLifeBulletData[currentlySelected].magsLeft);
    }

    public GameObject c4Prefab;
    void ShootRocketLauncher()
    {
        ConditionForFiring cond;

        if (currentlySelectedItem.isFullAuto)
        {
            cond = Input.GetMouseButton;
        }
        else
        {
            cond = Input.GetMouseButtonDown;
        }

        if (Fire(cond, 0) && Time.time >= nextTimeToFire && !reloading && currentLifeBulletData[currentlySelected].bulletsLeft > 0) // this is ran everytime we shoot a bullet
        {
            player.AddRecoil(currentlySelectedItem.gunRecoilUp, currentlySelectedItem.gunRecoilSide);
            nextTimeToFire = Time.time + 1f / currentlySelectedItem.fireRate;

            currentlySelectedItem.PlayShootingSound();
            //playerManager.PlayMuzzleFlash();

            SendBulletTrail(mainCam.transform.forward, playerManager.muzzleFlash.transform.position, currentlySelected);
            Debug.Log("fired rocket launcher");
            //ClientSend.PlayerShoot(mainCam.transform.forward);


            ClientSend.PhysicsShoot(mainCam.transform.forward, playerManager.muzzleFlash.transform.position, Quaternion.identity);
            currentLifeBulletData[currentlySelected].bulletsLeft--;
            kickbackPos = Vector3.back * 0.5f;
        }

        // update hud ammo count
        if (currentLifeBulletData.ContainsKey(currentlySelected))
            hudManager.UpdateAmmoText(currentLifeBulletData[currentlySelected].bulletsLeft, currentLifeBulletData[currentlySelected].magsLeft);
    }

    void ShootMgl()
    {
        ConditionForFiring cond;

        if (currentlySelectedItem.isFullAuto)
        {
            cond = Input.GetMouseButton;
        }
        else
        {
            cond = Input.GetMouseButtonDown;
        }

        if (Fire(cond, 0) && Time.time >= nextTimeToFire && !reloading && currentLifeBulletData[currentlySelected].bulletsLeft > 0) // this is ran everytime we shoot a bullet
        {
            player.AddRecoil(currentlySelectedItem.gunRecoilUp, currentlySelectedItem.gunRecoilSide);
            nextTimeToFire = Time.time + 1f / currentlySelectedItem.fireRate;

            currentlySelectedItem.PlayShootingSound();

            SendBulletTrail(mainCam.transform.forward, playerManager.muzzleFlash.transform.position, currentlySelected);

            ClientSend.PhysicsShoot(mainCam.transform.forward, playerManager.muzzleFlash.transform.position, Quaternion.identity);
            currentLifeBulletData[currentlySelected].bulletsLeft--;
            kickbackPos = Vector3.back * 0.5f;
        }

        // update hud ammo count
        if (currentLifeBulletData.ContainsKey(currentlySelected))
            hudManager.UpdateAmmoText(currentLifeBulletData[currentlySelected].bulletsLeft, currentLifeBulletData[currentlySelected].magsLeft);
    }
    #endregion

    public void PlaySpadeSwingAndSound()
    {
        Animator animReference = null;
        if (playerManager.isLocalPlayer)
        {
            Debug.Log("should play swing");
            currentlySelectedItem.anim.SetTrigger("isBreakingBlocks");
            currentlySelectedItem.PlayShootingSound();
        }
        else
        {
            nonLocalPlayerItems[currentlySelected].GetComponentInChildren<Item>().anim.SetTrigger("isBreakingBlocks");
            nonLocalPlayerItems[currentlySelected].GetComponent<Item>().PlayShootingSound();

            Debug.Log("is anim ref null?: " + nonLocalPlayerItems[currentlySelected].GetComponent<Item>().anim == null);
        }
    }

    private float checkIncrement = 0.1f;
    public float reach = 4f;
    GameObject breakBlock;
    GameObject placeBlock;
    public void SetBreakAndPlaceBlockPositions(Vector3 facing)
    {
        if (playerManager.isDead) // if we are on delay we don't want to edit the block
            return;

        float step = checkIncrement;
        Vector3 lastPos = new Vector3();
        Vector3 breakPosition = new Vector3();
        Vector3 placePosition = new Vector3();

        while (step < reach)
        {
            Vector3 pos = (mainCam.transform.position) + (facing * step);
            if (breakBlock != null)
                breakBlock.SetActive(true);
            if (placeBlock != null)
                placeBlock.SetActive(true);
            if (world.CheckForVoxel(pos))
            {

                breakPosition = new Vector3(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
                placePosition = lastPos;

                if (currentlySelected == (int)ValidItems.spade)
                {
                    if (breakBlock != null)
                    {
                        breakBlock.transform.position = breakPosition;
                    }
                    break;
                }
                else
                {
                    if (placeBlock != null)
                    {
                        placeBlock.transform.position = placePosition;
                    }
                    break;
                }
            }
            else
            {
                if (breakBlock != null)
                    breakBlock.SetActive(false);
                if (placeBlock != null)
                    placeBlock.SetActive(false);
            }
            lastPos = new Vector3(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
            step += checkIncrement;
        }
    }

    // also check if we hit other player
    public void SendBulletTrail(Vector3 dir, Vector3 fromPos, int itemID, Quaternion rot = new Quaternion())
    {
        if (playerManager != null && playerManager.isLocalPlayer)
        {
            if (itemID == (int)ValidItems.rocketLauncher)
            {
                GameObject obj = Instantiate(rocketBulletPrefab, fromPos, mainCam.transform.rotation);
                obj.transform.localRotation = Quaternion.Euler(obj.transform.eulerAngles.x + 180, obj.transform.eulerAngles.y, obj.transform.eulerAngles.z);
                obj.GetComponent<Rigidbody>().AddForce(mainCam.transform.forward * 2000f);
            }
            else if (itemID == (int)ValidItems.mgl)
            {
                GameObject obj = Instantiate(mglGrenadePrefab, fromPos, mainCam.transform.rotation);
                //obj.transform.localRotation = Quaternion.Euler(obj.transform.eulerAngles.x + 180, obj.transform.eulerAngles.y, obj.transform.eulerAngles.z);
                obj.GetComponent<Rigidbody>().AddForce(mainCam.transform.forward * 1000f);
            }
            else
            {
                GameObject obj = Instantiate(bulletPrefab, fromPos, mainCam.transform.rotation);
                //obj.GetComponent<Rigidbody>().AddForce(dir * 10000f);

                RaycastHit hit;
                float distToMoveBullet = 0f;
                if (Physics.Raycast(new Ray(mainCam.transform.position, dir), out hit, 100f) == true)
                {
                    distToMoveBullet = hit.distance;

                    // play hitmark sound if hit other player (don't if he is dead)
                    if (hit.collider.CompareTag("Player") && !hit.collider.GetComponent<PlayerManager>().isDead)
                    {
                        audioSrc.PlayOneShot(hitmarkSound);
                    }
                }
                else
                {
                    distToMoveBullet = 100f;
                }
                obj.GetComponent<MoveBulletToDestination>().SetDestination(dir, distToMoveBullet);

                StartCoroutine(DestroySpawnedBullet(obj));
            }
        }
        else
        {
            if (itemID == (int)ValidItems.rocketLauncher)
            {
                GameObject obj = Instantiate(rocketBulletPrefab, fromPos, Quaternion.identity);
                obj.transform.localRotation = Quaternion.Euler(obj.transform.eulerAngles.x + 180, obj.transform.eulerAngles.y, obj.transform.eulerAngles.z);
                obj.GetComponent<Rigidbody>().AddForce(dir * 1000f);
            }
            else if (itemID == (int)ValidItems.mgl)
            {
                GameObject obj = Instantiate(mglGrenadePrefab, fromPos, Quaternion.identity);
                obj.GetComponent<Rigidbody>().AddForce(dir * 1000f);
            }
            else if (itemID == (int)ValidItems.c4)
            {
                // instantiate to hand in the same location and rotation
                GameObject instantiatedC4 = Instantiate(c4Prefab, fromPos, rot);
                Rigidbody rb = instantiatedC4.GetComponent<Rigidbody>();
                instantiatedC4.SetActive(true);
                // addforce to it
                rb.isKinematic = false;
                rb.AddForce(dir * 150f);
                instantiatedC4.GetComponent<c4OnCollision>().Throw();
            }
            else
            {
                GameObject obj = Instantiate(bulletPrefab, GetComponent<PlayerManager>().muzzleFlash.transform.position, Quaternion.identity);

                RaycastHit hit;
                float distToMoveBullet = 0f;
                if (Physics.Raycast(new Ray(transform.position + camPosForNonLocalPlayer, dir), out hit, 100f) == true)
                {
                    distToMoveBullet = hit.distance;
                }
                else
                {
                    distToMoveBullet = 100f;
                }
                obj.GetComponent<MoveBulletToDestination>().SetDestination(dir, distToMoveBullet);

                StartCoroutine(DestroySpawnedBullet(obj));
            }
        }
    }

    IEnumerator DestroySpawnedBullet(GameObject refToInstantiatedBullet)
    {
        yield return new WaitForSeconds(5f);

        if (refToInstantiatedBullet != null)
            Destroy(refToInstantiatedBullet);
    }

    public void PlayItemSoundForCurrentlySelectedItem()
    {
        if (playerManager != null && playerManager.isLocalPlayer)
        {
            return;
        }

        try
        {
            nonLocalPlayerItems[currentlySelected].GetComponent<Item>().PlayShootingSound();
        }
        catch (System.Exception e)
        {
            Debug.Log(e.Message);
        }

        Debug.Log("Trying to play shooting sound with item id: " + currentlySelected);

    }

    [HideInInspector]
    public IEnumerator reloadRoutine;
    IEnumerator Reload()
    {
        audioSrc.PlayOneShot(reloadingSound);
        reloading = true;
        ClientSend.ReloadGun(); // <- notify server of our reload
        yield return new WaitForSeconds(currentlySelectedItem.reloadTimeInSeconds);

        if (!cancelNextReload)
        {
            if (currentLifeBulletData.ContainsKey(currentlySelected))
            {
                if (currentLifeBulletData[currentlySelected].magsLeft > 0)
                {
                    if (currentlySelected == (int)ValidItems.c4)
                        currentlySelectedObj.SetActive(true);

                    currentLifeBulletData[currentlySelected].magsLeft--;
                    currentLifeBulletData[currentlySelected].bulletsLeft = currentLifeBulletData[currentlySelected].bulletsInMag;
                }
            }
        }
        cancelNextReload = false;
        reloading = false;
        yield break;
    }

    int previouslyActive;

    // network handler for updating hand items will call this method.
    public void UpdateHandItemForNonLocalPlayer(int newID)
    {
        currentlySelected = newID;

        if (currentlySelected != null)
        {

        }
        try
        {
            nonLocalPlayerItems[previouslyActive].SetActive(false);
            nonLocalPlayerItems[currentlySelected].SetActive(true);
        }

        catch (System.Exception e)
        {

        }
        Debug.Log("trying" + currentlySelected);
        if (nonLocalPlayerItems[currentlySelected].GetComponent<Item>().isGun)
        {

            GetComponent<PlayerManager>().muzzleFlash = nonLocalPlayerItems[currentlySelected].GetComponentInChildren<ParticleSystem>();
        }

        previouslyActive = currentlySelected;
    }

    void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (null == obj)
        {
            return;
        }

        obj.layer = newLayer;

        foreach (Transform child in obj.transform)
        {
            if (null == child)
            {
                continue;
            }
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    /// <summary>
    /// Set up the items list with appropriate items with certain, predefined properties.
    /// </summary>
    void AssignItemIDs()
    {
        int count = 0;
        foreach (GameObject item in items)
        {
            item.GetComponent<Item>().SetID(count);
            count++;
        }
    }

    public int GetCurrentlySelected()
    {
        return currentlySelected;
    }

    class BulletData : System.IEquatable<BulletData>
    {
        public int weaponID;
        public int bulletsLeft;
        public int bulletsInMag;
        public int magsLeft;

        public BulletData(int _weaponID, int _bulletsLeft, int _bulletsInMag, int _magsLeft)
        {
            weaponID = _weaponID;
            bulletsLeft = _bulletsLeft;
            bulletsInMag = _bulletsInMag;
            magsLeft = _magsLeft;

        }

        public bool Equals(BulletData other)
        {
            return weaponID == other.weaponID;
        }
    };

    public enum AnimationTypes
    {
        spade_swing,
    }
}
