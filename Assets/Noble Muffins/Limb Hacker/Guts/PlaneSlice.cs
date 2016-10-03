using UnityEngine;
using System.Collections.Generic;
using NobleMuffins.MuffinSlicer;

public partial class LimbHacker
{

	struct SplitAction
	{
		public const short nullIndex = -1;
		
		public const byte TO_FRONT = 0x01, TO_BACK = 0x02, INTERSECT = 0x04;
		
		public byte flags;			//1
		public int cloneOf;
		public int index0, index1, realIndex, parentTriangleIndex;	//4 * 4 = 16 bytes
		public float intersectionResult;							//4 bytes
		
		public SplitAction(bool _toFront, bool _toBack, int _index0)
		{
			flags = 0;
			if(_toFront) flags = (byte) (flags | TO_FRONT);
			if(_toBack) flags = (byte) (flags | TO_BACK);
			parentTriangleIndex = -1;
			index0 = _index0;
			index1 = nullIndex;
			cloneOf = nullIndex;
			realIndex = index0;
			intersectionResult = 0f;
		}
		
		public SplitAction(int _index0, int _index1, int _parentTriangleIndex)
		{
			flags = TO_FRONT | TO_BACK | INTERSECT;
			parentTriangleIndex = _parentTriangleIndex;
			index0 = _index0;
			index1 = _index1;
			cloneOf = nullIndex;
			realIndex = nullIndex;
			intersectionResult = 0f;
		}
		
		public new string ToString()
		{
			return string.Format("SplitAction: Parent={0}, Geometry={1}", parentTriangleIndex, realIndex);
		}
	};
	
