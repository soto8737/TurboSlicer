using UnityEngine;
using NobleMuffins.MuffinSlicer;

public partial class TurboSlice
{
	[System.Serializable]
	public class InfillConfiguration {
		public Material material;
		public Rect regionForInfill;
	}
	
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
	
	//This here is a mesh cache. A mesh cache contains data for both a collection of slice results (multiples might refer to
	//a single cache) or for a preloaded mesh. When preloaded mesh A is split, it will yield meshs B and C and its mesh
	//cache X will be duplicated into mesh cache Y that meshes B, C and all further derivatives will refer to. When B, C and
	//all other derivatives have fallen away from the screen, their respective mesh cache will be zapped.
	class MeshCache
	{		
		public readonly float creationTime = Time.time;
		
		public TurboList<Vector3> vertices;
		public TurboList<Vector3> normals;
		public TurboList<Vector2> coords;
		public TurboList<Vector2> coords2;

		public int[][] indices;
	
		public Material[] mats;
	}
	
	private MeshCache cacheFromGameObject(Sliceable sliceable, bool includeRoomForGrowth)
	{
		Renderer renderer = getMeshRenderer(sliceable);
		
		Mesh m = getMesh(sliceable);
		
		int initialCapacity = includeRoomForGrowth ? Mathf.RoundToInt((float) m.vertexCount * factorOfSafetyGeometry) : m.vertexCount;
		
		MeshCache c = new MeshCache();
		
		c.vertices = new TurboList<Vector3>(initialCapacity);
		if(sliceable.channelNormals) c.normals = new TurboList<Vector3>(initialCapacity);
		c.coords = new TurboList<Vector2>(initialCapacity);
		if(sliceable.channelUV2) c.coords2 = new TurboList<Vector2>(initialCapacity);
		
		c.indices = new int[m.subMeshCount][];
		
		for(int i = 0; i < m.subMeshCount; i++)
		{
			c.indices[i] = m.GetTriangles(i);
		}
		
		c.vertices.AddArray(m.vertices);
		if(sliceable.channelNormals) c.normals.AddArray(m.normals);
		c.coords.AddArray(m.uv);
		if(sliceable.channelUV2) c.coords2.AddArray(m.uv2);
		
		if(renderer != null)
		{
			if(renderer.sharedMaterials == null)
			{
				c.mats = new Material[1];
				c.mats[0] = renderer.sharedMaterial;
			}
			else
			{
				c.mats = renderer.sharedMaterials;
			}
		}
		else
		{
			Debug.LogError("Object '" + sliceable.name + "' has no renderer");
		}
		
		return c;
	}
	
	private static Renderer getMeshRenderer(Sliceable s)
	{
		GameObject holder = getMeshHolder(s);
		
		if(holder != null)
		{
			return holder.GetComponent(typeof(Renderer)) as Renderer;
		}
		else
		{
			return null;
		}
	}

	private Mesh getMesh(Sliceable s)
	{
		GameObject holder = getMeshHolder(s);
		Renderer renderer = holder.GetComponent<Renderer>();
		Mesh mesh = null;
		if(renderer is MeshRenderer)
		{
			mesh = holder.GetComponent<MeshFilter>().mesh;
		}
		else if(renderer is SkinnedMeshRenderer)
		{
			SkinnedMeshRenderer smr = renderer as SkinnedMeshRenderer;
			mesh = new Mesh();
			smr.BakeMesh(mesh);
			meshDeletionQueue.Enqueue(mesh);
		}
		return mesh;
	}

	private static void setMesh(Sliceable s, Mesh mesh)
	{
		GameObject holder = getMeshHolder(s);
		Renderer renderer = holder.GetComponent<Renderer>();
		MeshFilter filter = null;
		if(renderer is MeshRenderer)
		{
			filter = holder.GetComponent<MeshFilter>();
		}
		else if(renderer is SkinnedMeshRenderer)
		{
			holder = s.explicitlySelectedMeshHolder = s.gameObject;
			Material[] allMats = renderer.sharedMaterials;
			GameObject.DestroyImmediate(renderer);
			renderer = holder.AddComponent<MeshRenderer>();
			renderer.sharedMaterials = allMats;
			filter = holder.AddComponent<MeshFilter>();
		}
		if(filter != null) filter.mesh = mesh;
	}

	private static GameObject getMeshHolder(Sliceable s)
	{
		if(s.explicitlySelectedMeshHolder != null)
		{
			return s.explicitlySelectedMeshHolder;
		}
		else
		{
			MeshFilter[] allFilters = s.GetComponentsInChildren<MeshFilter>(true);
			
			if(allFilters.Length > 0)
			{
				return allFilters[0].gameObject;
			}
			else
			{
				return null;
			}
		}
	}
	
	private static Sliceable ensureSliceable(GameObject go)
	{
		Sliceable sliceable = go.GetComponent<Sliceable>();
		
		if(sliceable == null)
		{
			Debug.LogWarning("Turbo Slicer was given an object (" + go.name + ") with no Sliceable; improvising.");
			
			sliceable = go.AddComponent<Sliceable>();
			sliceable.currentlySliceable = true;
			sliceable.refreshColliders = true;
		}
		
		return sliceable;
	}

	private int[] concatenateIndexArrays(int[][] arrays)
	{
		int totalLength = 0;

		for(int i = 0; i < arrays.Length; i++)
		{
			totalLength += arrays[i].Length;
		}

		int[] newArray = new int[totalLength];
		int destination = 0;

		for(int i = 0; i < arrays.Length; i++)
		{
			System.Array.Copy(arrays[i], 0, newArray, destination, arrays[i].Length);
		}

		return newArray;
	}
}
