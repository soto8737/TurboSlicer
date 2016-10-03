using UnityEngine;
using System.Collections.Generic;

public class Hackable : MonoBehaviour, ISliceable
{	
	public Object alternatePrefab = null;
	
	public Transform[] severables = new Transform[0];
	public Dictionary<string,float> maximumTiltBySeverableName = new Dictionary<string, float>();
	
	public Material infillMaterial = null;
	
	public LimbHacker.Infill infillMode = LimbHacker.Infill.Sloppy;
	
	private bool destructionPending = false;
	
	void Start()
	{
		Collider[] childColliders = gameObject.GetComponentsInChildren<Collider>();
		
		foreach(Collider c in childColliders)
		{
			GameObject go = c.gameObject;
			
			ChildOfHackable referencer = go.GetComponent<ChildOfHackable>();
			
			if(referencer == null)
				referencer = go.AddComponent<ChildOfHackable>();
			
			referencer.parentHackable = this;
		}
	}
	
	public GameObject[] Slice(Vector3 positionInWorldSpace, Vector3 normalInWorldSpace)
	{
		if(destructionPending) return new[] { gameObject };
		
		GameObject[] result = LimbHacker.instance.severByPointAndNormal(gameObject, positionInWorldSpace, normalInWorldSpace);
		
		bool originalRemainsAfterSlice = false;
		
		for(int i = 0; i < result.Length; i++) originalRemainsAfterSlice |= result[i] == gameObject;
		
		destructionPending = !originalRemainsAfterSlice;
		
		return result;
	}
	
	public void handleSlice( GameObject[] results )
	{
		AbstractSliceHandler[] handlers = gameObject.GetComponents<AbstractSliceHandler>();
		
		foreach(AbstractSliceHandler handler in handlers)
		{
			handler.handleSlice(results);
		}
	}
	
	public bool cloneAlternate( Dictionary<string,bool> hierarchyPresence )
	{
		if(alternatePrefab == null)
		{
			return false;
		}
		else
		{
			AbstractSliceHandler[] handlers = gameObject.GetComponents<AbstractSliceHandler>();
			
			bool result = false;
			
			if(handlers.Length == 0)
			{
				result = true;
			}
			else
			{
				foreach(AbstractSliceHandler handler in handlers)
				{
//					Debug.Log("Processing handler: " + handler.ToString());
					result |= handler.cloneAlternate( hierarchyPresence );
				}
			}
//			Debug.Log("Cloning from alternate: " + result);
			
			return result;
			
		}
	}
}
