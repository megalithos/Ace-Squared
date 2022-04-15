using System.Collections;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using LagCompensation;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class Player : MonoBehaviour
{
	static World world;
	public static int explosionRadius = 2;
	public Transform shootOrigin;
	private Queue<ClientInputState> clientInputs = new Queue<ClientInputState>();

	[HideInInspector]
	public Vector3 lookDir;
	[HideInInspector]
	public int id;
	public float health;
	public float maxHealth = 100f;
	public float checkIncrement = 0.1f;
	private float reach = 4f;
	public float minimumDistanceBetweenPlacingPositionAndPlayerXZ = 0.5f;
	public float minimumDistanceBetweenPlacingPositionAndPlayerY = 3f;
	public string username;
	private Vector3 locationToShootRaysForEditingBlocks;
	private Vector3 locationToShootRaysForEditingBlocksStanding = new Vector3(0f, 2.201f, 0f); // should be player camera's position
	private Vector3 locationToShootRaysForEditingBlocksCrouching = new Vector3(0f, 1.65075f, 0f); // should be player camera's position
    [HideInInspector]
	public bool isDead;
	public bool isGrounded;
	public bool isSprinting;
	private bool jumpRequest;
	private static ClientInputState globalInputState;
	private Rigidbody rb;
	private bool canEditBlock;
	public GameObject rocketModel;
	Animator animator;
	public GameObject headToRotate;
	public GameObject c4Prefab;
	public Vector3 velocity;
	Vector3 acceleration;
	public CapsuleCollider noclipCollider;
	// these are important for client prediction. these must be same on both client and the server.
	//----------------------------------- collision checking
	public float distanceFromPlayerPositionToFeet = 0.15f;
	public float distanceFromPlayerPositionToHead = 0.4f;
	public float playerWidth = 0.5f;
	public float boundsTolerance = 0.1f;
	//----------------------------------- jumping/moving
	private float walkSpeed = 3f;
	private float sprintSpeed = 6f;
	private float jumpForce = 5f;
	private float verticalMomentum;
	private float playerSpeedMultiplierForSmootherMovement = 30f;
	//----------------------------------- gravity coefficients, etc.
	private float gravity = -9.81f;
	private float fallCoefficient = 5f;
	//-----------------------------------

	public bool crouching = false;
	private CapsuleCollider collider;
	private float collOriginalHeight;
	private float offsetCoefficientForColliderPositioning = 0.7f;
	private float offsetCoefficientForColliderHeightChange = 0.7f;
	private Vector3 collOriginalPosition;
	private Vector3 noclipCollOriginalPosition;
	private float noclipCollOriginalHeight;
	public int selectedItem = -1; // no item selected by default
	public int selectedBlockColor = 0;
	public int kills, deaths = 0;
    private Anticheat ac;
    private CurrentItemsData currentItemsData;

	[HideInInspector]
	public int teamID = 0;
	[HideInInspector]
	public int wishTeamID = -1; // -1 when not wishing to change team, else when wanting to change

	[HideInInspector]
	public enum ValidItems
	{
		spade,
		bluePrint, // tool to destroy blocks
		mp5_smg, // thing to place blocks with
		sniperRifle,
		m249,
		rocketLauncher,
		doubleBarrel,
		c4,
		mgl
	}

    float highestSpeed = 0f;

	private void Start()
	{
        ac = GetComponent<Anticheat>();
        currentItemsData = GetComponent<CurrentItemsData>();
		world = GameObject.Find("World").GetComponent<World>();
		verticalMomentum = 0f;
		jumpRequest = false;
		canEditBlock = true;
		rb = GetComponent<Rigidbody>();
		collider = GetComponent<CapsuleCollider>();
		animator = GetComponentInChildren<Animator>();

		collOriginalHeight = collider.height;
		collOriginalPosition = collider.center;
		noclipCollOriginalHeight = noclipCollider.height;
		noclipCollOriginalPosition = noclipCollider.center;
		locationToShootRaysForEditingBlocks = locationToShootRaysForEditingBlocksStanding;
        watch.Start();
        currentItemsData.ResetBulletCountEtc();
	}

	public void Initialize(int _id, string _username, Client client)
	{
		id = _id;
		username = _username;
		health = maxHealth;
		isDead = false;

		// set the selected block color to the team's default. better than a just black block
		selectedBlockColor = Config.teamDefaultColors[(Config.ValidTeams)teamID];
		Debug.Log("Initialized player ID:+ " + id);

        if (Config.KICK_FOR_TOO_MANY_PACKETS)
            StartCoroutine(CallResetReceivedPackets(client));
	}

    IEnumerator CallResetReceivedPackets(Client client)
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(1f);
            client.ResetBytesRead();
        }
    }

    public void HandleSelectedItemID(int newID)
    {
        // check if the player selected an item that is in the current class.
        // so players can't cheat that they are for example a sniper player but can use all weapons.
        if (currentItemsData.IsItemInHoldableItemsForCurrentClass(newID))
        {
            selectedItem = newID;

            // now send it to other clients
            ServerSend.SendPlayerSelectedItemChange(newID, id);
        }
        else
        {
            ac.TriggerPlayerBan();
        }
    }

    /// <summary>
    /// Title.
    /// </summary>
    /// <param name="crouch">If we are crouching, call with true</param>
    void ChangeColliderSizeAndPositionDependingIfCrouching(bool crouch)
	{

		if (crouch) // this code block enables crouch
		{
			collider.center = collider.center * offsetCoefficientForColliderPositioning;
			collider.height = collOriginalHeight * offsetCoefficientForColliderHeightChange;
			noclipCollider.center = noclipCollider.center * offsetCoefficientForColliderPositioning;
			noclipCollider.height = noclipCollOriginalHeight * offsetCoefficientForColliderHeightChange;
		}
		else if (!crouch) // this code block disables crouch
		{
			collider.center = collOriginalPosition;
			collider.height = collOriginalHeight;
			noclipCollider.center = noclipCollOriginalPosition;
			noclipCollider.height = noclipCollOriginalHeight;
		}
	}

	bool changeInCrouchState = false;
	bool canTakeDamageFromWater = true;
	/// <summary>Processes player input and moves the player.</summary>
	public void FixedUpdate()
    {
        if (Server.clients[id].mapLoadingDone && !Server.clients[id].vanishEnabled) // we do not want to send this player's position if he is vanished...
        {
			ServerSend.PlayerPosition(this);
			ServerSend.PlayerRotation(this);
			
			if (crouching != changeInCrouchState) // change in crouch state
			{
				ChangeColliderSizeAndPositionDependingIfCrouching(crouching);
				if (crouching)
				{
					animator.SetBool("isCrouching", true);
					locationToShootRaysForEditingBlocks = locationToShootRaysForEditingBlocksCrouching;
				}
				else
				{
					animator.SetBool("isCrouching", false);
					locationToShootRaysForEditingBlocks = locationToShootRaysForEditingBlocksStanding;
				}
			}

			changeInCrouchState = crouching;


			if (transform.position.y < 1.5 && canTakeDamageFromWater)
			{
				TakeDamage(Config.waterDamage, Player.DeathSource.fallOutOfMap, id);
				canTakeDamageFromWater = false;
				StartCoroutine(ResetWaterDmgCooldown());
			}
		}
		else
		{
			
		}

		#region commented
		//Declare the ClientInputState that we're going to be using.
		//ClientInputState inputState = null;

		//while (clientInputs.Count > 0 && ((inputState = clientInputs.Dequeue()) != null))
		//{
		//	globalInputState = inputState;

		//	//transform.Translate(velocity, Space.World);
		//	// Calculate how fast we should be moving
		//	Vector3 targetVelocity = new Vector3(globalInputState.horizontalInput, 0, globalInputState.verticalInput);
		//	targetVelocity = transform.TransformDirection(targetVelocity);
		//	targetVelocity *= walkSpeed;

		//	// Apply a force that attempts to reach our target velocity
		//	Vector3 velocity = rb.velocity;
		//	Vector3 velocityChange = (targetVelocity - velocity);
		//	velocityChange.x = Mathf.Clamp(velocityChange.x, -10f, 10f);
		//	velocityChange.z = Mathf.Clamp(velocityChange.z, -10f, 10f);
		//	velocityChange.y = 0;
		//	rb.AddForce(velocityChange, ForceMode.VelocityChange);
		//	jumpRequest = globalInputState._jumpRequest;
		//	transform.rotation = globalInputState._rotation;
		//	Jump();
		//	ServerSend.PlayerPosition(this);
		//	ServerSend.PlayerRotation(this);

		//	// Obtain the current SimulationState.
		//	SimulationState state = CurrentSimulationState(inputState);

		//	// Send the state back to the client.
		//	ServerSend.SendSimulationState(this, state);
		//}
		#endregion
	}

    public void SetPositionEtc(Vector3 _pos, Quaternion _rot, Vector3 _lookDir, bool _crouching, Vector3 _vel)
    {
        // we don't want the player to be able to set these if he is dead
        if (isDead)
            return;

        transform.position = _pos;
        transform.rotation = _rot;
        lookDir = _lookDir;
        crouching = _crouching;
        velocity = _vel;
    }

	void LateUpdate()
	{
		if (lookDir != Vector3.zero)
			headToRotate.transform.rotation = Quaternion.LookRotation(lookDir);
	}

	float timeWhenLastPositionReceived = 0f;
	Vector3 previousVelocity = Vector3.zero;
	Vector3 lastPos = Vector3.zero;
	float borderValue = 750f;
	Vector3 velocityCalculatedFromClientSentPosition = Vector3.zero;
	float verticalMovementAmount = 0f;
    Stopwatch watch = new Stopwatch();
    float totalDistanceMoved = 0f;
	public void OnPlayerPositionReceived()
	{
		// change in time since last position received
		float timeSinceLastPositionReceived = Time.time - timeWhenLastPositionReceived;

		// calculcate velocity based only on the position player sent us
		Vector3 velocity = (transform.position-lastPos)/(timeSinceLastPositionReceived);

        // increment
        ac.AddMovedDistance((transform.position - lastPos).magnitude);

        if (highestSpeed < velocity.magnitude)
            highestSpeed = velocity.magnitude;

		// calculate acceleration
		acceleration = (velocity - previousVelocity) / (timeSinceLastPositionReceived);

		// to fix bug where player dies after dying from a high place, if a player is dead when receiving player position
		// -> set VerticalMovementAmount to 0f and don't set it again.
		if (true)
		{
			if (transform.position.y < lastPos.y)
			{
				verticalMovementAmount += lastPos.y - transform.position.y; // calculate y movement
			}
		}

		timeWhenLastPositionReceived = Time.time;
		lastPos = transform.position;
		previousVelocity = velocity;
	}

	// used for making player not take any falldamage at the first moment he spawns
	bool justSpawned = true;
	// when we hit the ground, check
	// but we don't want to die after dying from a high place.
	void CheckForFallDamage()
	{
		//Debug.Log("hit the ground with vertical movement distance: " + verticalMovementAmount);

		if (verticalMovementAmount >= 6 && !justSpawned)
		{
            //Debug.Log("taking dmg");
			float dmgToTake = 20 * verticalMovementAmount - 120; // <- linear dependency with fall height and fall dmg
			TakeDamage(dmgToTake, DeathSource.fallDamage, id);
		}

		justSpawned = false;
		verticalMovementAmount = 0f;
	}

	float cooldownTimeForTakingDamageFromWater = 1f;
	IEnumerator ResetWaterDmgCooldown()
	{
		yield return new WaitForSeconds(cooldownTimeForTakingDamageFromWater);
		canTakeDamageFromWater = true;
	}

	public void Jump()
	{
		if (isDead)
			return;

		if (isGrounded && jumpRequest)
		{
			rb.AddForce(new Vector3(0, jumpForce, 0), ForceMode.Impulse);
			isGrounded = false;
			jumpRequest = false;
		}

	}

	int headshotDmgMultiplier = 2;
	int GetDamageGivenToOtherPlayer(Vector3 dir, out Player player, out bool headshot)
	{
		int dmgMultiplier = 1;
		headshot = false;
		if (Physics.Raycast(transform.position + locationToShootRaysForEditingBlocks, dir, out RaycastHit _hit, 1000f))
		{
			player = _hit.collider.GetComponentInParent<Player>();

			// check if we hit a playercollider with the raycast and it is not the collider of this player ( the shooter )
			if ((_hit.collider.CompareTag("PlayerCollider")  || _hit.collider.CompareTag("headshotCollider")) && _hit.collider.GetComponentInParent<Player>() != this)
			{
				if (_hit.collider.CompareTag("headshotCollider"))
				{
					dmgMultiplier = headshotDmgMultiplier;
					headshot = true;
				}

				switch (selectedItem)
				{
					case (int)ValidItems.sniperRifle:
						return 100 * dmgMultiplier;
						break;
					case (int)ValidItems.mp5_smg:
						return 25 * dmgMultiplier;
						break;
					case (int)ValidItems.m249:
						return 25 * dmgMultiplier;
						break;
					case (int)ValidItems.doubleBarrel:
						return 10; // 1 pellet dmg
						break;
				}

			}
		}
		player = null; // assign to null if hit nothing
		return 0; // did not hit player so return -1
	}

    /// <summary>
    /// Makes sure the client does not do any kind of packet injection or client side modding for ammo count.
    /// Will trigger ac ban if notices any cheating.
    /// </summary>
    public void ValidateAmmoCount()
    {
        Config.ItemCfg _ref = currentItemsData.currentlyHoldableItemsData[(ValidItems)selectedItem];
        if (_ref.bulletsLeft > 0)// nothing, we all good
        {
            _ref.bulletsLeft--;
            //Debug.Log($"bulletsLeft:{_ref.bulletsLeft}, bulletsInMag:{_ref.bulletsInMag}, magsLeft:{_ref.magsLeft}");
        }
        else
        {
            // !!! alert !!! player is shooting even though he should not be able to. there must be some
            // client modifications -> ac alert and ban
            Debug.Log("[AC] ALERT! player: " + id + " is possibly modifying ammo count on the client side!");
            ac.TriggerPlayerKick();
        }
    }

	public void Shoot(Vector3 _viewDirection)
    {
		if (isDead) // we don't wanna let the player shoot as dead
			return;

        ValidateAmmoCount();

		// rewind in time before shooting
		LagCompensation.History.instance.RewindInTime(this.id);

		ServerSend.PlayerMuzzleFlash(this);

		if (selectedItem != (int)ValidItems.rocketLauncher && selectedItem != (int)ValidItems.doubleBarrel)
			ServerSend.AmmoSimulation(this, _viewDirection, Vector3.zero, selectedItem, Quaternion.identity);

		if (selectedItem == (int)ValidItems.doubleBarrel)
			ShootShotgun(_viewDirection);

		Player hitPlayer;
		bool headshot;

		int dmg = GetDamageGivenToOtherPlayer(_viewDirection, out hitPlayer, out headshot);
		if (dmg > 0)
		{
			hitPlayer.TakeDamage(dmg, (DeathSource)selectedItem, id, headshot);
		}
		else
			EditBlockByShooting(_viewDirection);

		// roll back to the present
		LagCompensation.History.instance.UnRewind();
    }

	int shotgunPelletsToShoot = 15;
	public float shotgunDeviation = 3f;
	public float normalSpread = 0.04f;
	int pelletDmg = 20;
	/// <summary>
	/// Shoots raycasts in a fan shaped area. Counts raycasts (if hit enemy player) and multiplies that by dmg,
	/// and then substracts the hp from player.
	/// </summary>
	void ShootShotgun(Vector3 _viewDirection)
	{
		Debug.Log("shooting shotgun");
		/////////////////////////// for calculating spread
		
		///////////////////////////
		///
		int totalDamage = 0;
		RaycastHit _hit = new RaycastHit();
		Player hitPlayer;

		// cache the damages to each player here
		Dictionary<Player, int> playerDmg = new Dictionary<Player, int>();

		for (int i = 0; i < shotgunPelletsToShoot; i++)
		{
			Vector3 direction = _viewDirection; // Bullet Spread
			direction.x += Random.Range(-shotgunDeviation, shotgunDeviation);
			direction.y += Random.Range(-shotgunDeviation, shotgunDeviation);

			if (Physics.Raycast(transform.position + locationToShootRaysForEditingBlocks, direction, out _hit, 100f))
			{
				Debug.DrawLine(transform.position + locationToShootRaysForEditingBlocks, _hit.point, Color.red, 10);
				// check if we hit a playercollider with the raycast and it is not the collider of this player ( the shooter )
				if (_hit.collider.CompareTag("PlayerCollider") && _hit.collider.GetComponentInParent<Player>() != this)
				{
					Player thisPlayer = _hit.collider.GetComponent<Player>();

					if (!playerDmg.ContainsKey(thisPlayer))
					{
						playerDmg.Add(thisPlayer, pelletDmg);
					}
					else
						playerDmg[thisPlayer]++;
				}
				else if (_hit.collider.CompareTag("headshotCollider") && _hit.collider.GetComponentInParent<Player>() != this)
					{
						Player thisPlayer = _hit.collider.GetComponent<Player>();

						if (!playerDmg.ContainsKey(thisPlayer))
						{
							playerDmg.Add(thisPlayer, pelletDmg * 2);
						}
						else
							playerDmg[thisPlayer] += pelletDmg * 2;
					}
			}

			// for optimization reasons. rather do this than .takedamage on every pellet hit,
			// especially if the pellet count is rising
			foreach (KeyValuePair<Player, int> kvp in playerDmg)
			{
				kvp.Key.TakeDamage(kvp.Value, DeathSource.doubleBarrel, id);
			}
		}
	}

	public enum DeathSource
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

	public bool canDieByFallingOffMap = true;
    public void TakeDamage(float _damage, DeathSource source, int shooterID, bool headshot = false)
    {
        if (health <= 0f || _damage <= 0f || Server.clients[id].godmodeEnabled || Server.clients[id].vanishEnabled) // we don't want to take damage if we are dead
        {
            return;
        }
        if (Server.clients[shooterID].player.teamID == teamID && shooterID != id)
        {
            return;
        }
        
        health -= _damage;
		ServerSend.PlayerHealth(this);
		if (health <= 0f && !isDead)
        {
			// here this instance of the player dies. we want to serversend.killfeedupdate() here
			ServerSend.KillfeedUpdate(shooterID, source, headshot, this.id);

			// increment the shooter's kills if we killed someone else
			if (shooterID != id)
			{
				Server.clients[shooterID].player.kills++;
			} // decrement the kills if we killed ourselves
			else
			{
				Server.clients[shooterID].player.kills--;
			}
			
			// increment this player's deaths
			deaths++;
			isDead = true;
			health = 0f;
			canEditBlock = false;
			Debug.Log("calling die");
			StartCoroutine(Respawn());
		}
    }
	
    private IEnumerator Respawn()
    {
        yield return new WaitForSecondsRealtime(5f);
		if (wishTeamID >= 0)
		{
			// be sure we are not selecting a team that does not exist
			if (wishTeamID < (int)Config.ValidTeams.COUNT)
			{
				teamID = wishTeamID;

				// send acknowledgement that this player switched team
				ServerSend.PlayerTeamChangeAcknowledgement(id, teamID);

				selectedBlockColor = Config.teamDefaultColors[(Config.ValidTeams)teamID];
			}
			wishTeamID = -1;
		}
		health = maxHealth;
		canEditBlock = true;
		Vector3 spawnPos = Config.spawnAreas[teamID].GetRandomSpawnPosition(); // <- spawn to it's team random spawn position
		ServerSend.PlayerRespawned(this, spawnPos);
		justSpawned = true;
        isDead = false;
        currentItemsData.ResetBulletCountEtc();
        yield return new WaitForSeconds(5f);
		canDieByFallingOffMap = true;
    }

	/// <summary>
	/// For checking if we should place a block in the certain coordinates. If player is 
	/// standing on those coordinates or close enough to them, return true.
	/// </summary>
	/// <param name="positionToCheck">Position to check</param>
	/// <returns>If the voxel position is occupied or not. Returns true if player is standing in close proximity
	/// to the certain coordinates.</returns>
	bool isVoxelPositionOccupiedByPlayer(Vector3 positionToCheck)
	{
		Collider[] hits = Physics.OverlapBox(positionToCheck + new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f));

		foreach (Collider hit in hits)
		{
			if (hit.CompareTag("PlayerCollider") || hit.CompareTag("headshotCollider"))
			{
				return true;
			}
		}
		return false;
		//for	(int i = 1; i < Server.MaxPlayers; i++) // have tw start with i=1, because of the way implemented
		//{
		//	if (Server.clients[i].player == null)
		//		continue;

		//	// check if this is too close to positionToCheck
		//	//float dist = Vector3.Distance(positionToCheck, Server.clients[i].player.transform.position);
		//	Vector2 playerPosXZ = new Vector2(Mathf.FloorToInt(Server.clients[i].player.transform.position.x), Mathf.FloorToInt(Server.clients[i].player.transform.position.z));
		//	Vector2 checkXZ = new Vector2((positionToCheck.x) ,( positionToCheck.z));
		//	float distXZ = Vector2.Distance(playerPosXZ, checkXZ);

		//	float playerPosY = Server.clients[i].player.transform.position.y;
		//	float checkY = positionToCheck.y;
		//	float distY = Mathf.Abs(playerPosY + 0.5f - checkY);

		//	Vector3 vec1 = new Vector3(Server.clients[i].player.transform.position.x, playerPosY, Server.clients[i].player.transform.position.z);
		//	Vector3 vec2 = new Vector3(Server.clients[i].player.transform.position.x, playerPosY + 0.5f - checkY, Server.clients[i].player.transform.position.z);
		//	Debug.DrawLine(vec1, vec2);
		//	// if player is too close, return true
		//	if (distXZ < minimumDistanceBetweenPlacingPositionAndPlayerXZ)
		//	{
		//		//if ((distY < minimumDistanceBetweenPlacingPositionAndPlayerY && Server.clients[i].player.transform.position.y > positionToCheck.y ) ||
		//		//	(distY < Mathf.Abs(Mathf.FloorToInt(Server.clients[i].player.transform.position.y + 2.5f) - checkY) && Server.clients[i].player.transform.position.y <= positionToCheck.y))
		//		//{
		//		//	return true;
		//		//}
		//		if (distY < minimumDistanceBetweenPlacingPositionAndPlayerY)
		//		{
		//			return true;
		//		}
		//		
		//	}
		//}
		//return false;
	}

	float spadeDamageToPlayer = 40f;
	float spadeRangeForHittingPlayers = 2f;
	public void EditBlock(Vector3 facing, byte newBlockID)
	{
		// first check if we wanna edit to 0 (break) and see if player is on the way.
		// if there is player, then .takedamage
		if (newBlockID == 0 && Physics.Raycast(transform.position + locationToShootRaysForEditingBlocks, facing, out RaycastHit _hit, spadeRangeForHittingPlayers))
		{
			if (_hit.transform.tag == "PlayerCollider" && _hit.transform.gameObject != gameObject)
			{
				_hit.collider.GetComponentInParent<Player>().TakeDamage(spadeDamageToPlayer, Player.DeathSource.spade, id);
			}
		}

		if (!canEditBlock || isDead) // if we are on delay we don't want to edit the block
			return;

		float step = checkIncrement;
		Vector3 lastPos = new Vector3();
		Vector3 breakPosition = new Vector3();
		Vector3 placePosition = new Vector3();
		
		while (step < reach)
		{
			Vector3 pos = (transform.position + locationToShootRaysForEditingBlocks) + (facing * step);
			
			if (world.CheckForVoxel(pos)) // check if there is solid block at pos
			{
				breakPosition = new Vector3(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
				placePosition = lastPos;

				// break block (set it to 0=air)
				if (newBlockID == 0)
				{
					canEditBlock = false;
					StartCoroutine(DelayBlockPlacement(breakPosition, newBlockID));
					break;
				}
				else
				{
					// we want to place block, since newblockid is larger than 0 (so set it to newBlockID)
					// if the block position is not occupied. should check differently if we are placing block/removing
					if (!isVoxelPositionOccupiedByPlayer(placePosition) && world.IsVoxelInWorld(placePosition))
					{
						canEditBlock = false;
						StartCoroutine(DelayBlockPlacement(placePosition, newBlockID, selectedBlockColor));
					}
					break;
				}
			}

			lastPos = new Vector3(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
			step += checkIncrement;

			// since we have now edited the block successfully on the server,
			// we need to tell it to clients so they know that the block has been updated.
		}
	}
	float editBlockDelay = .3f;
	IEnumerator DelayBlockPlacement(Vector3 placePosition, byte newblockID, int color = 0)
	{
		EditBlockInWorld(placePosition, newblockID, false, color);
		yield return new WaitForSeconds(editBlockDelay);

		canEditBlock = true;
		yield return null;
	}

	public GameObject mglModel;
	public void PhysicsShoot(Vector3 facing, Vector3 shootFrom, Quaternion instantiateRotation)
	{
        ValidateAmmoCount();
        switch (selectedItem)
		{
			case (int)ValidItems.rocketLauncher:
				// instantiate
				GameObject obj = Instantiate(rocketModel, shootFrom, Quaternion.identity);

				obj.GetComponent<Rigidbody>().AddForce(facing * 2000f);
				obj.GetComponent<ExplodeOnImpact>().shooterID = id;
				obj.GetComponent<ExplodeOnImpact>().shooterSelectedItem = selectedItem;
				// add force
				break;
			case (int)ValidItems.c4:
				// instantiate
				GameObject obj1 = Instantiate(c4Prefab, shootFrom, instantiateRotation);

				obj1.GetComponent<Rigidbody>().AddForce(facing * 150f);
				obj1.GetComponent<c4collision>().thrown = true;
				obj1.GetComponent<c4collision>().shooterID = id;
				break;
			case (int)ValidItems.mgl:
				// instantiate
				GameObject obj2 = Instantiate(mglModel, shootFrom, Quaternion.identity);

				obj2.GetComponent<Rigidbody>().AddForce(facing * 1000f);
				obj2.GetComponent<ExplodeOnImpact>().shooterSelectedItem = selectedItem;
				obj2.GetComponent<ExplodeOnImpact>().shooterID = id;
				// add force
				break;
		}

	}

	Dictionary<Vector3, int> blocksWeHaveShootedAt = new Dictionary<Vector3, int>();
	public void EditBlockByShooting(Vector3 facing, float range = 100f)
	{
		if (!canEditBlock) // if we are on delay we don't want to edit the block
			return;

		float step = checkIncrement;
		Vector3 lastPos = new Vector3();
		Vector3 breakPosition = new Vector3();
		Vector3 placePosition = new Vector3();
		//Debug.Log("blocksWeHaveShootedAt.length = " + blocksWeHaveShootedAt.Count);
		while (step < range)
		{
			Vector3 pos = (transform.position + locationToShootRaysForEditingBlocks) + (facing * step);

			if (world.CheckForVoxel(pos)) // check if there is solid block at pos
			{
				if (true)
				{
					breakPosition = new Vector3(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));

					
					if (blocksWeHaveShootedAt.ContainsKey(breakPosition))
					{
						if (blocksWeHaveShootedAt[breakPosition] == GetTimesToShootForBreakingTheBlock())
						{
							// break block (set it to 0=air)
							EditBlockInWorld(breakPosition, 0);
							blocksWeHaveShootedAt.Remove(breakPosition);
						}
						else
						{
							// increment
							blocksWeHaveShootedAt[breakPosition]++;
						}
					}
					else
					{
						blocksWeHaveShootedAt.Add(breakPosition, 2);
						StartCoroutine(RemoveKeyFromBlocksWeHaveShootedAt(breakPosition));
					}
				}
				
				break;
				
			}

			lastPos = new Vector3(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
			step += checkIncrement;
		}
	}

	IEnumerator RemoveKeyFromBlocksWeHaveShootedAt(Vector3 key)
	{
		yield return new WaitForSeconds(5f);

		blocksWeHaveShootedAt.Remove(key);
	}

	int GetTimesToShootForBreakingTheBlock()
	{
		switch (selectedItem)
		{
			case (int)ValidItems.mp5_smg:
				return 3;
			case (int)ValidItems.sniperRifle:
				return 1;
			case (int)ValidItems.m249:
				return 2;

			default:
				return 1;
		}
	}

	public void OnClientInputStateReceived(ClientInputState message)
	{
		clientInputs.Enqueue(message);
	}

	public class ClientInputState
	{
		public Vector3 lookDir;
		public float horizontalInput;
		public float verticalInput;
		public Quaternion _rotation;
		public bool _jumpRequest;
		public int simulationFrame;
	}
	public class SimulationState
	{
		public Vector3 position;
		public Vector3 _velocity;
		public int simulationFrame;
	}

	private SimulationState CurrentSimulationState(ClientInputState _inputState)
	{
		return new SimulationState
		{
			position = transform.position,
			_velocity = rb.velocity,
			simulationFrame = _inputState.simulationFrame
		};
	}

	void OnTriggerEnter(Collider coll)
	{
		if (coll.tag == "worldterrain")
		{
			//Debug.Log("grounded");
			isGrounded = true;

			CheckForFallDamage();
		}
	}

	public static void OnBlockDestroyed(Vector3 pos)
	{
		var watch = System.Diagnostics.Stopwatch.StartNew();
		world.GetPathBlocks(pos);
	}

	static int lowestLevelWhereBlocksCanBeEdited = 2;

	// edit the block/voxel in the world map in memory and send the update to all clients
	public static void EditBlockInWorld(Vector3 pos, byte newBlockId, bool noSend = false, int color = 0)
	{
		if (pos.y < lowestLevelWhereBlocksCanBeEdited)
			return;

		if (Config.ENABLE_FALLING_BLOCKS)
		{
			Grid.instance.UpdateNode(pos, newBlockId != 0);
			world.TestIsSolid(pos);
		}
		
		if (newBlockId == 0) // we wish to break blocks
		{
			// edit the voxel in memory
			world.GetChunkFromVector3(pos).EditVoxel(pos, newBlockId, 0); // color will be anyways ignored

			if (Config.ENABLE_FALLING_BLOCKS)
			{
				Thread myThread = new Thread(() => OnBlockDestroyed(pos));
				myThread.Start();
			}

			// instantiate a new updated block object
			Server.UpdatedBlock block = new Server.UpdatedBlock(pos, 0, newBlockId);

			//if it is in list->remove it and add new one
			// if not in list->just add normally
			if (Server.updatedBlocks.Contains(block))
			{
				//Debug.Log("equals, trying to remove it...");
				Server.updatedBlocks.Remove(block);
			}

			// ad it to the list
			Server.updatedBlocks.Add(new Server.UpdatedBlock(pos, 0, newBlockId));

			// send block update to clients
			if (!noSend)
				ServerSend.BlockUpdated(block);

			//Debug.Log("count: " +Server.updatedBlocks.Count);
		}
		else // we wish to place blocks
		{
			Debug.Log("placing block with color of " + color.ToString());

			// edit the voxel in memory
			world.GetChunkFromVector3(pos).EditVoxel(pos, newBlockId, color); // color will be anyways ignored

			// instantiate a new updated block object
			Server.UpdatedBlock block = new Server.UpdatedBlock(pos, color, newBlockId);

			//if it is in list->remove it and add new one
			// if not in list->just add normally

			if (Server.updatedBlocks.Contains(block))
			{
				//Debug.Log("equals, trying to remove it...");
				Server.updatedBlocks.Remove(block);
			}

			// ad it to the list
			Server.updatedBlocks.Add(new Server.UpdatedBlock(pos, color, newBlockId));

			// send block update to clients
			if (!noSend)
				ServerSend.BlockUpdated(block);
		}
	}

	public enum ExplosionType
	{
		sphere,
		sphericalTunnel
	}

	static int c4ExplosionTunnelLength = 20;
	static int c4ExplosionRadius = 2;
	/// <summary>
	/// Create a spherical explosion based on radius. Basically sets voxels to be air
	/// in kind of spherical shape.
	/// </summary>
	/// <param name="radius">Radius of the explosion in Unity units.</param>
	public static void CreateExplosion(Vector3 explosionOrigin, Vector3 dir, int _shooterID, int selectedItem = (int)ValidItems.rocketLauncher, ExplosionType et = ExplosionType.sphere)
	{
		float pi = Mathf.PI;
		HashSet<Server.UpdatedBlock> listOfExplosionUpdatedBlocks = new HashSet<Server.UpdatedBlock>();
		if (selectedItem == (int)ValidItems.mgl)
			explosionRadius = 1;


		switch (et)
		{
			case ExplosionType.sphere:
				{
					for (int x = 0; x < explosionRadius * 2; x++)
					{
						for (int y = 0; y < explosionRadius * 2; y++)
						{
							for (int z = 0; z < explosionRadius * 2; z++)
							{
								Vector3 offset = new Vector3(x - explosionRadius + 0.5f, y - explosionRadius + 0.5f, z - explosionRadius + 0.5f);
								Vector3 checkPos = explosionOrigin + offset;

								// if there is a block and it's distance to explosionOrigin is smaller or equal to the radius
								if (world.CheckForVoxel(checkPos) && Vector3.Distance(checkPos, explosionOrigin) <= explosionRadius && checkPos.y >= lowestLevelWhereBlocksCanBeEdited)
								{
									// add to list each to list
									listOfExplosionUpdatedBlocks.Add(new Server.UpdatedBlock(checkPos, 0, 0)); // 0 cuz air
									EditBlockInWorld(checkPos, 0, true);
								}
							}
						}
					}
				}
				break;

			case ExplosionType.sphericalTunnel: // create tunnel in the direction of dir
				Vector3 dimensions = Vector3.zero;
				for (int x = 0; x < c4ExplosionTunnelLength; x++)
				{
					for (int y = 0; y < c4ExplosionRadius * 2; y++)
					{
						for (int z = 0; z < c4ExplosionRadius * 2; z++)
						{
							Vector3 offset = new Vector3();
							if (dir == Vector3.up || dir == Vector3.down) // works at least dowwards
							{
								offset = new Vector3(y - c4ExplosionRadius, (dir.y) * x, z - c4ExplosionRadius);
								dimensions = new Vector3(c4ExplosionRadius * 2, c4ExplosionTunnelLength, c4ExplosionRadius * 2);
							}
							else if (dir == Vector3.forward || dir == Vector3.back) // works
							{
								offset = new Vector3(y - c4ExplosionRadius, z - c4ExplosionRadius, (dir.z)*x);
								dimensions = new Vector3(c4ExplosionRadius * 2, c4ExplosionRadius * 2, c4ExplosionTunnelLength);
							}
							else if (dir == Vector3.right || dir == Vector3.left)
							{
								offset = new Vector3((dir.x) * x, y - c4ExplosionRadius, z - c4ExplosionRadius);
								dimensions = new Vector3(c4ExplosionTunnelLength, c4ExplosionRadius * 2, c4ExplosionRadius * 2);
							}

							Vector3 checkPos = explosionOrigin + offset;

							// if there is a block and it's distance to explosionOrigin is smaller or equal to the radius
							if (world.CheckForVoxel(checkPos) && checkPos.y >= lowestLevelWhereBlocksCanBeEdited)
							{
								// add to list each to list
								listOfExplosionUpdatedBlocks.Add(new Server.UpdatedBlock(checkPos, 0, 0)); // 0 cuz air
								EditBlockInWorld(checkPos, 0, true);
							}
						}
					}
				}
				OverlapBox(explosionOrigin, dir, dimensions, _shooterID);
				break;
		}
		

		ServerSend.SplitServerUpdatedBlocksIntoChunksAndSendToNewlyConnectedClient(-1, listOfExplosionUpdatedBlocks, true);
	}

	static void OverlapBox(Vector3 origin, Vector3 dir, Vector3 dimensions, int shooterID)
	{
		dimensions *= 0.5f;
		Collider[] hits = Physics.OverlapBox(origin +dir * c4ExplosionTunnelLength / 2, dimensions);

		foreach (Collider coll in hits)
		{
			if (coll.tag == "PlayerCollider")
				coll.GetComponent<Player>().TakeDamage(100f, Player.DeathSource.c4, shooterID);
		}
	}
}
