using UnityEngine;
using System.Collections.Generic;

partial class LimbHacker
{
	public GameObject[] severByPoint(GameObject go, Vector3 pointInWorldSpace)
	{
		return severByPointAndNormal(go, pointInWorldSpace, null);
	}
	
	public GameObject[] severByPointAndNormal(GameObject go, Vector3 pointInWorldSpace, Vector3? normalInWorldSpace)
	{
		Hackable hackable = go.GetComponent<Hackable>();
		
		if(hackable == null)
		{
			Debug.LogError("GameObject '" + go.name + "' cannot be sliced by point because it has no Hackable component. Please look for the 'slice by point' chapter in the manual.");
			
			GameObject[] blankResult = { go };
			return blankResult;
		}
		else
		{
			string boneName = null;
			float rootTipProgression = 0f;
			
			if(determineSlice(hackable, pointInWorldSpace, ref boneName, ref rootTipProgression))
			{
				return LimbHacker.instance.severByJoint(go, boneName, rootTipProgression, normalInWorldSpace);
			}
			else
			{
				GameObject[] notMuch = { go };
				return notMuch;
			}
		}
	}
	
	public bool determineSlice(Hackable hackable, Vector3 pointInWorldSpace, ref string boneName, ref float rootTipProgression)
	{
		const int nothing = -1;
		
		Transform[] severables = hackable.severables;
		
		Dictionary<Transform,int> indexByObject = new Dictionary<Transform, int>();
		for(int i = 0; i < severables.Length; i++)
		{
			indexByObject[ severables[i] ] = i;
		}
		
		Vector3[] severablesInThreeSpace = new Vector3[ severables.Length ];
		for(int i = 0; i < severables.Length; i++)
		{
			severablesInThreeSpace[i] = severables[i].position;
		}
		
		Vector3[] deltas = new Vector3 [ severables.Length ];
		for(int i = 0; i < severables.Length; i++)
		{
			deltas[i] = severablesInThreeSpace[i] - pointInWorldSpace;
		}
		
		float[] mags = new float[ severables.Length ];
		for(int i = 0; i < severables.Length; i++)
		{
			mags[i] = deltas[i].magnitude;
		}
		
		int indexOfNearestThing = nothing;
		float distanceToNearestThing = float.PositiveInfinity;
		for(int i = 0; i < severables.Length; i++)
		{
			if(mags[i] < distanceToNearestThing)
			{
				indexOfNearestThing = i;
				distanceToNearestThing = mags[i];
			}
		}
		
		if(indexOfNearestThing != nothing)
		{			
			Transform nearestThing = severables[indexOfNearestThing];
					
			if(indexByObject.ContainsKey( nearestThing.parent ))
			{
				int parentIndex = indexByObject[nearestThing.parent];
				
				Vector3 hereDelta = severablesInThreeSpace[ indexOfNearestThing ] - severablesInThreeSpace[ parentIndex ];
				
				Vector3 touchDelta = pointInWorldSpace - severablesInThreeSpace[ parentIndex ];
				
				//If the touch is closer to the parent than the severable is, than it's between them.
				//We'll use that and then use the root tip progression to slice just the right spot.
				if(touchDelta.magnitude < hereDelta.magnitude)
				{
					indexOfNearestThing = parentIndex;
					nearestThing = severables[ indexOfNearestThing ];
				}
			}
			
			List<int> childIndices = new List<int>();
			
			for(int i = 0; i < severables.Length; i++)
			{
				Transform candidate = severables[i];
				
				if(candidate.parent == nearestThing)
				{
					childIndices.Add(i);
				}
			}
			
			rootTipProgression = 0f;
			
			if(childIndices.Count > 0)
			{
				Vector3 aggregatedChildPositions = Vector3.zero;
				
				foreach(int i in childIndices)
				{
					aggregatedChildPositions += severablesInThreeSpace[i];
				}

				Vector3 meanChildPosition = aggregatedChildPositions / ( (float) childIndices.Count );
				
				Matrix4x4 flattenTransform;
				
				Vector3 v1 = Vector3.forward;
				Vector3 v2 = (severablesInThreeSpace[indexOfNearestThing] - meanChildPosition ).normalized;
				Vector3 v3 = Vector3.Cross( v1, v2 ).normalized;
				Vector3 v4 = Vector3.Cross( v3, v1 );
				
				float cos = Vector3.Dot(v2, v1);
				float sin = Vector3.Dot(v2, v4);
				
				Matrix4x4 m1 = Matrix4x4.identity;
				m1.SetRow(0, (Vector4) v1);
				m1.SetRow(1, (Vector4) v4);
				m1.SetRow(2, (Vector4) v3);
				
				Matrix4x4 m1i = m1.inverse;
				
				Matrix4x4 m2 = Matrix4x4.identity;
				m2.SetRow(0, new Vector4(cos, sin, 0, 0) );
				m2.SetRow(1, new Vector4(-sin, cos, 0, 0) );
				
				flattenTransform = m1i * m2 * m1;
				
				Vector3 transformedChildPosition = flattenTransform.MultiplyPoint( meanChildPosition );
				Vector3 transformedRootPosition = flattenTransform.MultiplyPoint( severablesInThreeSpace[indexOfNearestThing] );
				Vector3 transformedTouchPosition = flattenTransform.MultiplyPoint( pointInWorldSpace );
							
				rootTipProgression = 1f - Mathf.Clamp((transformedTouchPosition.z - transformedChildPosition.z) / (transformedRootPosition.z - transformedChildPosition.z), 0.05f, 1f);
			}
			
			boneName = nearestThing.name;
			
			return true;
		}
		else
		{
			return false;
		}
	}
}
