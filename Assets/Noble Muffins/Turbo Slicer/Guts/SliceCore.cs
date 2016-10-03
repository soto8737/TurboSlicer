using UnityEngine;
using System.Collections.Generic;
using NobleMuffins.MuffinSlicer;
using System.Text;

partial class TurboSlice
{
	private GameObject[] _splitByPlane(GameObject go, Vector4 planeInLocalSpace, bool destroyOriginal, bool callHandlers = true)
	{
		Sliceable sliceable = ensureSliceable(go);
		
		if(!sliceable.currentlySliceable)
		{
			GameObject[] result = { go };
			
			return result;
		}
		
		InfillConfiguration[] ourInfills = sliceable.infillers.Length > 0 ? sliceable.infillers : new InfillConfiguration[0];

		MeshCache c = null;	
		do
		{
			Mesh m = getMesh(sliceable);
			
			if(m == null)
			{
				break;
			}
			
			if(meshCaches.ContainsKey(m))
			{
				c = meshCaches[m];
			}
			else
				c = cacheFromGameObject(sliceable, true);
		}
		while(false);
		if(c == null)
		{
			Debug.LogWarning("Turbo Slicer cannot find mesh filter in object '" + go.name + "' in scene '" + Application.loadedLevelName + "'! Only objects featuring a mesh filter can be sliced.");
			
			GameObject[] result = { go };
			
			return result;
		}
		
		int submeshCount = c.indices.Length;
		
		//We're going to create two new tentative meshes which contain ALL original vertices in order,
		//plus room for new vertices. Not all of these copied vertices will be addressed, but copying them
		//over eliminates the need to remove doubles and do an On^2 search.
		
		TurboList<int>[] _frontIndices = new TurboList<int>[ submeshCount ];
		TurboList<int>[] _backIndices = new TurboList<int>[ submeshCount ];
		
		PlaneTriResult[] sidePlanes = new PlaneTriResult[c.vertices.Count];
		{
			Vector3[] vertices = c.vertices.array;
			
			for(int i = 0; i < sidePlanes.Length; i++)
			{
				sidePlanes[i] = MuffinSliceCommon.getSidePlane(ref vertices[i], ref planeInLocalSpace);
			}
		}
		
		for(int j = 0; j < submeshCount; j++)
		{	
			int initialCapacityIndices = Mathf.RoundToInt((float) c.indices[j].Length * factorOfSafetyIndices);
		
			_frontIndices[j] = new TurboList<int>(initialCapacityIndices);
			_backIndices[j] = new TurboList<int>(initialCapacityIndices);
			
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
				else
				{
					splitPending.AddArray(indices);
				}
			}
			
			InfillConfiguration ifc = null;
			
			if(j < c.mats.Length)
			{
				Material mat = c.mats[j];
				
				foreach(InfillConfiguration _ifc in ourInfills)
				{
					if(_ifc.material == mat)
					{
						ifc = _ifc;
					}
				}
			}			
			
			if(splitPending.Count > 0)
			{
				splitTriangles(planeInLocalSpace, splitPending.ToArray(), c, ifc, frontIndices, backIndices);
			}
		}
		
		GameObject[] results;
		
		bool onlyHaveOne = true;
		
		for(int i = 0; i < c.indices.Length; i++)
		{
			onlyHaveOne &= _frontIndices[i].Count == 0 || _backIndices[i].Count == 0;
		}

