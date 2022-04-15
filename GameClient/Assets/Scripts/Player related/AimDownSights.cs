using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AimDownSights : MonoBehaviour
{
	public Vector3 aimDownSights;
	 Vector3 hipFire;
	public float aimspeed;

	Camera cam;
	public bool useScopeForADS = false;
	float fov;
	public float camSpeed;
	PlayerManager playerManager;
	HUDManager hudManager;
	Camera weaponCam;
	public float fovToZoomTo = 33f;
	Player player;

	private void Start()
	{
		// Making sure your filed of view stays the same as the initial value
		player = GetComponentInParent<Player>();
		hipFire = gameObject.transform.localPosition;
		cam = GameObject.FindWithTag("MainCamera").GetComponent<Camera>();
		playerManager = GetComponentInParent<PlayerManager>();
		fov = cam.fieldOfView;
		hudManager = GameObject.FindWithTag("HudManager").GetComponent<HUDManager>();
		weaponCam = GameObject.FindWithTag("OverlayCamera").GetComponent<Camera>();
	}

	// Update is called once per frame
	void Update()
	{
		if (!playerManager.isLocalPlayer || playerManager.isDead || player.GetMenuOpen())
			return;

		if (Input.GetMouseButton(1)) 
		{
			transform.localPosition = Vector3.Slerp(transform.localPosition, aimDownSights, aimspeed * Time.deltaTime);
			//Closes up on center, through time, according to camSpeed variable
			if (cam.fieldOfView - Time.deltaTime * camSpeed >= fovToZoomTo)
				
			{
				cam.fieldOfView -= Time.deltaTime * camSpeed;
				
			}
			if (Vector3.Distance(aimDownSights,transform.localPosition) < 0.05f && weaponCam.enabled == true && useScopeForADS)
			{
                // we are using scope
				hudManager.SetScopeOverlayActive(true);
				weaponCam.enabled = false;
			}
		}
		else
		{
			transform.localPosition = Vector3.Slerp(transform.localPosition, hipFire, aimspeed * Time.deltaTime);
			//Gets farther away from center, through time, according to camSpeed variable
			if(cam.fieldOfView + Time.deltaTime * camSpeed <= fov)
				cam.fieldOfView += Time.deltaTime * camSpeed;
			
			
		}
		if (Input.GetMouseButtonUp(1) && useScopeForADS)
		{
			hudManager.SetScopeOverlayActive(false);
			weaponCam.enabled = true;
		}
	}

	// when we switch the weapon, it is destroyed.
	// detect that and remove scope overlay if it is active.
	void OnDestroy()
	{
		return;
		if (weaponCam.enabled == false)
		{
			weaponCam.enabled = true;
			hudManager.SetScopeOverlayActive(false);
		}
	}
}
