using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class TSD5ScoreAgent : AbstractSliceHandler
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
			CheckAboutFallingOutOfView();
		}
	}
	
	void CheckAboutFallingOutOfView()
	{
		Vector3 viewportPosition = camera.WorldToViewportPoint(transform.position);
		
		bool isUnderViewport = viewportPosition.y < 0f;
		
		if(isUnderViewport && !wasUnderViewport)
		{			
			TSD5ScoreModel.instance.missed++;
		}
		
		wasUnderViewport = isUnderViewport;
	}
	
	public override void handleSlice (GameObject[] results)
	{	
		if(isResultOfSlice)
			TSD5ScoreModel.instance.secondarySlices++;
		else
			TSD5ScoreModel.instance.primarySlices++;
		
		if(!isResultOfSlice)
		{
			for(int i = 0; i < results.Length; i++)
			{
				TSD5ScoreAgent target = results[i].GetComponent<TSD5ScoreAgent>();
				
				target.isResultOfSlice = true;
			}
		}
	}
}