		if(onlyHaveOne)
		{	
			//Do nothing
			results = new GameObject[1];
			results[0] = go;
		}
		else
		{
			MeshCache frontCache = new MeshCache();
			frontCache.vertices = c.vertices;
			if(sliceable.channelNormals)
				frontCache.normals = c.normals;
			frontCache.coords = c.coords;
			frontCache.coords2 = c.coords2;
			frontCache.mats = c.mats;
			
			MeshCache backCache = new MeshCache();
			backCache.vertices = c.vertices;
			if(sliceable.channelNormals)
				backCache.normals = c.normals;
			backCache.coords = c.coords;
			backCache.coords2 = c.coords2;
			backCache.mats = c.mats;
			
			frontCache.indices = new int[submeshCount][];
			backCache.indices = new int[submeshCount][];
			for(int i = 0; i < submeshCount; i++)
			{
				frontCache.indices[i] = _frontIndices[i].ToArray();
				backCache.indices[i] = _backIndices[i].ToArray();
			}
			
			Vector3[] geoSubsetOne, geoSubsetTwo;
			Vector3[] normalsSubsetOne = null, normalsSubsetTwo = null;
			Vector2[] uvSubsetOne, uvSubsetTwo;
			Vector2[] uv2SubsetOne = null, uv2SubsetTwo = null;
			int[][] indexSubsetOne, indexSubsetTwo;
			
			indexSubsetOne = new int[submeshCount][];
			indexSubsetTwo = new int[submeshCount][];
			
			//Perfect subset will inflate the array list size if needed to the exact figure. So if we estimate 0,
			//and there is 1 submesh, than we will have 1 allocation, and this is optimal. Estimation can only help
			//if we have THREE or more submeshes, which is a silly scenario for anyone concerned about performance.
			int estimateOne = 0, estimateTwo = 0;
			
			TurboList<Vector3>
				_geoSubsetOne = null, _geoSubsetTwo = null,
				_normalSubsetOne = null, _normalSubsetTwo = null;
			
			TurboList<Vector2>
				_uvSubsetOne = null, _uvSubsetTwo = null;

			TurboList<Vector2>
				_uv2SubsetOne = null, _uv2SubsetTwo = null;
			
			_geoSubsetOne = new TurboList<Vector3>(estimateOne);
			_geoSubsetTwo = new TurboList<Vector3>(estimateTwo);
			
			if(sliceable.channelNormals)
			{
				_normalSubsetOne = new TurboList<Vector3>(estimateOne);
				_normalSubsetTwo = new TurboList<Vector3>(estimateTwo);
			}
			
			_uvSubsetOne = new TurboList<Vector2>(estimateOne);
			_uvSubsetTwo = new TurboList<Vector2>(estimateTwo);

			if(sliceable.channelUV2)
			{
				_uv2SubsetOne = new TurboList<Vector2>(estimateOne);
				_uv2SubsetTwo = new TurboList<Vector2>(estimateTwo);
			}

			int transferTableMaximumKey = c.vertices.Count;
			
			int[] transferTableOne = new int[transferTableMaximumKey];
			int[] transferTableTwo = new int[transferTableMaximumKey];
			
			for(int i = 0; i < transferTableOne.Length; i++) transferTableOne[i] = -1;
			for(int i = 0; i < transferTableTwo.Length; i++) transferTableTwo[i] = -1;
			
			for(int i = 0; i < submeshCount; i++)
				perfectSubset(_frontIndices[i], c.vertices, c.normals, c.coords, c.coords2, out indexSubsetOne[i], _geoSubsetOne, _normalSubsetOne, _uvSubsetOne, _uv2SubsetOne, ref transferTableOne );
			
			for(int i = 0; i < submeshCount; i++)
				perfectSubset(_backIndices[i], c.vertices, c.normals, c.coords, c.coords2, out indexSubsetTwo[i], _geoSubsetTwo, _normalSubsetTwo, _uvSubsetTwo, _uv2SubsetTwo, ref transferTableTwo );
			
			geoSubsetOne = _geoSubsetOne.ToArray();
			geoSubsetTwo = _geoSubsetTwo.ToArray();
			if(sliceable.channelNormals)
			{
				normalsSubsetOne = _normalSubsetOne.ToArray();
				normalsSubsetTwo = _normalSubsetTwo.ToArray();
			}
			uvSubsetOne = _uvSubsetOne.ToArray();
			uvSubsetTwo = _uvSubsetTwo.ToArray();

			if(sliceable.channelUV2)
			{
				uv2SubsetOne = _uv2SubsetOne.ToArray();
				uv2SubsetTwo = _uv2SubsetTwo.ToArray();
			}

			//Note that we do not explicitly call recalculate bounds because (as per the manual) this is implicit in an
			//assignment to vertices whenever the vertex count changes from zero to non-zero.
			
			Mesh frontMesh = new Mesh();
			Mesh backMesh = new Mesh();
			
			GameObject frontObject, backObject;
			
			createResultObjects(go, sliceable, false, planeInLocalSpace, out frontObject, out backObject);
			
			ensureSliceable(frontObject);
			ensureSliceable(backObject);

			setMesh(frontObject.GetComponent<Sliceable>(), frontMesh);
			setMesh(backObject.GetComponent<Sliceable>(), backMesh);

			frontMesh.vertices = geoSubsetOne;
			backMesh.vertices = geoSubsetTwo;

			if(sliceable.channelTangents)
			{
				Vector4[] tangentsOne, tangentsTwo;
				
				int[] concatenatedIndicesOne = concatenateIndexArrays(indexSubsetOne);
				int[] concatenatedIndicesTwo = concatenateIndexArrays(indexSubsetTwo);
				
				RealculateTangents(geoSubsetOne, normalsSubsetOne, uvSubsetOne, concatenatedIndicesOne, out tangentsOne);
				RealculateTangents(geoSubsetTwo, normalsSubsetTwo, uvSubsetTwo, concatenatedIndicesTwo, out tangentsTwo);

				frontMesh.tangents = tangentsOne;
				backMesh.tangents = tangentsTwo;
			}

			if(sliceable.channelNormals)
			{
				frontMesh.normals = normalsSubsetOne;
				backMesh.normals = normalsSubsetTwo;
			}
			frontMesh.uv = uvSubsetOne;
			backMesh.uv = uvSubsetTwo;

			if(sliceable.channelUV2)
			{
				frontMesh.uv2 = uv2SubsetOne;
				backMesh.uv2 = uv2SubsetTwo;
			}
			
			frontMesh.subMeshCount = submeshCount;
			backMesh.subMeshCount = submeshCount;
			
			for(int i = 0 ; i < submeshCount; i++)
			{
				var indicesOne = indexSubsetOne[i].Length > 0 ? indexSubsetOne[i] : new int[] { 0, 0, 0 };
				var indicesTwo = indexSubsetTwo[i].Length > 0 ? indexSubsetTwo[i] : new int[] { 0, 0, 0 };

				frontMesh.SetTriangles(indicesOne, i);
				backMesh.SetTriangles(indicesTwo, i);
			}
			
			TSCallbackOnDestroy frontCallback = frontObject.GetComponent<TSCallbackOnDestroy>();
			TSCallbackOnDestroy backCallback = backObject.GetComponent<TSCallbackOnDestroy>();

			if(frontCallback == null)
			{
				frontCallback = frontObject.AddComponent<TSCallbackOnDestroy>();
			}
			
			if(backCallback == null)
			{
				backCallback = backObject.AddComponent<TSCallbackOnDestroy>();
			}
				
			frontCallback.callWithMeshOnDestroy = releaseMesh;
			frontCallback.mesh = frontMesh;
				
			backCallback.callWithMeshOnDestroy = releaseMesh;
			backCallback.mesh = backMesh;

			foreach(var mesh in new [] { frontMesh, backMesh }) mesh.Optimize();
			
			meshCaches[frontMesh] = frontCache;
			meshCaches[backMesh] = backCache;
			
			results = new GameObject[2];
			results[0] = frontObject;
			results[1] = backObject;
			
			if(sliceable != null && sliceable.refreshColliders)
			{
				foreach(GameObject r in results)
				{
					Collider collider = r.GetComponent<Collider>();
										
					if(collider != null)
					{
						bool isTrigger = collider.isTrigger;
						
						if(collider is BoxCollider)
						{
							GameObject.DestroyImmediate(collider);
							collider = r.AddComponent<BoxCollider>();
						}
						else if(collider is SphereCollider)
						{
							GameObject.DestroyImmediate(collider);
							collider = r.AddComponent<SphereCollider>();
						}
						else if(collider is MeshCollider)
						{
							MeshCollider mc = (MeshCollider) collider;
							
							bool isFront = r == frontObject;
							
							Mesh mesh = isFront ? frontMesh : backMesh;
							
							mc.sharedMesh = mesh;
						}
						
						collider.isTrigger = isTrigger;
					}	
				}
			}
			
			if(callHandlers && sliceable != null)
				sliceable.handleSlice(results);

			if(destroyOriginal)
				GameObject.Destroy(go);
		}
		
