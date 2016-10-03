#define SHIBBY

using UnityEngine;
using System.Collections.Generic;
using NobleMuffins.MuffinSlicer;
using System.Linq;
using System.Text;

public partial class LimbHacker: MonoBehaviour
{
	public enum Infill { Sloppy, Meticulous };
	
	// Suppose we have source model A and we slice it. We create a mesh cache X (cloned from it) which is going to contain
	//the geometrical for the resulting slices and ALL subsequent slices. A number too large will result in unneccessarily
	//large memory allocations while a figure too small will result in frequent time-consuming reallocations as the vector
	//must be expanded to accomodate the growing geometry. Note that when a lineage is finally, entirely off screen, this
	//memory will be dereferenced such that the VM can release it at its discretion.
	// This number (9/2) was arrived at experimentally; at this number, reallocations are rare ( < 3 times a minute ).
	const float factorOfSafetyGeometry = 9f / 2f;
	
	// This is a little different as indices are -not- retained. This is how much we need to allocate for each resultant mesh,
	//compared to the original. I have set it assume that resultant meshes may be up to 90% the complexity of originals because
	//a highly uneven slice (a common occurrence) will result in this.
	const float factorOfSafetyIndices = 0.9f;
	
	private static LimbHacker _instance;
	public static LimbHacker instance {
		get {
			if(_instance == null)
			{
				GameObject go = new GameObject();
				_instance = go.AddComponent<LimbHacker>();
			}
			return _instance;
		}
	}
	
	// Use this for initialization
	void Start ()
	{
		_instance = this;
	}
	
	
	public GameObject[] severByJoint(GameObject go, string jointName)
	{
		return severByJoint(go, jointName, 0f, Vector3.zero);
	}
	
	public GameObject[] severByJoint(GameObject go, string jointName, float rootTipProgression, Vector3? planeNormal)
	{
		rootTipProgression = Mathf.Clamp01(rootTipProgression);

		//These here are in local space because they're only used to copy to the resultant meshes; they're not used
		//to transform the vertices. We expect a world-space slice input.

		Hackable hackable = null;

		{
			Hackable[] hackables = go.GetComponentsInChildren<Hackable>();
			
			if(hackables.Length > 0)
			{
				if(hackables.Length > 1)
				{
					Debug.LogWarning("Limb Hacker found multiple slice configurations on object '" + go.name + "' in scene '" + Application.loadedLevelName + "'! Behavior is undefined.");				
				}
				
				hackable = hackables[0];
			}
		}
		
		//We need information about which BONES are getting severed.

		var allBones = LimbHacker.FindBonesInTree(go);

		var childTransformByName = new Dictionary<string, Transform>();
		var parentKeyByKey = new Dictionary<string,string>();
		
		foreach(Transform t in GetConcatenatedHierarchy(go.transform))
		{
			childTransformByName[t.name] = t;
			
			Transform parent = t.parent;
			
			if(t == go.transform)
				parent = null;

			parentKeyByKey[t.name] = parent == null ? null : parent.name;
		}
				
		var severedByChildName = new Dictionary<string, bool>();

		{
			foreach(string childName in childTransformByName.Keys)
			{
				severedByChildName[childName] = childName == jointName;
			}
			
			bool changesMade;
			do
			{
				changesMade = false;
				
				foreach(string childKey in childTransformByName.Keys)
				{
					bool severed = severedByChildName[childKey];
					
					if(severed)
						continue;
					
					string parentKey = parentKeyByKey[childKey];
					
					bool parentSevered;
					
					if(severedByChildName.TryGetValue(parentKey, out parentSevered) == false)
						continue;
					
					if(parentSevered)
					{
						severedByChildName[childKey] = true;
						
						changesMade = true;
					}
				}
			}
			while(changesMade);
		}
		
		GameObject frontObject, backObject;
		
		{
			var bonePresenceFront = new Dictionary<string, bool>();
			var bonePresenceBack = new Dictionary<string, bool>();
			
			foreach(KeyValuePair<string,bool> kvp in severedByChildName)
			{
				bonePresenceFront[kvp.Key] = kvp.Value;
				bonePresenceBack[kvp.Key] = !kvp.Value;
			}
			
			createResultObjects(go, hackable, childTransformByName, bonePresenceFront, bonePresenceBack, out frontObject, out backObject);
		}

		var skinnedMeshRenderers = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);

