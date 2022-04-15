using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public int id;
    public string username;
    public float health;
    public float maxHealth = 100f;
    [HideInInspector]
    public ParticleSystem muzzleFlash;
    Rigidbody rb;
    public GameObject handToRotate1;
    public GameObject handToRotate2;
    public GameObject headToRotate;
    public Transform rightHandPos;
    public Transform rightHand;
    public bool crouching = false;
    private Animator animator;
    HUDManager hudManager;
    public Vector3 lookDir;

    [HideInInspector]
    public bool isLocalPlayer;

    Dictionary<int, Material> teamIDAndMaterial = new Dictionary<int, Material>();
    public Material greenTeamMaterial;
    public Material blueTeamMaterial;

    public List<GameObject> objectsToChangeTheColorOfBasedOnTeamColor;

    [HideInInspector]
    public bool isDead;
    ItemManager itemManager;
    UIManager _UIManager;
    private Vector3 velocity;
    Player player;
    public float offsetCoefficientForColliderPositioning = 0.7f;
    public float offsetCoefficientForColliderHeightChange = 0.7f;
    private CapsuleCollider collider; // reference to player's collider
    private Vector3 collOriginalPosition;
    private float collOriginalHeight;
    private float coeffForDistanceBetweenColliderAndCamera;

    [HideInInspector]
    public bool isCorridorWideEnough = true;
    [HideInInspector]
    public bool corridorCheckFailed = false;
    Transform cam;

    [HideInInspector]
    int teamID = 0;

    AudioSource playerAudioSource;
    public AudioClip footstepSound;

    public void SetTeamID(int toSet)
    {
        teamID = toSet;
    }

    public void Initialize(int _id, string _username, int _teamID)
    {
        id = _id;
        if (_username.Length == 0)
        {
            username = "player_" + id;
        }
        else
            username = _username;

        health = maxHealth;
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
        isLocalPlayer = GetComponent<Player>() != null;

        collider = GetComponent<CapsuleCollider>();
        if (isLocalPlayer)
        {
            player = GetComponent<Player>();
            cam = GameObject.Find("Main Camera").transform;
            coeffForDistanceBetweenColliderAndCamera = Mathf.Abs(cam.localPosition.y - collider.center.y);

        }

        isDead = false;
        _UIManager = GameObject.FindWithTag("UIManager").GetComponent<UIManager>();
        itemManager = GetComponent<ItemManager>();
        hudManager = GameObject.FindWithTag("HudManager").GetComponent<HUDManager>();
        collOriginalHeight = collider.height;
        collOriginalPosition = collider.center;
        teamID = _teamID;

        if (!isLocalPlayer)
        {
            teamIDAndMaterial.Add(0, greenTeamMaterial);
            teamIDAndMaterial.Add(1, blueTeamMaterial);
            ApplyMaterialsToBodyParts();
        }
        if (isLocalPlayer)
        {
            GameObject.FindWithTag("GameManager").GetComponent<World>().player = gameObject.transform;
        }
        playerAudioSource = GetComponent<AudioSource>(); // get reference to the audio source in player object for playing footstep sounds
        StartCoroutine(footstepSoundLoop());
    }

    void ApplyMaterialsToBodyParts()
    {
        foreach (GameObject item in objectsToChangeTheColorOfBasedOnTeamColor)
        {
            Debug.Log("trying key ." + teamID);
            item.GetComponent<SkinnedMeshRenderer>().material = teamIDAndMaterial[teamID];
        }
    }

    GameObject GetChildWithName(GameObject obj, string name)
    {
        Transform trans = obj.transform;
        Transform childTrans = trans.Find(name);
        if (childTrans != null)
        {
            return childTrans.gameObject;
        }
        else
        {
            return null;
        }
    }


    public void SetHealth(float _health)
    {
        health = _health;

        Debug.Log("set my helth to " + _health);
        if (health <= 0f)
        {
            ClientSideDie();
        }

        int hpToSet = (int)_health;
        if (hpToSet < 0)
            hpToSet = 0;
        if (isLocalPlayer)
            hudManager.SetHpText(hpToSet);
    }

    bool previousCrouchingState = false;
    public void FixedUpdate()
    {
        animator.SetBool("isCrouching", crouching);
        if (!isLocalPlayer)
        {
            if (previousCrouchingState == crouching)
                return;

            previousCrouchingState = crouching;

            if (crouching)
            {
                Crouch(true);
            }
            else
            {
                Crouch(false);
            }

        }
    }

    /// <summary>
    /// Check if player can uncrouch. When we are in narrow hole -> check if we can crouch so we don't crouch
    /// through blocks. FOR LOCAL PLAYER ONLY
    /// </summary>
    /// <returns></returns>
    public bool CorridorIsWideEnough()
    {
        Vector3 startPt = rb.position;
        startPt.y += collider.radius;
        Vector3 endPt = rb.position;
        endPt.y += collOriginalHeight - collider.radius;
        RaycastHit[] hits = Physics.CapsuleCastAll(startPt, endPt, collider.radius * 0.95f, Vector3.up, 0.1f); // coeff 0.95f is so the check capsule is just little bit smaller than the player's capsule.
        foreach (RaycastHit hit in hits)
        {
            Debug.Log("hit: " + hit.transform.tag);
            if (hit.transform.tag == "worldterrain")
                return false;
        }
        return true;
    }

    /// <summary>
    /// Enable or disable crouching based on crouch variable. Also change the player's collider height
    /// and position to match crouching approximately.
    /// </summary>
    /// <param name="crouch"></param>
    public void Crouch(bool crouch)
    {

        if (crouch) // this code block enables crouch
        {
            crouching = true;
            collider.center = collider.center * offsetCoefficientForColliderPositioning;
            collider.height = collOriginalHeight * offsetCoefficientForColliderHeightChange;

            if (isLocalPlayer)
            {
                cam.localPosition = new Vector3(0f, collider.center.y + coeffForDistanceBetweenColliderAndCamera * offsetCoefficientForColliderHeightChange, 0f);
                player.speed = player.crouchSpeed;
            }
        }
        else if (!crouch) // this code block disables crouch
        {
            crouching = false;
            collider.center = collOriginalPosition;
            collider.height = collOriginalHeight;

            if (isLocalPlayer)
            {
                cam.localPosition = new Vector3(0f, collider.center.y + coeffForDistanceBetweenColliderAndCamera, 0f);
                player.speed = player.walkSpeed;
            }
        }
    }


    public int interpolationFramesCount = 10; // Number of frames to completely interpolate between the 2 positions
    int elapsedFrames = 0;

    void Update()
    {
        if (isLocalPlayer || isDead)
            return;

        float interpolationRatio = (float)elapsedFrames / interpolationFramesCount;

        Vector3 interpolatedPosition = Vector3.Lerp(transform.position, posToMoveTo, interpolationRatio);

        elapsedFrames = (elapsedFrames + 1) % (interpolationFramesCount + 1);  // reset elapsedFrames to zero after it reached (interpolationFramesCount + 1)

        transform.position = interpolatedPosition;
    }

    public void LateUpdate()
    {
        if (this != null && !isLocalPlayer)
        {
            RotateHeadAndHands();
        }
    }

    Quaternion savedRotation;
    Vector3 posToMoveTo;

    void RotateHeadAndHands()
    {
        // calculate angle to rotate hand
        float angleDiff1 = Vector3.Angle(lookDir, handToRotate1.transform.forward);
        float angleDiff2 = Vector3.Angle(lookDir, handToRotate2.transform.forward);

        // rotate hand
        handToRotate1.transform.Rotate(Vector3.left, angleDiff1);
        handToRotate1.transform.Rotate(Vector3.left, -90f); // extra -90 deg to align properly

        // rotate hand
        handToRotate2.transform.Rotate(new Vector3(-1, 1, 0), angleDiff1);
        handToRotate2.transform.Rotate(new Vector3(-1, 1, 0), -90f); // extra -90 deg to align properly

        // rotate head
        headToRotate.transform.rotation = Quaternion.LookRotation(lookDir);
    }

    // for rigidbody position and interpolation options
    public void SetPosition(Vector3 pos)
    {
        //rb.MovePosition(pos);

        posToMoveTo = pos;
    }

    public void ClientSideDie()
    {
        animator.SetBool("isDead", true);
        if (isLocalPlayer)
        {
            // 1. lock player's controls
            isDead = true;
            savedRotation = transform.localRotation;
            rb.useGravity = false;
            rb.velocity = Vector3.zero;
            _UIManager.EnableDeathMenu();
            itemManager.cancelNextReload = true;
            if (itemManager.reloadRoutine != null)
                StopCoroutine(itemManager.reloadRoutine);
            if (itemManager.audioSrc != null && itemManager.audioSrc.isPlaying)
                itemManager.audioSrc.Stop();
        }
        else
        {
            rb.velocity = Vector3.zero;

        }

    }

    public void Respawn(Vector3 spawnPosition)
    {
        animator.SetBool("isDead", false);
        if (isLocalPlayer)
        {
            GetComponent<Player>().Respawn(spawnPosition);
            World.instance.ResetViewDistanceChunks();
            rb.velocity = Vector3.zero;
            transform.localRotation = savedRotation;
            rb.freezeRotation = true;
            isDead = false;
            rb.useGravity = true;
            SetHealth(maxHealth);
            _UIManager.DisableDeathMenu();
            itemManager.ClearCurrentLifeBulletDataDictionary();
            itemManager.RefreshAmmoEtc();
            itemManager.cancelNextReload = false;
            Crouch(false);
            hudManager.SetScopeOverlayActive(false);
            GameObject.FindWithTag("OverlayCamera").GetComponent<Camera>().enabled = true;
            Debug.Log("Respawning");
            itemManager.RefreshItem(0);
        }
        else
        {
            rb.velocity = Vector3.zero;
            ApplyMaterialsToBodyParts();
        }

    }

    public void PlayMuzzleFlash()
    {
        if (muzzleFlash != null)
        {
            muzzleFlash.Play(); // important to call muzzleflash first so we set the currentlyselected item to correct one and then
            if (!isLocalPlayer)
                itemManager.PlayItemSoundForCurrentlySelectedItem();
        }
        else
            Debug.LogError("Can't find reference to muzzleFlash");
    }

    public void PlayAnimation(int id)
    {
        if (id == ((int)ItemManager.AnimationTypes.spade_swing))
        {
            if (!isLocalPlayer)
                itemManager.PlaySpadeSwingAndSound();
            else
                throw new System.Exception("Why tf is local player getting these callbacks???");
        }
    }

    public void SetVelocity(Vector3 _vel)
    {
        velocity = _vel;
    }

    IEnumerator footstepSoundLoop()
    {
        while (true)
        {
            if (velocity.magnitude > 4f && !crouching && Mathf.Abs(velocity.y) < 0.5f && !isDead)
            {
                playerAudioSource.PlayOneShot(footstepSound);
            }
            yield return new WaitForSeconds(0.6f);
        }
    }
}