		return results;
	}
	
	
	/* I'm going to reorganize splitTriangle - which is called per triangle and operates per triangle - into
	 * splitTriangles. splitTriangles operates on a batch and is organized on a per action basis.
	 * 
	 * The first observation about splitTriangle is that classifyPoint is called upon pretty much every single
	 * one going in. Second, some of this work is repeated when indices repeat vertex references...
	 *
	 * Let's assume that it's probably more costly to hunt down repeated vertices than to repeat the work a little.
	 * 
	 */

	struct SplitAction
	{
		public const short nullIndex = -1;
		
		public const byte TO_FRONT = 0x01, TO_BACK = 0x02, INTERSECT = 0x04;
		
		public byte flags;
		public int cloneOf, index0, index1, realIndex;
		public float intersectionResult;
		
		public SplitAction(bool _toFront, bool _toBack, int _index0)
		{
			flags = 0;
			if(_toFront) flags = (byte) (flags | TO_FRONT);
			if(_toBack) flags = (byte) (flags | TO_BACK);
			index0 = _index0;
			index1 = nullIndex;
			cloneOf = nullIndex;
			realIndex = index0;
			intersectionResult = 0f;
		}
		
		public SplitAction(int _index0, int _index1)
		{
			flags = TO_FRONT | TO_BACK | INTERSECT;
			index0 = _index0;
			index1 = _index1;
			cloneOf = nullIndex;
			realIndex = nullIndex;
			intersectionResult = 0f;
		}
	};
	
	static void splitTriangles(Vector4 plane, int[] sourceIndices,
		MeshCache meshCache, InfillConfiguration infill, TurboList<int> frontIndices, TurboList<int> backIndices)
	{
		bool doInfill = infill != null;
		
		bool doNormals = meshCache.normals != null;
		bool doUV2 = meshCache.coords2 != null;
		
		Vector3[] sourceGeometry = meshCache.vertices.array;
		Vector3[] sourceNormals = null;
		if(doNormals)
			sourceNormals = meshCache.normals.array;
		Vector2[] sourceCoords = meshCache.coords.array;
		Vector2[] sourceCoords2 = null;
		if(doUV2)
			sourceCoords2 = meshCache.coords2.array;
		
		float[] pointClassifications = new float[sourceIndices.Length];
		for(int i = 0; i < pointClassifications.Length; i++)
		{
			pointClassifications[i] = MuffinSliceCommon.classifyPoint(ref plane, ref sourceGeometry[ sourceIndices[i] ]);
		}
		
		//Now we're going to do the decision making pass. This is where we assess the side figures and produce actions...
		
		int inputTriangleCount = sourceIndices.Length / 3;
		
		//A good action count estimate can avoid reallocations.
		//We expect exactly five actions per triangle.
		int actionEstimate = inputTriangleCount * 5;
		List<SplitAction> splitActions = new List<SplitAction>(actionEstimate);
		
		//We want to count how many vertices are yielded from each triangle split. This will be used later to add the indices.
		short[] frontVertexCount = new short[inputTriangleCount];
		short[] backVertexCount = new short[inputTriangleCount];
		
		short totalFront = 0, totalBack = 0;
	
		for(int i = 0; i < sourceIndices.Length; i += 3)
		{
			int[] indices = { sourceIndices[i], sourceIndices[i+1], sourceIndices[i+2] };
			
			float[] sides = { pointClassifications[i], pointClassifications[i+1], pointClassifications[i+2] };
			
			short indexA = 2;
			
			short front = 0, back = 0;
			
			for(short indexB = 0; indexB < 3; indexB++)
			{
				float sideA = sides[indexA];
				float sideB = sides[indexB];
				
				if(sideB > 0f)
				{
					if(sideA < 0f)
					{
						//Find intersection between A, B. Add to BOTH
						splitActions.Add( new SplitAction(indices[indexA], indices[indexB]) );
						front++;
						back++;
					}
					//Add B to FRONT.
					splitActions.Add( new SplitAction(true, false, indices[indexB]));
					front++;
				}
				else if (sideB < 0f)
				{
					if (sideA > 0f)
					{
						//Find intersection between A, B. Add to BOTH
						splitActions.Add( new SplitAction(indices[indexA], indices[indexB]));
						front++;
						back++;
					}
					//Add B to BACK.
					splitActions.Add( new SplitAction(false, true, indices[indexB]));
					back++;
				}
				else
				{
					//Add B to BOTH.
					splitActions.Add( new SplitAction(false, true,  indices[indexB]));
					front++;
					back++;
				}
				
				indexA = indexB;
			}
			
			int j = i / 3; //This is the triangle counter.
			
			frontVertexCount[j] = front;
			backVertexCount[j] = back;
			
			totalFront += front;
			totalBack += back;
		}
		
		// We're going to iterate through the splits only several times, so let's
		//find the subset once now.
		// Since these are STRUCTs, this is going to COPY the array content. The
		//intersectionInverseRelation table made below helps us put it back into the
		//main array before we use it.
		SplitAction[] intersectionActions;
		int[] intersectionInverseRelation;
		{
			int intersectionCount = 0;
			
			foreach(SplitAction sa in splitActions)
				if((sa.flags & SplitAction.INTERSECT) == SplitAction.INTERSECT)
					intersectionCount++;
			
			intersectionActions = new SplitAction[intersectionCount];
			intersectionInverseRelation = new int[intersectionCount];
			
			int j = 0;
			for(int i = 0; i < splitActions.Count; i++)
			{
				SplitAction sa = splitActions[i];
				if((sa.flags & SplitAction.INTERSECT) == SplitAction.INTERSECT)
				{
					intersectionActions[j] = sa;
					intersectionInverseRelation[j] = i;
					j++;
				}
			}
		}
		
		// Next, we're going to find out which splitActions replicate the work of other split actions.
		//A given SA replicates another if and only if it _both_ calls for an intersection _and_ has
		//the same two parent indices (index0 and index1). This is because all intersections are called
		//with the same other parameters, so any case with an index0 and index1 matching will yield the
		//same results.
		// Only caveat is that two given splitActions might have the source indices in reverse order, so
		//we'll arbitrarily decide that "greater first" or something is the correct order. Flipping this
		//order has no consequence until after the intersection is found (at which point flipping the order
		//necessitates converting intersection i to 1-i to flip it as well.)
		// We can assume that every SA has at most 1 correlation. For a given SA, we'll search the list
		//UP TO its own index and, if we find one, we'll take the other's index and put it into the CLONE OF
		//slot.
		// So if we had a set like AFDBAK, than when the _latter_ A comes up for assessment, it'll find
		//the _first_ A (with an index of 0) and set the latter A's cloneOf figure to 0. This way we know
		//any latter As are a clone of the first A.
		
		for(int i = 0; i < intersectionActions.Length; i++)
		{
			SplitAction a = intersectionActions[i];
			
			//Ensure that the index0, index1 figures are all in the same order.
			//(We'll do this as we walk the list.)
			if(a.index0 > a.index1)
			{
				int j = a.index0;
				a.index0 = a.index1;
				a.index1 = j;
			}
			
			//Only latters clone formers, so we don't need to search up to and past the self.
			for(int j = 0; j < i; j++)
			{
				SplitAction b = intersectionActions[j];
				
				
				bool match = a.index0 == b.index0 && a.index1 == b.index1;
				
				if(match)
				{
					a.cloneOf = j;
				}
			}
			
			intersectionActions[i] = a;
		}
		
		//Next, we want to perform all INTERSECTIONS. Any action which has an intersection needs to have that, like, done.
		
		for(int i = 0; i < intersectionActions.Length; i++)
		{
			SplitAction sa = intersectionActions[i];
			
			if(sa.cloneOf == SplitAction.nullIndex)
			{
				Vector3 pointA = sourceGeometry[ sa.index0 ];
				Vector3 pointB = sourceGeometry[ sa.index1 ];
				sa.intersectionResult = MuffinSliceCommon.intersectCommon(ref pointB, ref pointA, ref plane);
				intersectionActions[i] = sa;
			}
			
		}
		
		int newIndexStartsAt = meshCache.vertices.Count;
		
		// Let's create a table that relates an INTERSECTION index to a GEOMETRY index with an offset of 0 (for example
		//to refer to our newVertices or to the transformedVertices or whatever; internal use.)
		// We can also set up our realIndex figures in the same go.
		int uniqueVertexCount = 0;
		int[] localIndexByIntersection = new int[intersectionActions.Length];
		{				
			int currentLocalIndex = 0;
			for(int i = 0; i < intersectionActions.Length; i++)
			{
				SplitAction sa = intersectionActions[i];
				
				int j;
				
				if(sa.cloneOf == SplitAction.nullIndex)
				{
					j = currentLocalIndex++;
				}
				else
				{
					//This assumes that the widget that we are a clone of already has its localIndexByIntersection assigned.
					//We assume this because above – where we seek for clones – we only look behind for cloned elements.
					j = localIndexByIntersection[sa.cloneOf];
				}
				
				sa.realIndex = newIndexStartsAt + j;
				
				localIndexByIntersection[i] = j;
				
				intersectionActions[i] = sa;
			}
			uniqueVertexCount = currentLocalIndex;
		}
				
		//Let's figure out how much geometry we might have.
		//The infill geometry is a pair of clones of this geometry, but with different NORMALS and UVs. (Each set has different normals.)
		
		int newGeometryEstimate = uniqueVertexCount * (doInfill ? 3 : 1);
		
		//In this ACTION pass we'll act upon intersections by fetching both referred vertices and LERPing as appropriate.
		//The resultant indices will be written out over the index0 figures.
		
		Vector3[] newVertices = new Vector3[newGeometryEstimate];
		Vector3[] newNormals = null;
		if(doNormals)
			newNormals = new Vector3[newGeometryEstimate];
		Vector2[] newUVs = new Vector2[newGeometryEstimate];
		Vector2[] newUV2s = new Vector2[newGeometryEstimate];
			
		//LERP to create vertices
		{
			int currentNewIndex = 0;
			foreach(SplitAction sa in intersectionActions)
			{
				if(sa.cloneOf == SplitAction.nullIndex)
				{
					Vector3 v = sourceGeometry[sa.index0];
					Vector3 v2 = sourceGeometry[sa.index1];
					newVertices[currentNewIndex] = Vector3.Lerp(v2, v, sa.intersectionResult);
					currentNewIndex++;
				}
			}
		}
		
		//Normals:
		if(doNormals)
		{
			int currentNewIndex = 0;
			foreach(SplitAction sa in intersectionActions)
			{	
				if(sa.cloneOf == SplitAction.nullIndex)
				{
					Vector3 n = sourceNormals[sa.index0];
					Vector3 n2 = sourceNormals[sa.index1];
					newNormals[currentNewIndex] = Vector3.Lerp(n2, n, sa.intersectionResult);
					currentNewIndex++;
				}
			}
		}
		
		//UVs:
		{
			int currentNewIndex = 0;
			foreach(SplitAction sa in intersectionActions)
			{
				if(sa.cloneOf == SplitAction.nullIndex)
				{
					Vector2 uv = sourceCoords[sa.index0];
					Vector2 uv2 = sourceCoords[sa.index1];
					newUVs[currentNewIndex] = Vector2.Lerp(uv2, uv, sa.intersectionResult);
					currentNewIndex++;
				}
			}
		}

		//UV2s:
		if(doUV2)
		{
			int currentNewIndex = 0;
			foreach(SplitAction sa in intersectionActions)
			{
				if(sa.cloneOf == SplitAction.nullIndex)
				{
					Vector2 uv = sourceCoords2[sa.index0];
					Vector2 uv2 = sourceCoords2[sa.index1];
					newUV2s[currentNewIndex] = Vector2.Lerp(uv2, uv, sa.intersectionResult);
					currentNewIndex++;
				}
			}
		}
		
		//All the polygon triangulation algorithms depend on having a 2D polygon. We also need the slice plane's
		//geometry in two-space to map the UVs.
		
		//NOTE that as we only need this data to analyze polygon geometry for triangulation, we can TRANSFORM (scale, translate, rotate)
		//these figures any way we like, as long as they retain the same relative geometry. So we're going to perform ops on this
		//data to create the UVs by scaling it around, and we'll feed the same data to the triangulator.
		
		//Our's exists in three-space, but is essentially flat... So we can transform it onto a flat coordinate system.
		//The first three figures of our plane four-vector describe the normal to the plane, so if we can create
		//a transformation matrix from that normal to the up normal, we can transform the vertices for observation.
		//We don't need to transform them back; we simply refer to the original vertex coordinates by their index,
		//which (as this is an ordered set) will match the indices of coorisponding transformed vertices.
		
		//This vector-vector transformation comes from Benjamin Zhu at SGI, pulled from a 1992
		//forum posting here: http://steve.hollasch.net/cgindex/math/rotvecs.html
		
		/*	"A somewhat "nasty" way to solve this problem:

			Let V1 = [ x1, y1, z1 ], V2 = [ x2, y2, z2 ]. Assume V1 and V2 are already normalized.
			
			    V3 = normalize(cross(V1, V2)). (the normalization here is mandatory.)
			    V4 = cross(V3, V1).
			             
			         [ V1 ]
			    M1 = [ V4 ]
			         [ V3 ]
			
			    cos = dot(V2, V1), sin = dot(V2, V4)
			            
			         [ cos   sin    0 ]
			    M2 = [ -sin  cos    0 ]
			         [ 0     0      1 ]
			         
			The sought transformation matrix is just M1^-1 * M2 * M1. This might well be a standard-text solution."
			
			-Ben Zhu, SGI, 1992
		 */
		
		Vector2[] transformedVertices = new Vector2[0];
		int infillFrontOffset = 0, infillBackOffset = 0;
		
		if(doInfill)
		{
			transformedVertices = new Vector2[newGeometryEstimate];

			
			// Based on the algorithm described above, this will create a matrix permitting us
			//to multiply a given vertex yielding a vertex transformed to an XY plane (where Z is
			//undefined.)
			// This algorithm cannot work if we're already in that plane. We know if we're already
			//in that plane if X and Y are both zero and Z is nonzero.
			bool canUseSimplifiedTransform = Mathf.Approximately(plane.x, 0f) && Mathf.Approximately(plane.y, 0f);
			if(!canUseSimplifiedTransform)
			{
				Matrix4x4 flattenTransform;

				Vector3 v1 = Vector3.forward;
				Vector3 v2 = new Vector3( plane.x, plane.y, plane.z ).normalized;
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
				
				for(int i = 0; i < newVertices.Length; i++)
				{
					transformedVertices[i] = (Vector2) flattenTransform.MultiplyPoint3x4( newVertices[i] );
				}
			}
			else
			{
				for(int i = 0; i < newVertices.Length; i++)
				{
					transformedVertices[i] = new Vector2(newVertices[i].x, -newVertices[i].y);
				}
			}
			
			for(int i = 1; i < localIndexByIntersection.Length; i++)
			{
				int localIndexForI = localIndexByIntersection[i];
				Vector2 vertexForI = transformedVertices[ localIndexForI ];
								
				for(int j = 0; j < i; j++)
				{
					int localIndexForJ = localIndexByIntersection[j];
					Vector2 vertexForJ = transformedVertices[ localIndexForJ ];
					
					const float threshold = 0.000001f;
					
					Vector2 delta = vertexForJ - vertexForI;
					
					bool theyreTheSame = Mathf.Abs(delta.x) < threshold && Mathf.Abs(delta.y) < threshold;
					
					
					if(theyreTheSame)
					{
						localIndexByIntersection[i] = localIndexForJ;
					}
				}
			}
			
			// We want to normalize the entire transformed vertices. To do this, we find the largest
			//floats in either (by abs). Then we scale. Of course, this normalizes us to figures
			//in the range of [-1f,1f] (not necessarily extending all the way on both sides), and
			//what we need are figures between 0f and 1f (not necessarily filling, but necessarily
			//not spilling.) So we'll shift it here.
			{
				float x = 0f, y = 0f;
				
				for(int i = 0; i < transformedVertices.Length; i++)
				{
					Vector2 v = transformedVertices[i];
					
					v.x = Mathf.Abs(v.x);
					v.y = Mathf.Abs(v.y);
					
					if(v.x > x) x = v.x;
					if(v.y > y) y = v.y;
				}
				
				//We would use 1f/x, 1f/y but we also want to scale everything to half (and perform an offset) as
				//described above.
				x = 0.5f / x;
				y = 0.5f / y;
				
				Rect r = infill.regionForInfill;
				
				for(int i = 0; i < transformedVertices.Length; i++)
				{
					Vector2 v = transformedVertices[i];
					v.x *= x;
					v.y *= y;
					v.x += 0.5f;
					v.y += 0.5f;
					v.x *= r.width;
					v.y *= r.height;
					v.x += r.x;
					v.y += r.y;
					transformedVertices[i] = v;
				}
			}
			
			//Now let's build the geometry for the two slice in-fills.
			//One is for the front side, and the other for the back side. Each has differing normals.
			
			infillFrontOffset = uniqueVertexCount;
			infillBackOffset = uniqueVertexCount * 2;
			
			//The geometry is identical...
			
			System.Array.Copy(newVertices, 0, newVertices, infillFrontOffset, uniqueVertexCount);
			System.Array.Copy(newVertices, 0, newVertices, infillBackOffset, uniqueVertexCount);
			
			System.Array.Copy(transformedVertices, 0, newUVs, infillFrontOffset, uniqueVertexCount);
			System.Array.Copy(transformedVertices, 0, newUVs, infillBackOffset, uniqueVertexCount);

			if(doUV2)
			{
				for(int i = 0; i < uniqueVertexCount; i++)
				{
					newUV2s[i + infillFrontOffset] = Vector2.zero;
				}
				for(int i = 0; i < uniqueVertexCount; i++)
				{
					newUV2s[i + infillBackOffset] = Vector2.zero;
				}
			}
			
			if(doNormals)
			{
				Vector3 infillFrontNormal = ((Vector3) plane) * -1f;
				infillFrontNormal.Normalize();
				
				for(int i = infillFrontOffset; i < infillBackOffset; i++)
					newNormals[i] = infillFrontNormal;
				
				Vector3 infillBackNormal = (Vector3) plane;
				infillBackNormal.Normalize();
				
				for(int i = infillBackOffset; i < newNormals.Length; i++)
					newNormals[i] = infillBackNormal;
			}
		}
		
		//Get the exact indices into two tables. Note that these are indices for TRIANGLES and QUADS, which we'll triangulate in the next section.
		int[] newFrontIndex = new int[totalFront];
		int[] newBackIndex = new int[totalBack];
		
		//Note that here we refer to split actions again, so let's copy back the updated splitActions.
		for(int i = 0; i < intersectionActions.Length; i++)
		{
			int j = intersectionInverseRelation[i];
			splitActions[j] = intersectionActions[i];
		}
		
		int newFrontIndexCount = 0, newBackIndexCount = 0;
		foreach(SplitAction sa in splitActions)
		{
			if((sa.flags & SplitAction.TO_FRONT) == SplitAction.TO_FRONT)
			{
				newFrontIndex[newFrontIndexCount] = sa.realIndex;
				newFrontIndexCount++;
			}
			if((sa.flags & SplitAction.TO_BACK) == SplitAction.TO_BACK)
			{
				newBackIndex[newBackIndexCount] = sa.realIndex;
				newBackIndexCount++;
			}
		}
		
		//Now we need to triangulate sets of quads.
		//We recorded earlier whether we're looking at triangles or quads – in order. So we have a pattern like TTQTTQQTTTQ, and
		//we can expect these vertices to match up perfectly to what the above section of code dumped out.
		
		int startIndex = 0;
		
		int[] _indices3 = new int[3];
		int[] _indices4 = new int[6];
		
		foreach(short s in frontVertexCount)
		{
			if(s == 3)
			{
				_indices3[0] = newFrontIndex[startIndex];
				_indices3[1] = newFrontIndex[startIndex + 1];
				_indices3[2] = newFrontIndex[startIndex + 2];
				frontIndices.AddArray(_indices3);
			}
			else if(s == 4)
			{
				_indices4[0] = newFrontIndex[startIndex];
				_indices4[1] = newFrontIndex[startIndex + 1];
				_indices4[2] = newFrontIndex[startIndex + 3];
				_indices4[3] = newFrontIndex[startIndex + 1];
				_indices4[4] = newFrontIndex[startIndex + 2];
				_indices4[5] = newFrontIndex[startIndex + 3];
				frontIndices.AddArray(_indices4);
			}
			startIndex += s;
		}
		
		startIndex = 0;
		
		foreach(short s in backVertexCount)
		{
			if(s == 3)
			{
				_indices3[0] = newBackIndex[startIndex];
				_indices3[1] = newBackIndex[startIndex + 1];
				_indices3[2] = newBackIndex[startIndex + 2];
				backIndices.AddArray(_indices3);
			}
			else if(s == 4)
			{
				_indices4[0] = newBackIndex[startIndex];
				_indices4[1] = newBackIndex[startIndex + 1];
				_indices4[2] = newBackIndex[startIndex + 3];
				_indices4[3] = newBackIndex[startIndex + 1];
				_indices4[4] = newBackIndex[startIndex + 2];
				_indices4[5] = newBackIndex[startIndex + 3];
				backIndices.AddArray(_indices4);
			}
			startIndex += s;
		}
		
		//Let's add this shiznit in!
		
		meshCache.vertices.AddArray(newVertices);
		if(doNormals)
			meshCache.normals.AddArray(newNormals);
		meshCache.coords.AddArray(newUVs);
		if(doUV2)
			meshCache.coords2.AddArray(newUV2s);

		//Now we need to fill in the slice hole.
		
		//We need to find the POLYGON[s] representing the slice hole[s]. There may be more than one. 
		//Then we need to TRIANGULATE these polygons and write them out.
		
		//Above we've built the data necessary to pull this off. We have:
		
		// - Geometry for the polygon around the edges in Vertex3 / Normal / UV format, already added
		//to the geometry setup.
		// - Geometry for the polygon in Vertex2 format in matching order, aligned to the slice plane.
		// - A collection of all data points and 1:1 hashes representing their physical location.
		
		//In this mess of data here may be 0 or non-zero CLOSED POLYGONS. We need to walk the list and
		//identify each CLOSED POLYGON (there may be none, or multiples). Then, each of these must be
		//triangulated separately.
		
		//Vertices connected to each other in a closed polygon can be found to associate with each other
		//in two ways. Envision a triangle strip that forms a circular ribbon – and that we slice through
		//the middle of this ribbon. Slice vertices come in two kinds of pairs; there are pairs that COME FROM
		//the SAME triangle, and pairs that come from ADJACENT TRIANGLES. The whole chain is formed from
		//alternating pair-types.
		
		//So for example vertex A comes from the same triangle as vertex B, which in turn matches the position
		//of the NEXT triangle's vertex A.
		
		//The data is prepared for us to be able to identify both kinds of associations. First,
		//association by parent triangle is encoded in the ORDERING. Every PAIR from index 0 shares a parent
		//triangle; so indices 0-1, 2-3, 4-5 and so on are each a pair from a common parent triangle.
		
		//Meanwhile, vertices generated from the common edge of two different triangles will have the SAME
		//POSITION in three-space.
		
		//We don't have to compare Vector3s, however; this has already been done. Uniques were eliminated above.
		//What we have is a table; localIndexByIntersection. This list describes ALL SLICE VERTICES in terms
		//of which VERTEX (in the array – identified by index) represents that slice vertex. So if we see that
		//localIndexByIntersection[0] == localIndexByIntersection[4], than we know that slice vertices 0 and 4
		//share the same position in three space.
		
		//With that in mind, we're going to go through the list in circles building chains out of these
		//connections.
		
		if(doInfill)
		{
			List<int> currentWorkingPoly = new List<int>();
			List<int> currentTargetPoly = new List<int>();
			List<List<int>> allPolys = new List<List<int>>();
			List<int> claimed = new List<int>();
			
			int lastAdded = -1;
			
			//ASSUMPTION: Every element will be claimed into some kind of chain by the end whether correlated or not.
			do
			{
				for(int i = 0; i < localIndexByIntersection.Length; i++)
				{					
					bool go = false, fail = false, startNewChain = false;
					
					//If we didn't just add one, we're looking to start a chain. That means we have to find one that
					//isn't already claimed.
					if(lastAdded < 0)
					{
						go = claimed.Contains(i) == false;
					}
					else if(lastAdded == i)
					{
						//We've gone through twice without finding a match. This means there isn't one, or something.
						
						fail = true;
					}
					else
					{
						//Otherwise, we're trying to find the next-in-chain.
						//A valid next-in-chain is connected by geometry which, as discussed, means it's connected
						//by having matching parent indices (index0, index1).
						
						bool match = localIndexByIntersection[i] == localIndexByIntersection[lastAdded];
						
						//But there's a special case about the match; it's possible that we've closed the loop!
						//How do we know we've closed the loop? There are multiple ways but the simplest is that
						//the chain already contains the element in question.
						
						bool loopComplete = match && currentWorkingPoly.Contains(i);
						
						if(loopComplete)
						{
							allPolys.Add(currentTargetPoly);
							startNewChain = true;
						}
						else
						{
							go = match;
						}
					}
					
					if(go)
					{
						int partnerByParent = i % 2 == 1 ? i - 1 : i + 1;
						
						int[] pair = { i, partnerByParent };
						
						currentWorkingPoly.AddRange(pair);
						claimed.AddRange(pair);
						
						currentTargetPoly.Add(partnerByParent);
						
						lastAdded = partnerByParent;
						
						//Skip ahead and resume the search _from_ here, so that we don't step into it
						//again from within this loop walk.
						i = partnerByParent;
					}
					else if(fail)
					{
						//We want to start a fresh poly without adding this to the valid polys.
						startNewChain = true;
					}
					
					if(startNewChain)
					{
						currentWorkingPoly.Clear();
						currentTargetPoly = new List<int>();
						lastAdded = -1;
					}
				}
			}
			while(currentWorkingPoly.Count > 0);
			
			//Now we go through each poly and triangulate it.
						
			foreach(List<int> intersectionIndicesForThisPolygon in allPolys)
			{
				Vector2[] geometryForThisPolygon = new Vector2[intersectionIndicesForThisPolygon.Count];
				
				for(int i = 0; i < geometryForThisPolygon.Length; i++)
				{
					int j = localIndexByIntersection[ intersectionIndicesForThisPolygon[i] ];
					geometryForThisPolygon[i] = transformedVertices[j];
				}
				
				List<Vector2> deduplicatedGeometry = new List<Vector2>( geometryForThisPolygon.Length );
				List<int> deduplicatedIndices = new List<int>( intersectionIndicesForThisPolygon.Count );
				
				for(int i = 0; i < intersectionIndicesForThisPolygon.Count; i++)
				{
					bool match = false;
					
					int intersectionIndex = intersectionIndicesForThisPolygon[i];
					int localIndex = localIndexByIntersection[ intersectionIndex ];
					
					for(int j = 0; j < deduplicatedIndices.Count && !match; j++)
					{
						int priorIntersectionIndex = deduplicatedIndices[ j ];
						int priorLocalIndex = localIndexByIntersection[ priorIntersectionIndex ];
					
						match = localIndex == priorLocalIndex;
					}
					
					if(!match)
					{
						deduplicatedGeometry.Add( geometryForThisPolygon[i] );
						deduplicatedIndices.Add( intersectionIndicesForThisPolygon[i] );
					}
				}
				
				int[] result;
								
				if(Triangulation.triangulate(deduplicatedGeometry.ToArray(), out result))
				{
					int[] front = new int[result.Length];
					int[] back = new int[result.Length];
					
					for(int i = 0; i < result.Length; i++)
					{
						int p = deduplicatedIndices[ result[i] ];
						int local = localIndexByIntersection[ p ];
						front[i] = local + infillFrontOffset + newIndexStartsAt;
						back[i] = local + infillBackOffset + newIndexStartsAt;
					}
					
					for(int i = 0; i < result.Length; i += 3)
					{
						int j = front[i];
						front[i] = front[i + 2];
						front[i + 2] = j;
					}
					
					frontIndices.AddArray(front);
					backIndices.AddArray(back);
					
					if(infillGeometryReceivers != null)
					{
						int[] infillIndices = new int[result.Length];
						
						for(int i = 0; i < result.Length; i++)
						{
							int p = deduplicatedIndices[ result[i] ];
							int local = localIndexByIntersection[ p ];
							infillIndices[i] = local;
						}
						
						infillGeometryReceivers(newVertices, newUVs, newNormals, infillIndices);
					}
				}
			}
		}
	}
	
	private void perfectSubset(
		TurboList<int> _sourceIndices,
		TurboList<Vector3> _sourceVertices,
		TurboList<Vector3> _sourceNormals,
		TurboList<Vector2> _sourceUVs,
		TurboList<Vector2> _sourceUV2s,
		out int[] targetIndices,
		TurboList<Vector3> targetVertices,
		TurboList<Vector3> targetNormals,
		TurboList<Vector2> targetUVs,
		TurboList<Vector2> targetUV2s,
		ref int[] transferTable)
	{
		int[] sourceIndices = _sourceIndices.array;
		Vector3[] sourceVertices = _sourceVertices.array;
		Vector2[] sourceUVs = _sourceUVs.array;


		Vector2[] sourceUV2s = _sourceUV2s == null ? null : _sourceUV2s.array;
		
		Vector3[] sourceNormals = null;
		if(_sourceNormals != null)
			sourceNormals = _sourceNormals.array;
		
		targetIndices = new int[_sourceIndices.Count];
		
		int targetIndex = targetVertices.Count;
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
		
		targetVertices.EnsureCapacity(targetIndex);
		if(targetNormals != null) targetNormals.EnsureCapacity(targetIndex);
		targetUVs.EnsureCapacity(targetIndex);
		if(targetUV2s != null) targetUV2s.EnsureCapacity(targetIndex);

		targetVertices.Count = targetIndex;
		if(targetNormals != null)
			targetNormals.Count = targetIndex;
		targetUVs.Count = targetIndex;
		
		for(int i = 0; i < transferTable.Length; i++)
		{
			int j = transferTable[i];
			if(j != -1)
				targetVertices.array[j] = sourceVertices[i];
		}
		
		if(targetNormals != null)
		{
			for(int i = 0; i < transferTable.Length; i++)
			{
				int j = transferTable[i];
				if(j != -1)
					targetNormals.array[j] = sourceNormals[i];
			}
		}

		for(int i = 0; i < transferTable.Length; i++)
		{
			int j = transferTable[i];
			if(j != -1)
				targetUVs.array[j] = sourceUVs[i];
		}
		
		if(targetUV2s != null)
		{
			for(int i = 0; i < transferTable.Length; i++)
			{
				int j = transferTable[i];
				if(j != -1)
					targetUV2s.array[j] = sourceUV2s[i];
			}
		}
	}

}