		foreach(var smr in skinnedMeshRenderers)
		{
			var m = smr.sharedMesh;
			LoadSkinnedMeshRendererIntoCache(smr, true);

			var severedByBoneIndex = new Dictionary<int,bool>();
			var mandatoryByBoneIndex = new bool[smr.bones.Length];
			
			string severedJointKey = jointName;

			Dictionary<string,int> boneIndexByName = new Dictionary<string, int>();
			List<string> orderedBoneNames = new List<string>();
			
			foreach(Transform bone in smr.bones)
			{
				boneIndexByName[bone.name] = orderedBoneNames.Count;
				orderedBoneNames.Add(bone.name);
			}

			for(int boneIndex = 0; boneIndex < orderedBoneNames.Count; boneIndex++)
			{
				string boneName = orderedBoneNames[boneIndex];
				
				severedByBoneIndex[boneIndex] = severedByChildName[boneName];
			}
			
			Vector4 plane = Vector4.zero;

			bool willSliceThisMesh = boneIndexByName.ContainsKey(severedJointKey);

			if(willSliceThisMesh)
			{
				//We need to create a slice plane in local space. We're going to do that by using the bind poses
				//from the SEVERED limb, its PARENT and its CHILDREN to create a position and normal.

				Matrix4x4[] orderedBindPoses = smr.sharedMesh.bindposes;
				
				int severedJointIndex = boneIndexByName[severedJointKey];
				
				Matrix4x4 severedJointMatrix = orderedBindPoses[severedJointIndex].inverse;
				
				Matrix4x4 severedJointParentMatrix = Matrix4x4.identity;
				
				if(parentKeyByKey.ContainsKey(severedJointKey))
				{
					string severedJointParentKey = parentKeyByKey[severedJointKey];
					
					if(boneIndexByName.ContainsKey(severedJointParentKey))
					{
						int severedJointParentIndex = boneIndexByName[severedJointParentKey];
						
						severedJointParentMatrix = orderedBindPoses[severedJointParentIndex].inverse;
					}
				}
				
				VectorAccumulator meanChildPosition = new VectorAccumulator();
				
				for(int i = 0; i < boneIndexByName.Count; i++)
				{
					mandatoryByBoneIndex[i] = false;
				}
				
				if(parentKeyByKey.ContainsKey(severedJointKey))
				{
					string parentKey = parentKeyByKey[ severedJointKey ];
					if(boneIndexByName.ContainsKey(parentKey))
					{
						mandatoryByBoneIndex[ boneIndexByName[ parentKey ] ] = true;
					}
				}
				
				if(rootTipProgression > 0f)
				{
					mandatoryByBoneIndex[ boneIndexByName[ jointName ] ] = true;
					
					List<string> childKeys = new List<string>();
					foreach(KeyValuePair<string,string> kvp in parentKeyByKey)
					{
						if(kvp.Value == severedJointKey)
						{
							childKeys.Add(kvp.Key);
						}
					}
					
					List<int> childIndices = new List<int>();
					foreach(string key in childKeys)
					{
						int childIndex;

						if(boneIndexByName.TryGetValue(key, out childIndex))
						{
							childIndices.Add(childIndex);
						}
					}
					
					foreach(int index in childIndices)
					{						
						Matrix4x4 childMatrix = orderedBindPoses[index].inverse;
						
						Vector3 childPosition = childMatrix.MultiplyPoint3x4( Vector3.zero );
						
						meanChildPosition.addFigure( childPosition );
					}
				}
				
				Vector3 position0 = severedJointParentMatrix.MultiplyPoint3x4( Vector3.zero );
				Vector3 position1 = severedJointMatrix.MultiplyPoint3x4( Vector3.zero );
				Vector3 position2 = meanChildPosition.mean;
				
				Vector3 deltaParent = position0 - position1;
				Vector3 deltaChildren = position1 - position2;
				
				Vector3 position = Vector3.Lerp(position1, position2, rootTipProgression);
				
				Vector3 normalFromParentToChild = -Vector3.Lerp(deltaParent, deltaChildren, rootTipProgression).normalized;
				
				if(planeNormal.HasValue)
				{
					Matrix4x4 fromWorldToLocalSpaceOfBone = smr.bones[severedJointIndex].worldToLocalMatrix;
					
					Vector3 v = planeNormal.Value;
					v = fromWorldToLocalSpaceOfBone.MultiplyVector(v);
					v = severedJointMatrix.MultiplyVector(v);
					v.Normalize();
					
					if(Vector3.Dot(v, normalFromParentToChild) < 0f)
					{
						v = -v;
					}
					
					v = MuffinSliceCommon.clampNormalToBicone(v, normalFromParentToChild, 30f);
					
					planeNormal = v;
				}
				else
				{
					planeNormal = normalFromParentToChild;
				}
				
				plane = (Vector4) planeNormal.Value;
				
				plane.w = -(plane.x * position.x + plane.y * position.y + plane.z * position.z);
			}
					
			//We're going to create two new tentative meshes which contain ALL original vertices in order,
			//plus room for new vertices. Not all of these copied vertices will be addressed, but copying them
			//over eliminates the need to remove doubles and do an On^2 search.
			
			int submeshCount = c.indices.Length;
			
			TurboList<int>[] _frontIndices = new TurboList<int>[ submeshCount ];
			TurboList<int>[] _backIndices = new TurboList<int>[ submeshCount ];
			
			PlaneTriResult[] sidePlanes = new PlaneTriResult[c.vertices.Count];
			{
				BoneWeight[] weights = c.weights.array;
				Vector3[] vertices = c.vertices.array;
				int count = c.vertices.Count;
				
				bool[] whollySeveredByVertexIndex = new bool[count];
				bool[] severableByVertexIndex = new bool[count];
				bool[] mandatoryByVertexIndex = new bool[count];
				
				const float minimumWeightForRelevance = 0.1f;
					
				for(int i = 0; i < severableByVertexIndex.Length; i++)
				{
					BoneWeight weight = weights[i];
					
					bool whollySevered = true;				
					bool severable = false;
					bool mandatory = false;
					
					int[] indices = { weight.boneIndex0, weight.boneIndex1, weight.boneIndex2, weight.boneIndex3 };
					float[] scalarWeights = { weight.weight0, weight.weight1, weight.weight2, weight.weight3 };
					
					for(int j = 0; j < 4; j++)
					{
						if(scalarWeights[j] > minimumWeightForRelevance)
						{
							int index = indices[j];
							bool _severable = severedByBoneIndex[ index ];
							bool _mandatory = mandatoryByBoneIndex[ index ];
							whollySevered &= _severable;
							severable |= _severable;
							mandatory |= _mandatory;
						}
					}
						
					whollySeveredByVertexIndex[i] = whollySevered;
					severableByVertexIndex[i] = severable;
					mandatoryByVertexIndex[i] = mandatory;
				}
				
				for(int i = 0; i < sidePlanes.Length; i++)
				{
					if(willSliceThisMesh && mandatoryByVertexIndex[i])
						sidePlanes[i] = MuffinSliceCommon.getSidePlane(ref vertices[i], ref plane);
					else if(whollySeveredByVertexIndex[i])
						sidePlanes[i] = PlaneTriResult.PTR_FRONT;
					else if(willSliceThisMesh && severableByVertexIndex[i])
						sidePlanes[i] = MuffinSliceCommon.getSidePlane(ref vertices[i], ref plane);
					else
						sidePlanes[i] = PlaneTriResult.PTR_BACK;
				}
			}

			TurboList<int> frontInfill = null;
			TurboList<int> backInfill = null;
			
			for(int j = 0; j < submeshCount; j++)
			{	
				int initialCapacityIndices = Mathf.RoundToInt((float) c.indices[j].Length * factorOfSafetyIndices);
				
				_frontIndices[j] = new TurboList<int>(initialCapacityIndices);
				_backIndices[j] = new TurboList<int>(initialCapacityIndices);
				
				if(hackable.infillMaterial != null && c.mats[j] == hackable.infillMaterial)
				{
					frontInfill = _frontIndices[j];
					backInfill = _backIndices[j];
				}
			}
			
			if(hackable.infillMaterial != null && frontInfill == null)
			{
				frontInfill = new TurboList<int>(1024);
				backInfill = new TurboList<int>(1024);
			}
			
			for(int j = 0; j < submeshCount; j++)
			{
				int initialCapacityIndices = Mathf.RoundToInt((float) c.indices[j].Length * factorOfSafetyIndices);
				
				int[] _indices = c.indices[j];
				
				TurboList<int> frontIndices = _frontIndices[j];
				TurboList<int> backIndices = _backIndices[j];
				TurboList<int> splitPending = new TurboList<int>(initialCapacityIndices);

				int[] indices = new int[3];
				
				for(int i = 0; i < _indices.Length; )
				{	
					indices[0] = _indices[i++];
					indices[1] = _indices[i++];
					indices[2] = _indices[i++];
					
					// compute the side of the plane each vertex is on
					PlaneTriResult r1 = sidePlanes[indices[0]];
					PlaneTriResult r2 = sidePlanes[indices[1]];
					PlaneTriResult r3 = sidePlanes[indices[2]];
					
					if ( r1 == r2 && r1 == r3 ) // if all three vertices are on the same side of the plane.
					{
						if ( r1 == PlaneTriResult.PTR_FRONT ) // if all three are in front of the plane, then copy to the 'front' output triangle.
						{
							frontIndices.AddArray(indices);
						}
						else
						{
							backIndices.AddArray(indices);
						}
					}
					else if(willSliceThisMesh)
					{
						splitPending.AddArray(indices);
					}
				}
			
				if(willSliceThisMesh)
				{
					splitTrianglesLH(plane, c.vertices.array, sidePlanes, splitPending.ToArray(), c, frontIndices, backIndices, hackable.infillMode, frontInfill, backInfill);
				}
			}
			
			if(hackable.infillMaterial != null)
			{
				bool alreadyPresent = System.Array.IndexOf<Material>(c.mats, hackable.infillMaterial) >= 0;
				
				if(!alreadyPresent)
				{	
					int oldLength = c.mats.Length, newLength = c.mats.Length + 1;
					
					Material[] newMats = new Material[ newLength ];
					System.Array.Copy(c.mats, newMats, oldLength);
					newMats[ newLength - 1 ] = hackable.infillMaterial;
					c.mats = newMats;
					
					TurboList<int>[] indexArray;
					
					indexArray = new TurboList<int>[ newLength ];
					System.Array.Copy(_backIndices, indexArray, oldLength );
					indexArray[ newLength - 1 ] = backInfill;
					_backIndices = indexArray;
					
					indexArray = new TurboList<int>[ newLength ];
					System.Array.Copy(_frontIndices, indexArray, oldLength );
					indexArray[ newLength - 1 ] = frontInfill;
					_frontIndices = indexArray;
					
					submeshCount++;
				}
			}

			Vector3[] geoSubsetOne, geoSubsetTwo;
			Vector3[] normalsSubsetOne, normalsSubsetTwo;
			Vector2[] uvSubsetOne, uvSubsetTwo;
			BoneWeight[] weightSubsetOne, weightSubsetTwo;
			int[][] indexSubsetOne, indexSubsetTwo;
			
			indexSubsetOne = new int[submeshCount][];
			indexSubsetTwo = new int[submeshCount][];

			targetSubsetOne.Clear();
			targetSubsetTwo.Clear ();
						
			int transferTableMaximumKey = c.vertices.Count;

			int[] transferTableOne = new int[transferTableMaximumKey];
			int[] transferTableTwo = new int[transferTableMaximumKey];
			
			for(int i = 0; i < transferTableOne.Length; i++) transferTableOne[i] = -1;
			for(int i = 0; i < transferTableTwo.Length; i++) transferTableTwo[i] = -1;

			for(int i = 0; i < submeshCount; i++)
				perfectSubsetRD(_frontIndices[i], c.vertices, c.normals, c.UVs, c.weights, out indexSubsetOne[i], targetSubsetOne, ref transferTableOne );
			
			for(int i = 0; i < submeshCount; i++)
				perfectSubsetRD(_backIndices[i], c.vertices, c.normals, c.UVs, c.weights, out indexSubsetTwo[i], targetSubsetTwo, ref transferTableTwo );
			
			//Note that we do not explicitly call recalculate bounds because (as per the manual) this is implicit in an
			//assignment to vertices whenever the vertex count changes from zero to non-zero.
			
			Mesh frontMesh = new Mesh();
			Mesh backMesh = new Mesh();			

			var frontSMR = GetSkinnedMeshRendererWithName(frontObject, smr.name);
			var backSMR = GetSkinnedMeshRendererWithName(backObject, smr.name);

			if(targetSubsetOne.vertices.Count > 0)
			{
				frontSMR.materials = c.mats;
				frontSMR.sharedMesh = frontMesh;
				frontMesh.vertices = targetSubsetOne.vertices.ToArray();
				frontMesh.normals = targetSubsetOne.normals.ToArray();
				frontMesh.uv = targetSubsetOne.UVs.ToArray();
				frontMesh.boneWeights = targetSubsetOne.weights.ToArray();
				frontMesh.subMeshCount = submeshCount;
				frontMesh.bindposes = m.bindposes;
				
				for(int i = 0 ; i < submeshCount; i++)
				{
					frontMesh.SetTriangles(indexSubsetOne[i], i);
				}
			}
			else
			{
				GameObject.DestroyImmediate(frontSMR);
			}

			if(targetSubsetTwo.vertices.Count > 0)
			{
				backSMR.materials = c.mats;
				backSMR.sharedMesh = backMesh;
				backMesh.vertices = targetSubsetTwo.vertices.ToArray();
				backMesh.normals = targetSubsetTwo.normals.ToArray();
				backMesh.uv = targetSubsetTwo.UVs.ToArray();
				backMesh.boneWeights = targetSubsetTwo.weights.ToArray();
				backMesh.subMeshCount = submeshCount;
				backMesh.bindposes = m.bindposes;

				for(int i = 0 ; i < submeshCount; i++)
				{
					backMesh.SetTriangles(indexSubsetTwo[i], i);
				}
			}
			else
			{
				GameObject.DestroyImmediate(backSMR);
			}
		}

