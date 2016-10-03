using UnityEngine;
using System.Collections;
using NobleMuffins.MuffinSlicer;

partial class LimbHacker {

	class GeometryCache {
		public readonly TurboList<Vector3> vertices = new TurboList<Vector3>(0);
		public readonly TurboList<Vector3> normals = new TurboList<Vector3>(0);
		public readonly TurboList<Vector2> UVs = new TurboList<Vector2>(0);
		public readonly TurboList<BoneWeight> weights = new TurboList<BoneWeight>(0);

		public virtual void Clear() {
			vertices.Clear();
			normals.Clear();
			UVs.Clear();
			weights.Clear();
		}
	}

	class MeshCache: GeometryCache
	{
		public int[][] indices;
		public Material[] mats;

		public override void Clear() {
			base.Clear();

			indices = null;
			mats = null;
		}
	}

	private readonly MeshCache c = new MeshCache();
	private readonly GeometryCache targetSubsetOne = new GeometryCache();
	private readonly GeometryCache targetSubsetTwo = new GeometryCache();

	private void perfectSubsetRD(TurboList<int> _sourceIndices, TurboList<Vector3> _sourceVertices, TurboList<Vector3> _sourceNormals, TurboList<Vector2> _sourceUVs, TurboList<BoneWeight> _sourceWeights,
		out int[] targetIndices, GeometryCache target, ref int[] transferTable)
	{
		int[] sourceIndices = _sourceIndices.array;
		Vector3[] sourceVertices = _sourceVertices.array;
		Vector2[] sourceUVs = _sourceUVs.array;
		Vector3[] sourceNormals = _sourceNormals.array;
		BoneWeight[] sourceWeights = _sourceWeights.array;
		
		targetIndices = new int[_sourceIndices.Count];
				
		int targetIndex = target.vertices.Count;
		for(int i = 0; i < _sourceIndices.Count; i++)
		{
			int requestedVertex = sourceIndices[i];
			
			int j = transferTable[requestedVertex];
			
			if(j == -1)
			{
				j = targetIndex;
				transferTable[requestedVertex] = j;
				targetIndex++;
			}
			
			targetIndices[i] = j;
		}
		
		target.vertices.EnsureCapacity(targetIndex);
		target.normals.EnsureCapacity(targetIndex);
		target.UVs.EnsureCapacity(targetIndex);
		target.weights.EnsureCapacity(targetIndex);
		
		target.vertices.Count = targetIndex;
		target.normals.Count = targetIndex;
		target.UVs.Count = targetIndex;
		target.weights.Count = targetIndex;
		
		for(int i = 0; i < transferTable.Length; i++)
		{
			int j = transferTable[i];
			if(j != -1)
				target.vertices.array[j] = sourceVertices[i];
		}
		
		for(int i = 0; i < transferTable.Length; i++)
		{
			int j = transferTable[i];
			if(j != -1)
				target.normals.array[j] = sourceNormals[i];
		}
		
		for(int i = 0; i < transferTable.Length; i++)
		{
			int j = transferTable[i];
			if(j != -1)
				target.UVs.array[j] = sourceUVs[i];
		}
		
		for(int i = 0; i < transferTable.Length; i++)
		{
			int j = transferTable[i];
			if(j != -1)
				target.weights.array[j] = sourceWeights[i];
		}
	}
	
	private void LoadSkinnedMeshRendererIntoCache(SkinnedMeshRenderer smr, bool includeRoomForGrowth)
	{		
		Mesh m = smr.sharedMesh;
		
		int initialCapacity = includeRoomForGrowth ? Mathf.RoundToInt((float) m.vertexCount * factorOfSafetyGeometry) : m.vertexCount;

		c.vertices.Clear();
		c.normals.Clear();
		c.UVs.Clear();
		c.weights.Clear();

		c.vertices.EnsureCapacity(initialCapacity);
		c.normals.EnsureCapacity(initialCapacity);
		c.UVs.EnsureCapacity(initialCapacity);
		c.weights.EnsureCapacity(initialCapacity);

		c.indices = new int[m.subMeshCount][];
		
		for(int i = 0; i < m.subMeshCount; i++)
		{
			c.indices[i] = m.GetTriangles(i);
		}
		
		c.vertices.AddArray(m.vertices);	
		c.normals.AddArray(m.normals);
		c.UVs.AddArray(m.uv);
		c.weights.AddArray(m.boneWeights);
	
		c.mats = smr.sharedMaterials;
	}
}
