using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
	[HideInInspector]
	public bool isGrounded;
	public bool isSprinting;

	private Transform cam;
	private World world;
	
	private float sensitivity = 5000f;

	private float horizontal;
	private float vertical;
	private float mouseHorizontal;
	private float mouseVertical;
	private float moveHorizontal;
	private float moveVertical;
	private bool jumpRequest;
	private bool reconciliationNecessary = false;
	public Transform highlightBlock;
	public Transform placeBlock;
	public float checkIncrement = 0.1f;
	public float reach = 8f;

	public Text selectedBlockText;
	public byte selectedBlockIndex = 1;
    private Rigidbody virtualRB;
	ChatManager chatManager;

	// --------------

	#region client prediction variables
	private ClientInputState inputState;
	private int simulationFrame = 0;
	private int lastCorrectedFrame;
	private Rigidbody rb;
	private PlayerManager playerManager;
	private MeshCollider playerDontFallOfBlockEdgesCollider;
	public MeshCollider playerDontFallOfBlockEdgesCollider2;
	private ItemManager itemManager;
	public bool menuOpen = false;
	private UIManager _UIManager;

	// these are important for client prediction. these must be same on both client and the server.
	//----------------------------------- collision checking
	public float distanceFromPlayerPositionToFeet = 0.15f;
	public float distanceFromPlayerPositionToHead = 0.4f;
	public float playerWidth = 0.5f;
	public float boundsTolerance = 0.1f;
	
	//----------------------------------- jumping/moving
	[HideInInspector]
	public readonly float walkSpeed = 6f;
	[HideInInspector]
	public readonly float crouchSpeed = 3f;
	[HideInInspector]
	public float speed;
	//private float sprintSpeedConstant = 1.5f;
    [SerializeField]
	private float jumpForce;
	private float verticalMomentum;
	private float playerSpeedMultiplierForSmootherMovement = 30f;
	//----------------------------------- gravity coefficients, etc.
	private float gravity = -9.81f;
	private float fallCoefficient = 5f;
	[SerializeField]
	private float fallMultiplierFloat = 2.5f; // multiplier for when player's velocity.y is smaller than 0, so we have crispier falling
											  //-----------------------------------
	// recoil
	private float sideRecoil = 0f;
	private float upRecoil = 0f;
	private float recoilSpeed = 20f;

	// The default input state which is used by the server in the
	// event that a client doesn't submit an input for a given frame.
	private static ClientInputState defaultInputState = new ClientInputState();

	// The maximum cache size for both the ClientInputState and 
	// SimulationState caches. Unity's default time step for FixedUpdate
	// is 50hz, meaning we will store roughly 50 states per second.
	private const int STATE_CACHE_SIZE = 256;

	// The cache that stores all of the client's predicted movement reuslts. 
	private SimulationState[] simulationStateCache = new SimulationState[STATE_CACHE_SIZE];
	
	// The cache that stores all of the client's inputs. 
	private ClientInputState[] inputStateCache = new ClientInputState[STATE_CACHE_SIZE];

	// The last known SimulationState provided by the server. 
	private SimulationState serverSimulationState = new SimulationState();
	#endregion

	private Transform myTransform;

	public bool debugMovement { get; private set; } // if true will let the player fly

	private void Start()
	{ 
		speed = walkSpeed;
		// reference assigning
		cam = GameObject.Find("Main Camera").transform;
		world = GameObject.Find("GameManager").GetComponent<World>();
		playerManager = GetComponent<PlayerManager>();
		rb = GetComponent<Rigidbody>();
		playerDontFallOfBlockEdgesCollider = GetComponentInChildren<MeshCollider>();
		itemManager = GetComponent<ItemManager>();
		_UIManager = GameObject.FindWithTag("UIManager").GetComponent<UIManager>();

		isSprinting = false;
		verticalMomentum = 0f;
		Cursor.lockState = CursorLockMode.Locked;
		myTransform = transform;
		chatManager = GameObject.FindWithTag("HudManager").GetComponentInChildren<ChatManager>();

        DisableDebugMovement();
    }

	float groundHitMultiplier = 1f; // used for slowing the character down when hitting the ground
	private void FixedUpdate()
	{
        if (debugMovement)
        {
            ClientSend.PlayerPosition(rb.position, transform.rotation, cam.transform.forward, playerManager.crouching, rb.velocity);

            if (playerManager.corridorCheckFailed)
            {
                if (playerManager.CorridorIsWideEnough() && playerManager.crouching == true)
                {
                    playerManager.corridorCheckFailed = false;
                    playerManager.Crouch(false);
                }
            }

            return;
        }

        //Calculate how fast we should be moving
        Vector3 targetVelocity = new Vector3(moveHorizontal, 0, moveVertical);
        if (targetVelocity.magnitude > 1f)
            targetVelocity.Normalize();
        targetVelocity = transform.TransformDirection(targetVelocity);
        targetVelocity *= speed;
        targetVelocity *= groundHitMultiplier;
        groundHitMultiplier = Mathf.Lerp(groundHitMultiplier, 1f, 0.05f);

        // Apply a force that attempts to reach our target velocity
        Vector3 velocity = rb.velocity;
        Vector3 velocityChange = (targetVelocity - velocity);
        velocityChange.x = Mathf.Clamp(velocityChange.x, -10f, 10f);
        velocityChange.z = Mathf.Clamp(velocityChange.z, -10f, 10f);
        velocityChange.y = 0;
        rb.AddForce(velocityChange, ForceMode.VelocityChange);

        // for crispier jumping
        if (rb.velocity.y < 0)
            rb.velocity += Vector3.up * Physics.gravity.y * (fallMultiplierFloat- 1);

        ClientSend.PlayerPosition(rb.position, transform.rotation, cam.transform.forward, playerManager.crouching, rb.velocity);

		if (playerManager.corridorCheckFailed)
		{
			if (playerManager.CorridorIsWideEnough() && playerManager.crouching == true)
			{
				playerManager.corridorCheckFailed = false;
				playerManager.Crouch(false);
			}
		}

		#region commented
		//// Set the inputState's simulation frame.
		//// NOTE: We don't do this in update, as FixedUpdate is called 
		//// in conjunction with the physics time step, and not once per frame.
		//inputState.simulationFrame = simulationFrame;

		//// Send the input to the server to be processed.
		//ClientSend.SendInputState(inputState);

		//// Reconciliate if there's a message from the server.
		//if (serverSimulationState != null) Reconciliate();

		//// Get the current simulation state.
		//SimulationState simulationState = CurrentSimulationState(inputState);

		//// Determine the cache index based on modulus operator.
		//int cacheIndex = simulationFrame % STATE_CACHE_SIZE;

		//// Store the SimulationState into the simulationStateCache 
		//simulationStateCache[cacheIndex] = simulationState;

		//// Store the ClientInputState into the inputStateCache
		//inputStateCache[cacheIndex] = inputState;

		//// Increase the client's simulation frame.
		//++simulationFrame;
		#endregion
	}

	public void EnableDebugMovement()
	{
		debugMovement = true;
		rb.useGravity = false;
        rb.isKinematic = true;
	}

	public void DisableDebugMovement()
	{
		debugMovement = false;
        rb.isKinematic = false;
		rb.useGravity = true;
	}

	public bool GetMenuOpen()
	{
		return menuOpen;
	}

	float xRotation = 0f;
	float yRotation = 0f;
	private void LateUpdate()
	{
        if (debugMovement)
        {
            float tv = moveVertical;
            tv *= speed;

            // move in direction of looking
            transform.position += cam.transform.forward * speed * moveVertical * Time.deltaTime * 10;

            if (playerManager.corridorCheckFailed)
            {
                if (playerManager.CorridorIsWideEnough() && playerManager.crouching == true)
                {
                    playerManager.corridorCheckFailed = false;
                    playerManager.Crouch(false);
                }
            }
        }


        if (Input.GetKeyDown(KeyCode.Return))
		{
			// *= sprintSpeedConstant;
		}
		else if (Input.GetKeyUp(KeyCode.LeftShift))
		{
			//walkSpeed /= sprintSpeedConstant;
		}

		if (menuOpen || playerManager.isDead)
		{
			// don't move player when the menu is open
			moveHorizontal = 0f;
			moveVertical = 0f;
			upRecoil = 0f;
			sideRecoil = 0f;
			
			return;
		}

        float _sensitivity;

        // use sniper sensitivity if we are scoping
        if (HUDManager.instance.GetScopeOverlayActive())
        {
            _sensitivity = Config.settings["sensitivity"] * (Config.settings["scoped_sensitivity"] / 500);
        }
        else
        {
            _sensitivity = Config.settings["sensitivity"];
        }
		
		xRotation -= mouseVertical *   _sensitivity * Time.deltaTime;
        yRotation -= mouseHorizontal * _sensitivity * Time.deltaTime;
		xRotation = Mathf.Clamp(xRotation, -90f, 90f);
		GetPlayerInputs();
		RotateHeadAndHands();

		#region Recoil
		//yRotation += sideRecoil * Time.deltaTime * 100f;
		xRotation -= upRecoil * Time.deltaTime *100f;
		sideRecoil -= recoilSpeed * Time.deltaTime;
		upRecoil -= recoilSpeed * Time.deltaTime;
		if (sideRecoil < 0)
		{
			sideRecoil = 0;
		}
		if (upRecoil <0)
		{
			upRecoil = 0;
		}
		#endregion

        if (!debugMovement)
        {
            // handle mouse look
            cam.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

            rb.MoveRotation(Quaternion.Euler(rb.rotation.eulerAngles + new Vector3(0f, mouseHorizontal * _sensitivity * Time.deltaTime, 0f)));
        }
        else
        {
            // handle mouse look
            cam.localRotation = Quaternion.Euler(xRotation, -yRotation, 0f);
        }
        
        
        playerManager.SetVelocity(rb.velocity);
		CheckCrouch();
	}

	void RotateHeadAndHands()
	{
		// calculate angle to rotate hand
		float angleDiff1 = Vector3.Angle(cam.transform.forward, playerManager.handToRotate1.transform.forward);
		float angleDiff2 = Vector3.Angle(cam.transform.forward, playerManager.handToRotate2.transform.forward);

		// rotate hand
		playerManager.handToRotate1.transform.Rotate(Vector3.left, angleDiff1);
		playerManager.handToRotate1.transform.Rotate(Vector3.left, -90f); // extra -90 deg to align properly

		// rotate hand
		playerManager.handToRotate2.transform.Rotate(new Vector3(-1, 1, 0), angleDiff1);
		playerManager.handToRotate2.transform.Rotate(new Vector3(-1, 1, 0), -90f); // extra -90 deg to align properly

		// rotate head
		playerManager.headToRotate.transform.localRotation = Quaternion.Euler(-xRotation, 0f, 0f);
	}

	#region crouchingLogic
	/// <summary>
	/// For deciding on crouching based on hitting the crouch key. 
	/// </summary>
	void CheckCrouch()
	{
		if (Input.GetKeyDown(KeyCode.LeftControl))
		{
			if (playerManager.CorridorIsWideEnough())
				playerManager.Crouch(true);
		}
		else if (Input.GetKeyUp(KeyCode.LeftControl))
		{
			if (playerManager.CorridorIsWideEnough())
			{
				playerManager.Crouch(false);
			}
			else
				playerManager.corridorCheckFailed = true;
		}
	}
	
	#endregion

	public void Jump()
	{
		if (isGrounded && !debugMovement) // we don't want normal jumping during debug movement
		{
			Debug.Log("sending jump");
			rb.AddForce(new Vector3(0, jumpForce, 0), ForceMode.Impulse);
			isGrounded = false;
			jumpRequest = false;
		}

	}

	public void AddRecoil(float up, float side)
	{
		sideRecoil += side;
		upRecoil += up;
	}

	// this is for debug reasons and adding "fake lag"
	IEnumerator SendPlayerMovementWithDelay(float Count, ClientInputState _inputState)
	{
		yield return new WaitForSeconds(Count); //Count is the amount of time in seconds that you want to wait.
		ClientSend.SendInputState(_inputState);
		yield return null;
	}

	private void GetPlayerInputs()
	{
		moveHorizontal = Input.GetAxis("Horizontal");
		moveVertical = Input.GetAxis("Vertical");
		mouseHorizontal = Input.GetAxis("Mouse X");
		mouseVertical = Input.GetAxis("Mouse Y");
		if (Input.GetButtonDown("Jump"))
		{
			Jump();
		}
		//inputState = new ClientInputState
		//{
		//	lookDir = cam.forward,
		//	horizontalInput = Input.GetAxis("Horizontal"),
		//	verticalInput = Input.GetAxis("Vertical"),
		//	_rotation = transform.rotation,
		//	_jumpRequest = jumpRequest,
		//};


		//ClientSend.PlayerMovement(_inputs, Input.GetButtonDown("Jump"));
		//StartCoroutine(SendPlayerMovementWithDelay(0.2f, inputState));

		//if (Input.GetButtonDown("Sprint"))
		//	isSprinting = true;
		//if (Input.GetButtonUp("Sprint"))
		//	isSprinting = false;

		float scroll = Input.GetAxis("Mouse ScrollWheel");
	}

	private bool Reconciliate()
	{
		// Sanity check, don't for old states.
		if (serverSimulationState.simulationFrame <= lastCorrectedFrame) return false;

		// Determine the cache index 
		int cacheIndex = serverSimulationState.simulationFrame % STATE_CACHE_SIZE;

		// Obtain the cached input and simulation states.
		ClientInputState cachedInputState = inputStateCache[cacheIndex];
		SimulationState cachedSimulationState = simulationStateCache[cacheIndex];

		// If there's missing cache data for either input or simulation 
		// snap the player's position to match the server.
		if (cachedInputState == null || cachedSimulationState == null)
		{
			transform.position = serverSimulationState.position;
			//rb.velocity = serverSimulationState.velocity;

			// Set the last corrected frame to equal the server's frame.
			lastCorrectedFrame = serverSimulationState.simulationFrame;

			return false;
		}

		// Find the difference between the vector's values. 
		float differenceX = Mathf.Abs(cachedSimulationState.position.x - serverSimulationState.position.x);
		float differenceY = Mathf.Abs(cachedSimulationState.position.y - serverSimulationState.position.y);
		float differenceZ = Mathf.Abs(cachedSimulationState.position.z - serverSimulationState.position.z);

		//  The amount of distance in units that we will allow the client's
		//  prediction to drift from it's position on the server, before a
		//  correction is necessary. 
		float tolerance = 0f; //

		
		// A correction is necessary.
		if (differenceX > tolerance || differenceY > tolerance || differenceZ > tolerance)
		{
			
			// Set the player's position to match the server's state. 
			rb.position = serverSimulationState.position;
			//rb.velocity = serverSimulationState.velocity;

			// Declare the rewindFrame as we're about to resimulate our cached inputs. 
			int rewindFrame = serverSimulationState.simulationFrame;

			// Loop through and apply cached inputs until we're 
			// caught up to our current simulation frame. 
			while (rewindFrame < simulationFrame)
			{
				// Determine the cache index 
				int rewindCacheIndex = rewindFrame % STATE_CACHE_SIZE;

				// Obtain the cached input and simulation states.
				ClientInputState rewindCachedInputState = inputStateCache[rewindCacheIndex];
				SimulationState rewindCachedSimulationState = simulationStateCache[rewindCacheIndex];

				// If there's no state to simulate, for whatever reason, 
				// increment the rewindFrame and continue.
				if (rewindCachedInputState == null || rewindCachedSimulationState == null)
				{
					++rewindFrame;
					continue;
				}

				// Process the cached inputs. 
				//ProcessInputs(rewindCachedInputState);

				// Replace the simulationStateCache index with the new value.
				SimulationState rewoundSimulationState = CurrentSimulationState(inputState);
				rewoundSimulationState.simulationFrame = rewindFrame;
				simulationStateCache[rewindCacheIndex] = rewoundSimulationState;

				// Increase the amount of frames that we've rewound.
				++rewindFrame;
			}
			// Once we're complete, update the lastCorrectedFrame to match.
			// NOTE: Set this even if there's no correction to be made. 
			lastCorrectedFrame = serverSimulationState.simulationFrame;
			return true;
		}
		// Once we're complete, update the lastCorrectedFrame to match.
		// NOTE: Set this even if there's no correction to be made. 
		lastCorrectedFrame = serverSimulationState.simulationFrame;
		return false;

		
	}

	public void Respawn(Vector3 spawnPosition)
	{
		rb.velocity = Vector3.zero;
		transform.position = spawnPosition;
	}

	private SimulationState CurrentSimulationState(ClientInputState inputState)
	{
		return new SimulationState
		{
			position = transform.position,
			velocity = rb.velocity,
			simulationFrame = inputState.simulationFrame
		};
	}

	// when we receive simulation state
	public void OnServerSimulationStateReceived(SimulationState message)
	{
		// Only register newer SimulationState's. 
		if (serverSimulationState?.simulationFrame < message.simulationFrame)
		{
			serverSimulationState = message;
		}
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
		public Vector3 velocity;
		public int simulationFrame;
	}

	#region physicsSimulation
	private float checkDownSpeed(float downSpeed)
	{
		if (
			(world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + downSpeed - distanceFromPlayerPositionToFeet, transform.position.z - playerWidth)) && (!left && !back)) ||
			(world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + downSpeed - distanceFromPlayerPositionToFeet, transform.position.z - playerWidth)) && (!right && !back)) ||
			(world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + downSpeed - distanceFromPlayerPositionToFeet, transform.position.z + playerWidth)) && (!right && !front)) ||
			(world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + downSpeed - distanceFromPlayerPositionToFeet, transform.position.z + playerWidth)) && (!left && !front))
		   )
		{
			isGrounded = true;
			return 0;
		}
		else
		{
			isGrounded = false;
			return downSpeed;
		}
	}


	private float checkUpSpeed(float upSpeed)
	{
		if (
			(world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + distanceFromPlayerPositionToHead + upSpeed, transform.position.z - playerWidth)) && (!left && !back)) ||
			(world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + distanceFromPlayerPositionToHead + upSpeed, transform.position.z - playerWidth)) && (!right && !back)) ||
			(world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + distanceFromPlayerPositionToHead + upSpeed, transform.position.z + playerWidth)) && (!right && !front)) ||
			(world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + distanceFromPlayerPositionToHead + upSpeed, transform.position.z + playerWidth)) && (!left && !front))
		   )
		{
			verticalMomentum = 0;
			// set to 0 so the player falls when their head hits a block while jumping
			return 0;
		}
		else
		{
			return upSpeed;
		}
	}


	public bool front
	{

		get
		{
			if (
				world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y - distanceFromPlayerPositionToFeet, transform.position.z + playerWidth)) ||
				world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y + distanceFromPlayerPositionToHead, transform.position.z + playerWidth))
				)
				return true;
			else
				return false;
		}

	}
	public bool back
	{

		get
		{
			if (
				world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y - distanceFromPlayerPositionToFeet, transform.position.z - playerWidth)) ||
				world.CheckForVoxel(new Vector3(transform.position.x, transform.position.y + distanceFromPlayerPositionToHead, transform.position.z - playerWidth))
				)
				return true;
			else
				return false;
		}

	}
	public bool left
	{

		get
		{
			if (
				world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y - distanceFromPlayerPositionToFeet, transform.position.z)) ||
				world.CheckForVoxel(new Vector3(transform.position.x - playerWidth, transform.position.y + distanceFromPlayerPositionToHead, transform.position.z))
				)
				return true;
			else
				return false;
		}

	}
	public bool right
	{

		get
		{
			if (
				world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y - distanceFromPlayerPositionToFeet, transform.position.z)) ||
				world.CheckForVoxel(new Vector3(transform.position.x + playerWidth, transform.position.y + distanceFromPlayerPositionToHead, transform.position.z))
				)
				return true;
			else
				return false;
		}

	}
	#endregion

	bool setToZero = true;
	void OnTriggerStay (Collider coll) // we are grounded
	{
		if (gameObject.tag != "MainCamera" && coll.tag == "worldterrain")
		{
			isGrounded = true;
			playerDontFallOfBlockEdgesCollider.enabled = true;
			playerDontFallOfBlockEdgesCollider2.enabled = true;
			
			if (setToZero)
			{
				groundHitMultiplier = 0f;
				setToZero = false;
			}
		}
	}
	private void OnTriggerExit(Collider coll) // we are not grounded
	{
		if (gameObject.tag != "MainCamera" && coll.tag == "worldterrain")
		{
			isGrounded = false;
			if (rb.velocity.y > 0) // disable only when jumping, programmer's note: 200iq method to not fall of edges.
			{
				playerDontFallOfBlockEdgesCollider.enabled = false;
				playerDontFallOfBlockEdgesCollider2.enabled = false;
			}

			if (rb.velocity.y > 1f)
				setToZero = true;
		}
	}
}