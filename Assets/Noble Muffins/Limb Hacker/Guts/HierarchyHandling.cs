using UnityEngine;
using System.Collections.Generic;
using NobleMuffins.MuffinSlicer;

partial class LimbHacker
{
	private void handleHierarchy(Transform root, Dictionary<string,bool> bonePresenceByName, Dictionary<string,Transform> originalsByName)
	{
		List<Transform> bones = new List<Transform>();
		
		{
			SkinnedMeshRenderer smr = root.GetComponentInChildren<SkinnedMeshRenderer>();
			
			if(smr != null)
			{
				Transform[] _bones = smr.bones;
			
				bones.AddRange(_bones);	
				
				//  Hierarchies often have transforms between bones and the root that are not
				// part of the bones collection pulled from the SMR. However if we turn these
				// intermediaries off, the ragdoll will not work. For the purposes of this
				// procedure, we're going to treat these AS bones.
				
				foreach(Transform bone in _bones)
				{
					Transform parent = bone.parent;
					
					if(bones.Contains(parent) == false)
					{
						bones.Add(parent);
					}
				}
			}
		}
		
		List<Transform> allChildren = new List<Transform>( bonePresenceByName.Count );
		
		concatenateHierarchy(root, allChildren);
		
		foreach(Transform t in allChildren)
		{
			GameObject go = t.gameObject;
			
			bool thisIsTheSkinnedMeshRenderer = go.GetComponent<Renderer>() != null && go.GetComponent<Renderer>() is SkinnedMeshRenderer;
			
			string key = t.name;
			
			bool shouldBePresent = true;
			
			Transform presenceKeySource = t;
			do
			{
				string presenceKey = presenceKeySource.name;
				if(bonePresenceByName.ContainsKey(presenceKey))
				{
					shouldBePresent = bonePresenceByName[presenceKey];
					break;
				}
				else
				{
					presenceKeySource = presenceKeySource.parent;
				}
			}
			while(allChildren.Contains( presenceKeySource ));
			
			shouldBePresent &= originalsByName.ContainsKey(key) ? originalsByName[key].gameObject.activeInHierarchy : false;
			
			bool isBone = bones.Contains(t);
			
			if(!shouldBePresent && isBone)
			{
				Collider c = t.GetComponent<Collider>();
				
				if(c != null)
					c.enabled = shouldBePresent;
				
				Rigidbody r = t.GetComponent<Rigidbody>();
				CharacterJoint j = t.GetComponent<CharacterJoint>();
				if(r != null && j != null)
				{
					//r.isKinematic = true;
					//j.connectedBody = null;
					r.mass = 0.001f;
				}
				else if(r != null)
				{
					r.mass = 0.001f;
					//r.useGravity = false;
				}
			}
			else
			{
				go.SetActive(shouldBePresent || thisIsTheSkinnedMeshRenderer);
			}
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

	private static SkinnedMeshRenderer GetSkinnedMeshRendererWithName(GameObject root, string name)
	{
		var allSMRs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

		foreach(var smr in allSMRs)
		{
			if(smr.name.Equals(name))
			{
				return smr;
			}
		}

		return null;
	}

	private ICollection<Transform> GetConcatenatedHierarchy(Transform t)
	{
		var children = new HashSet<Transform>() as ICollection<Transform>;
		concatenateHierarchy(t, children);
		return children;
	}

	void concatenateHierarchy(Transform t, ICollection<Transform> results)
	{
		for(int i = 0; i < t.childCount; i++)
		{
			Transform child = t.GetChild(i);
			//Debug.Log("Adding child: " + child.name + " (of " + t.name + ", " + t.GetChildCount() + ")");
			results.Add(child);
			concatenateHierarchy(child, results);
		}
	}
}