		var results = new GameObject[] {
			frontObject, backObject
		};

		hackable.handleSlice(results);

		return results;
	}

	public class ForestException: System.Exception {
		public ForestException(string message): base(message) {
		}
	}

	private static bool IsThisChildOfThat(Transform subject, Transform parent)
	{
		do {
			subject = subject.parent;
		} while(subject != null && subject != parent);
		return subject == parent;
	}

	private static bool IsThisTheRootBone(Transform candidate, ICollection<Transform> boneSet)
	{
		//A candidate can't be anything but the root if its parent is null.

		if(candidate.parent == null)
		{
			return true;
		}

		//A candidate is NOT the root if its parent is a subset of the bone set.

		if(boneSet.Contains(candidate.parent))
		{
			return false;
		}

		//A candidate is the root bone if every other bone is a child of it.

		bool allOthersAreChildren = true;

		foreach(var bone in boneSet)
		{
			if(candidate == bone.parent)
			{
				continue;
			}
			if(IsThisChildOfThat(bone, candidate) == false)
			{
				allOthersAreChildren = false;
				break;
			}
		}

		return allOthersAreChildren;
	}

//	private static bool hasWarnedAboutNullRootNode = false;

	public static IEnumerable<Transform> FindBonesInTree(GameObject go)
	{
		var skinnedMeshRenderers = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);

		var bones = new HashSet<Transform>();

		var rootCandidates = new HashSet<Transform>();

		foreach(var smr in skinnedMeshRenderers)
		{
			if(smr.rootBone != null)
			{
				rootCandidates.Add(smr.rootBone);
			}
			else
			{
				//Just pick a bone and crawl up the path until we can verify that we found the root.

				var rootCandidate = smr.bones.First();

				while(LimbHacker.IsThisTheRootBone(rootCandidate, smr.bones))
				{
					rootCandidate = rootCandidate.parent;
				}

				rootCandidates.Add(rootCandidate);
				
//				if(!hasWarnedAboutNullRootNode)
//				{
//					Debug.LogWarning("Limb Hacker found SkinnedMeshRenderer on object '" + smr.name + "' which has no root bone defined. This means Limb Hacker will need to compute the root node at run time. Assign a root node manually to improve performance.");
//					hasWarnedAboutNullRootNode = true;
//				}
			}

			foreach(var bone in smr.bones)
			{
				bones.Add(bone);
			}
		}

		//LimbHacker requires a single tree; there must be precisely one root. Conceptually
		//a root has no parent. In Unity, the root may have a parent but that is fine provided
		//that the parent is not part of the bone set.

		//First we need to determine, from the set of root candidates, what the root is.
		//The root is the root candidate for which every other root is a child.

		Transform root = null;

		if(rootCandidates.Count == 1)
		{
			root = rootCandidates.First();
		}
		else if(rootCandidates.Count > 0)
		{
			foreach(var rootCandidate in rootCandidates)
			{
				bool valid = true; 

				foreach(var possibleChild in rootCandidates)
				{
					if(possibleChild == rootCandidate)
						continue;

					valid &= LimbHacker.IsThisChildOfThat(possibleChild, rootCandidate);

					if(!valid)
						break;
				}

				if(valid)
				{
					root = rootCandidate;
					break;
				}
			}
		}
		
		if(root == null) {
			var boneDescriptor = new StringBuilder();
			foreach(var bone in bones)
			{
				boneDescriptor.AppendFormat("{0}: {1}\n", bone.name, bone.parent == null ? "nil" : bone.parent.name);
			}
			throw new ForestException(string.Format("{0} does not have a single, valid tree. LimbHacker compatible objects must have a single bone tree. Tree dump:\n{1}", go.name, boneDescriptor.ToString()));
		}

		return bones;
	}
}
