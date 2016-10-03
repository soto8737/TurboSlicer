using UnityEngine;
using System.Collections;

public class TSD4ScoreAgent : AbstractSliceHandler
{
	public bool isResultOfSlice = false;
	
	private new Camera camera;
	private new Transform transform;
	
	private bool wasUnderViewport = true;
	
	// Use this for initialization
	void Start ()
	{
		transform = GetComponent<Transform>();
		camera = Camera.main;
	}
	
	void Update()
	{
		if(!isResultOfSlice)
		{
			Vector3 viewportPosition = camera.WorldToViewportPoint(transform.position);
			
			bool isUnderViewport = viewportPosition.y < 0f;
			
			if(isUnderViewport && !wasUnderViewport)
			{			
				TSD4ScoreModel.instance.missed++;
			}
			
			wasUnderViewport = isUnderViewport;
		}
	}
	
	public override void handleSlice (GameObject[] results)
	{	
		if(isResultOfSlice)
			TSD4ScoreModel.instance.secondarySlices++;
		else
			TSD4ScoreModel.instance.primarySlices++;
		
		if(!isResultOfSlice)
		{
			for(int i = 0; i < results.Length; i++)
			{
				TSD4ScoreAgent scoreAgent = results[i].GetComponent<TSD4ScoreAgent>();
				
				scoreAgent.isResultOfSlice = true;
			}
		}
	}
}
