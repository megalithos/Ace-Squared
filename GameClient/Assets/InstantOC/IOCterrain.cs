using System;
using UnityEngine;
using System.Collections;

public class IOCterrain : IOCcomp {

	private IOCcam iocCam;
	private bool hidden;
	private int counter;
	private int frameInterval;
	private Terrain terrain;

	void Awake () {
		
		Init();
	}
	
	public override void Init(){
		try
		{
			iocCam =  Camera.main.GetComponent<IOCcam>();
			terrain = GetComponent<Terrain>();
			this.enabled = true;
		}
		catch(Exception e)
		{
			this.enabled = false;
			Debug.Log(e.Message);
		}
	}
	
	void Start () {
		terrain.enabled = false;
	}

	void Update () {
		frameInterval = Time.frameCount % 4;
		if(frameInterval == 0){
			if(!hidden && Time.frameCount - counter > iocCam.hideDelay)
			{
				Hide();
			}
		}
	}

	public void Hide(){
		terrain.enabled = false;
		hidden = true;
	}

	public override void UnHide(RaycastHit hit) {
		counter = Time.frameCount;
		terrain.enabled = true;
		hidden = false;
	}
}
