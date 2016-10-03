using UnityEngine;
using System.Collections;

public class ChildOfHackable : MonoBehaviour, ISliceable
{
	[HideInInspector]
	public Hackable parentHackable;
	
	void Start()
	{
		if(parentHackable == null)
		{
			Debug.LogWarning("Unconfigured ChildOfHackable found. Removing. If you added this to an object yourself, please remove it.");
			GameObject.DestroyImmediate(this);
		}
	}

	public GameObject[] Slice (Vector3 positionInWorldSpace, Vector3 normalInWorldSpace)
	{		
		return parentHackable.Slice(positionInWorldSpace, normalInWorldSpace);
	}
}
