using UnityEngine;
using System.Collections.Generic;
using NobleMuffins.MuffinSlicer;

public class Sliceable : MonoBehaviour, ISliceable
{
	public bool currentlySliceable = true;
	
	public bool refreshColliders = true;
	public TurboSlice.InfillConfiguration[] infillers = new TurboSlice.InfillConfiguration[0];
	
	public bool channelNormals = true;
	public bool channelTangents = false;
	public bool channelUV2 = false;

	public GameObject explicitlySelectedMeshHolder = null;
	
	public GameObject meshHolder {
		get {
			if(explicitlySelectedMeshHolder == null)
			{
				Component[] renderers = GetComponentsInChildren(typeof(Renderer), true);
				if(renderers.Length > 0)
				{
					explicitlySelectedMeshHolder = renderers[0].gameObject;
				}
			}
			return explicitlySelectedMeshHolder;
		}
	}
	
	public Object alternatePrefab = null;
	public bool alwaysCloneFromAlternate = false;
	
	public GameObject[] Slice(Vector3 positionInWorldSpace, Vector3 normalInWorldSpace)
	{
		if(currentlySliceable)
		{
			Matrix4x4 worldToLocalMatrix = transform.worldToLocalMatrix;
			
			Vector3 position = worldToLocalMatrix.MultiplyPoint3x4(positionInWorldSpace);
			Vector3 normal = worldToLocalMatrix.MultiplyVector(normalInWorldSpace).normalized;
			
			Vector4 planeInLocalSpace = MuffinSliceCommon.planeFromPointAndNormal(position, normal);
			
			return TurboSlice.instance.splitByPlane(gameObject, planeInLocalSpace, true);
		}
		else
		{				
			return new[] { gameObject };
		}
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
		else if(alwaysCloneFromAlternate)
		{
			return true;
		}
		else
		{
			AbstractSliceHandler[] handlers = gameObject.GetComponents<AbstractSliceHandler>();
			
			foreach(AbstractSliceHandler handler in handlers)
			{
				if(handler.cloneAlternate( hierarchyPresence ))
				{
					return true;
				}
			}
		
			return false;
		}
	}
}