	static void splitTrianglesLH(Vector4 plane, Vector3[] snapshot, PlaneTriResult[] sidePlanes, int[] sourceIndices, MeshCache meshCache,
		TurboList<int> frontIndices, TurboList<int> backIndices,
		Infill? infillMode, TurboList<int> frontInfill, TurboList<int> backInfill)
	{
		bool doInfill = infillMode.HasValue && frontInfill != null && backInfill != null;
		
		Vector3[] sourceGeometry = meshCache.vertices.array;
		Vector3[] sourceNormals = meshCache.normals.array;
		Vector2[] sourceUVs = meshCache.UVs.array;
		BoneWeight[] sourceWeights = meshCache.weights.array;
		
		float[] pointClassifications = new float[sourceIndices.Length];
		for(int i = 0; i < pointClassifications.Length; i++)
		{
			pointClassifications[i] = MuffinSliceCommon.classifyPoint(ref plane, ref snapshot[ sourceIndices[i] ]);
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
						splitActions.Add( new SplitAction(indices[indexA], indices[indexB], i) );
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
						splitActions.Add( new SplitAction(indices[indexA], indices[indexB], i));
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
		// Only caveat is that two given splitActions might as the source indices in reverse order, so
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
			
			Vector3 aVector = sourceGeometry[a.index0] + sourceGeometry[a.index1];
			
			//Only latters clone formers, so we don't need to search up to and past the self.
			for(int j = 0; j < i; j++)
			{
				SplitAction b = intersectionActions[j];
				
				bool match = a.index0 == b.index0 && a.index1 == b.index1;
				
				if(!match)
				{
					Vector3 bVector = sourceGeometry[b.index0] + sourceGeometry[b.index1];
					
//					match = Mathf.Approximately(aVector.x, bVector.x);
//					match &= Mathf.Approximately(aVector.y, bVector.y);
//					match &= Mathf.Approximately(aVector.z, bVector.z);

					// What are the chances, really?

					match = Mathf.Approximately(aVector.x + aVector.y + aVector.z, bVector.x + bVector.y + bVector.z);
				}
				
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
				/*float ir = vertexSums[ sa.index0 ] + vertexSums[ sa.index1 ];
				
				ir += 1f;
				ir *= 0.5f;
				ir = 1f - ir;
				
				sa.intersectionResult = ir;*/
				
				Vector3 pointA = snapshot[ sa.index0 ];
				Vector3 pointB = snapshot[ sa.index1 ];
				sa.intersectionResult = MuffinSliceCommon.intersectCommon(ref pointB, ref pointA, ref plane);
				intersectionActions[i] = sa;
			}
		}
		
		// Let's create a table that relates an INTERSECTION index to a GEOMETRY index with an offset of 0 (for example
		//to refer to our newVertices or to the transformedVertices or whatever; internal use.)
		// We can also set up our realIndex figures in the same go.
		int newIndexStartsAt = meshCache.vertices.Count;
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
		Vector3[] newNormals = new Vector3[newGeometryEstimate];
		Vector2[] newUVs = new Vector2[newGeometryEstimate];
		BoneWeight[] newWeights = new BoneWeight[newGeometryEstimate];
			
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
					Vector2 uv = sourceUVs[sa.index0];
					Vector2 uv2 = sourceUVs[sa.index1];
					newUVs[currentNewIndex] = Vector2.Lerp(uv2, uv, sa.intersectionResult);
					currentNewIndex++;
				}
			}
		}
		
		//Bone Weights:
		{
			int currentNewIndex = 0;
			foreach(SplitAction sa in intersectionActions)
			{
				if(sa.cloneOf == SplitAction.nullIndex)
				{
					BoneWeight bw;
					
					if(sidePlanes[sa.index0] == PlaneTriResult.PTR_FRONT)
					{
						bw = sourceWeights[sa.index0];
					}
					else
					{
						bw = sourceWeights[sa.index1];
					}
					
					newWeights[currentNewIndex] = bw;
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
			transformedVertices = new Vector2[newGeometryEstimate / 3];
			
			Matrix4x4 flattenTransform;
			
			//Based on the algorithm described above, this will create a matrix permitting us
			//to multiply a given vertex yielding a vertex transformed to an XY plane (where Z is
			//undefined.)
			{
				Vector3 v1 = Vector3.forward;
				Vector3 v2 = new Vector3( plane.x, plane.y, plane.z ).normalized;
				
				float difference = (v1 - v2).magnitude;
				
				if(difference > 0.01f)
				{
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
				}
				else
				{
					flattenTransform = Matrix4x4.identity;
				}
			}
			for(int i = 0; i < transformedVertices.Length; i++)
			{
				transformedVertices[i] = (Vector2) flattenTransform.MultiplyPoint3x4( newVertices[i] );
//				Debug.Log(newVertices[i] + " > " + transformedVertices[i]);
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
				
				Rect r = new Rect(0, 0, 1f, 1f);
				
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
			
			System.Array.Copy(newWeights, 0, newWeights, infillFrontOffset, uniqueVertexCount);
			System.Array.Copy(newWeights, 0, newWeights, infillBackOffset, uniqueVertexCount);
			
			System.Array.Copy(transformedVertices, 0, newUVs, infillFrontOffset, uniqueVertexCount);
			System.Array.Copy(transformedVertices, 0, newUVs, infillBackOffset, uniqueVertexCount);
			
			Vector3 infillFrontNormal = ((Vector3) plane) * -1f;
			infillFrontNormal.Normalize();
			
			for(int i = infillFrontOffset; i < infillBackOffset; i++)
				newNormals[i] = infillFrontNormal;
			
			Vector3 infillBackNormal = (Vector3) plane;
			infillBackNormal.Normalize();
			
			for(int i = infillBackOffset; i < newNormals.Length; i++)
				newNormals[i] = infillBackNormal;
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
		meshCache.normals.AddArray(newNormals);
		meshCache.UVs.AddArray(newUVs);
		meshCache.weights.AddArray(newWeights);

		//Now we need to fill in the slice hole. There are TWO infillers; the Sloppy and Meticulous.
		
		//The sloppy infiller will find a point in the middle of all slice vertices and produce a triangle fan.
		//It can work fast, but will have issues with non-roundish cross sections or cross sections with multiple holes.
		
		//The meticulous infill can distinguish between polygons and accurately fill multiple holes, but is more sensitive to
		//geometrical oddities. It may fail when slicing certain joints because of the way that not all geometry is sliced.
		//It is transferred from Turbo Slicer, where it is a key part of the product, but it is not most appropriate here.
		//Nevertheless, it is here in case it is needed.
		
		if(doInfill && infillMode == Infill.Sloppy)
		{
			VectorAccumulator centerVertex = new VectorAccumulator();
			VectorAccumulator centerUV = new VectorAccumulator();
			VectorAccumulator centerNormal = new VectorAccumulator();
			
			Dictionary<int,float> weightsByBone = new Dictionary<int,float>();
			
			int sliceVertexCount = newGeometryEstimate / 3;
			
			for(int i = 0; i < sliceVertexCount; i++)
			{
				centerVertex.addFigure( newVertices[i] );
				centerUV.addFigure ( newUVs[i] );
				centerNormal.addFigure( newNormals[i] );
				
				BoneWeight bw = newWeights[i];
				
				if(weightsByBone.ContainsKey(bw.boneIndex0))
					weightsByBone[bw.boneIndex0] += bw.weight0;
				else
					weightsByBone[bw.boneIndex0] = bw.weight0;
				
				/*if(weightsByBone.ContainsKey(bw.boneIndex1))
					weightsByBone[bw.boneIndex1] += bw.weight1;
				else
					weightsByBone[bw.boneIndex1] = bw.weight1;
				
				if(weightsByBone.ContainsKey(bw.boneIndex2))
					weightsByBone[bw.boneIndex2] += bw.weight2;
				else
					weightsByBone[bw.boneIndex2] = bw.weight2;
				
				if(weightsByBone.ContainsKey(bw.boneIndex3))
					weightsByBone[bw.boneIndex3] += bw.weight3;
				else
					weightsByBone[bw.boneIndex3] = bw.weight3;*/
			}
			
			List<KeyValuePair<int,float>> orderedWeights = new List<KeyValuePair<int,float>>( weightsByBone );

			orderedWeights.Sort((firstPair,nextPair) =>
			    {
			        return -firstPair.Value.CompareTo(nextPair.Value);
			    }
			);
			
			BoneWeight centerWeight = new BoneWeight();
			Vector4 weightNormalizer = Vector4.zero;
			
			if(orderedWeights.Count > 0)
			{
				centerWeight.boneIndex0 = orderedWeights[0].Key;
				weightNormalizer.x = 1f;
			}
			
			weightNormalizer.Normalize();
			
			centerWeight.weight0 = weightNormalizer.x;
			centerWeight.weight1 = weightNormalizer.y;
			centerWeight.weight2 = weightNormalizer.z;
			centerWeight.weight3 = weightNormalizer.w;
			
			int centerIndex = meshCache.vertices.Count;
			
			meshCache.vertices.Count++;
			meshCache.normals.Count++;
			meshCache.UVs.Count++;
			meshCache.weights.Count++;
			
			meshCache.vertices.array[centerIndex] = centerVertex.mean;
			meshCache.UVs.array[centerIndex] = centerUV.mean;
			meshCache.normals.array[centerIndex] = centerNormal.mean;
			meshCache.weights.array[centerIndex] = centerWeight;
			
			Vector2 transformedCenter = Vector2.zero;
			foreach(Vector2 v in transformedVertices)
				transformedCenter += v;
			transformedCenter /= transformedVertices.Length;
			
			Dictionary<int,float> angleByIndex = new Dictionary<int,float>();
			for(int i = 0; i < transformedVertices.Length; i++)
			{
				Vector2 delta = transformedVertices[i] - transformedCenter;
				angleByIndex[i] = Mathf.Atan2(delta.y, delta.x);
			}
			
			List<KeyValuePair<int,float>> orderedVertices = new List<KeyValuePair<int,float>>( angleByIndex );

			orderedVertices.Sort((firstPair,nextPair) =>
			    {
			        return firstPair.Value.CompareTo(nextPair.Value);
			    }
			);
			
			for(int i = 0; i < orderedVertices.Count; i++)
			{
				bool atEnd = i == orderedVertices.Count - 1;
				int iNext = atEnd ? 0 : i + 1;
				
				int index0 = orderedVertices[i].Key;
				int index1 = orderedVertices[iNext].Key;
				
				int[] frontInfillIndices = { centerIndex, index1 + infillFrontOffset + newIndexStartsAt, index0 + infillFrontOffset + newIndexStartsAt };
				frontInfill.AddArray(frontInfillIndices);
				
				int[] backInfillIndices = { centerIndex, index0 + infillBackOffset + newIndexStartsAt, index1 + infillBackOffset + newIndexStartsAt };
				backInfill.AddArray(backInfillIndices);
			}
		}
		else if(doInfill && infillMode == Infill.Meticulous)
		{
			//If that fails, one can use the more accurate but more delicate "meticulous" infiller.
	
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
						
						//Debug.Log("[fail]");
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
			
			foreach(List<int> _poly in allPolys)
			{
				Vector2[] poly = new Vector2[_poly.Count];
				
				for(int i = 0; i < poly.Length; i++)
				{
					int j = localIndexByIntersection[ _poly[i] ];
					poly[i] = transformedVertices[j];
				}
				
				int[] result;
				
				if(Triangulation.triangulate(poly, out result))
				{
					int[] front = new int[result.Length];
					int[] back = new int[result.Length];
					
					for(int i = 0; i < result.Length; i++)
					{
						int p = _poly[ result[i] ];
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
					
					frontInfill.AddArray(front);
					backInfill.AddArray(back);
				}
			}
		}
	}
}
