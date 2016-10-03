using UnityEngine;
using System.Collections.Generic;
using NobleMuffins.MuffinSlicer;

partial class TurboSlice
{
	private void handleHierarchy(Transform root, Dictionary<string,bool> presenceByName, Dictionary<string,Transform> originalsByName)
	{
		List<Transform> allChildren = new List<Transform>( presenceByName.Count );
		
		concatenateHierarchy(root, allChildren);
		
		foreach(Transform t in allChildren)
		{
			GameObject go = t.gameObject;
			
			//bool thisIsTheSkinnedMeshRenderer = go.renderer != null && go.renderer is SkinnedMeshRenderer;
			
			string key = t.name;
			
			bool shouldBePresent = presenceByName.ContainsKey(key) ? presenceByName[key] : false;

			shouldBePresent &= originalsByName.ContainsKey(key) && originalsByName[key].gameObject.activeSelf;
			
			go.SetActive( shouldBePresent ); // || thisIsTheSkinnedMeshRenderer;
		}
		
		foreach(Transform t in allChildren)
		{
			string key = t.name;
			
			if(originalsByName.ContainsKey(key))
			{
				Transform original = originalsByName[key];
				
				t.localPosition = original.localPosition;
				t.localRotation = original.localRotation;
				t.localScale = original.localScale;
			}
		}
	}

	private void determinePresence(Transform root, Vector4 plane, out Dictionary<string,Transform> transformByName, out Dictionary<string,bool> frontPresence, out Dictionary<string,bool> backPresence)
	{
		List<Transform> allChildren = new List<Transform>();
		
		concatenateHierarchy(root, allChildren);
		
		Vector3[] positions = new Vector3[allChildren.Count];
		
		for(int i = 0; i < positions.Length; i++)
		{
			positions[i] = allChildren[i].position;
		}
		
		Matrix4x4 worldToLocal = root.worldToLocalMatrix;
		
		for(int i = 0; i < positions.Length; i++)
		{
			positions[i] = worldToLocal.MultiplyPoint3x4(positions[i]);
		}
		
		PlaneTriResult[] ptr = new PlaneTriResult[positions.Length];
		
		for(int i = 0; i < positions.Length; i++)
		{
			ptr[i] = MuffinSliceCommon.getSidePlane(ref positions[i], ref plane);
		
		}
		
		transformByName = new Dictionary<string, Transform>();
		frontPresence = new Dictionary<string, bool>();
		backPresence = new Dictionary<string, bool>();
		
		bool duplicateNameWarning = false;
	
		for(int i = 0; i < ptr.Length; i++)
		{
			Transform t = allChildren[i];
			string key = t.name;
			
			if(transformByName.ContainsKey(key))
				duplicateNameWarning = true;
			
			transformByName[key] = t;
				
			frontPresence[key] = ptr[i] == PlaneTriResult.PTR_FRONT;
			backPresence[key] = ptr[i] == PlaneTriResult.PTR_BACK;
		}
		
		if(duplicateNameWarning)
			Debug.LogWarning("Sliceable has children with non-unique names. Behaviour is undefined!");
	}
	
	void concatenateHierarchy(Transform t, List<Transform> results)
	{
		foreach(Transform child in t)
		{
			results.Add(child);
			concatenateHierarchy(child, results);
		}
	}
}
