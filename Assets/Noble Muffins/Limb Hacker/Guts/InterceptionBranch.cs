using UnityEngine;
using System.Collections.Generic;
using NobleMuffins.MuffinSlicer;

partial class LimbHacker
{
	private void applyAnimationState(Animation original, GameObject _target)
	{
		Animation target = _target.GetComponentInChildren<Animation>();
		
		if(target != null)
		{
			foreach(AnimationState state in original)
			{
				AnimationState other = target[state.name];
				
				other.blendMode = state.blendMode;
				other.enabled = state.enabled;
				other.layer = state.layer;
				other.normalizedSpeed = state.normalizedSpeed;
				other.normalizedTime = state.normalizedTime;
				other.speed = state.speed;
				other.time = state.time;
				other.weight = state.weight;
				other.wrapMode = state.wrapMode;
			}
		}
	}

	private void createResultObjects(GameObject go, Hackable sliceable,
		Dictionary<string,Transform> transformByName,
		Dictionary<string,bool> frontPresence, Dictionary<string,bool> backPresence,
		out GameObject frontObject, out GameObject backObject)
	{
		Transform goTransform = go.transform;
				
		bool useAlternateForFront, useAlternateForBack;
		
		if(sliceable.alternatePrefab == null)
		{
			useAlternateForFront = false;
			useAlternateForBack = false;
		}
		else
		{
			useAlternateForFront = sliceable.cloneAlternate(frontPresence);
			useAlternateForBack = sliceable.cloneAlternate(backPresence);
		}
		
		bool backIsNew = useAlternateForBack;
				
		if(backIsNew)
		{
			Object backSource = useAlternateForBack ? sliceable.alternatePrefab : go;
			backObject = (GameObject) GameObject.Instantiate(backSource);
		}
		else
			backObject = go;
		
		
		Object frontSource = useAlternateForFront ? sliceable.alternatePrefab : go;
		frontObject = (GameObject) GameObject.Instantiate(frontSource);
		
		handleHierarchy(frontObject.transform, frontPresence, transformByName);
		handleHierarchy(backObject.transform, backPresence, transformByName);
		
		Transform parent = goTransform.parent;
		
		Vector3 position = goTransform.localPosition;
		Vector3 scale = goTransform.localScale;
		
		Quaternion rotation = goTransform.localRotation;
		
		frontObject.transform.parent = parent;
		frontObject.transform.localPosition = position;
		frontObject.transform.localScale = scale;
		
		frontObject.transform.localRotation = rotation;
		
		frontObject.layer = go.layer;

		frontObject.name = "LHR Front";
		backObject.name = "LHR Back";
		
		if(backIsNew)
		{

			backObject.transform.parent = parent;
			backObject.transform.localPosition = position;
			backObject.transform.localScale = scale;
			
			backObject.transform.localRotation = rotation;
			
			backObject.layer = go.layer;
			
			GameObject.Destroy(go);
		}
	}
	
}
