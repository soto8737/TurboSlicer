using UnityEngine;
using System.Collections.Generic;
using NobleMuffins.MuffinSlicer;

public partial class TurboSlice
{
	public GameObject[] splitByPlane(GameObject go, Vector4 planeInLocalSpace, bool destroyOriginal)
	{
		return _splitByPlane(go, planeInLocalSpace, destroyOriginal);
	}
		
	public GameObject[] splitByLine(GameObject target, Camera camera, Vector3 _start, Vector3 _end)
	{
		return splitByLine(target, camera, _start, _end, true);
	}
	
	public GameObject[] splitByLine(GameObject target, Camera camera, Vector3 _start, Vector3 _end, bool destroyOriginal)
	{
		Vector3 targetPositionRelativeToCamera = camera.transform.worldToLocalMatrix.MultiplyPoint3x4( target.transform.position );
		
		_start.z = targetPositionRelativeToCamera.z;
		_end.z = targetPositionRelativeToCamera.z;
		
		Vector3 _middle = (_start + _end) / 2f;
		_middle.z *= 2f;
				
		Vector3 start = camera.ScreenToWorldPoint(_start);
		Vector3 middle = camera.ScreenToWorldPoint(_middle);
		Vector3 end = camera.ScreenToWorldPoint(_end);
		
		return splitByTriangle(target, new[] { start, middle, end }, destroyOriginal );
	}
	
	public GameObject[] splitByTriangle(GameObject target, Vector3[] triangleInWorldSpace, bool destroyOriginal)
	{
		Vector3[] t = new Vector3[3];
		
		Matrix4x4 matrix = target.transform.worldToLocalMatrix;
		
		t[0] = matrix.MultiplyPoint3x4(triangleInWorldSpace[0]);
		t[1] = matrix.MultiplyPoint3x4(triangleInWorldSpace[1]);
		t[2] = matrix.MultiplyPoint3x4(triangleInWorldSpace[2]);
		
		Vector4 plane = Vector4.zero;
		
		plane.x = t[0].y * (t[1].z - t[2].z) + t[1].y * (t[2].z - t[0].z) + t[2].y * (t[0].z - t[1].z);
		plane.y = t[0].z * (t[1].x - t[2].x) + t[1].z * (t[2].x - t[0].x) + t[2].z * (t[0].x - t[1].x);
		plane.z = t[0].x * (t[1].y - t[2].y) + t[1].x * (t[2].y - t[0].y) + t[2].x * (t[0].y - t[1].y);
		plane.w = -( t[0].x * (t[1].y * t[2].z - t[2].y * t[1].z) + t[1].x * (t[2].y * t[0].z - t[0].y * t[2].z) + t[2].x * (t[0].y * t[1].z - t[1].y * t[0].z) );
		
		return _splitByPlane(target, plane, destroyOriginal);
	}
	
	public GameObject[] shatter(GameObject go, int steps)
	{
		Sliceable priorSliceable = go.GetComponent<Sliceable>();

		List<GameObject> l = new List<GameObject>();
		
		l.Add(go);
		
		List<GameObject> l2 = l;
		
		for(int i = 0; i < steps; i++)
		{
			l2 = new List<GameObject>(l.Count * 2);
			
			Vector4 shatterPlane = (Vector4) Random.insideUnitSphere;
			
			foreach(GameObject go2 in l)
			{
				l2.AddRange( _splitByPlane(go2, shatterPlane, true, false) );
			}
			
			l = l2;
		}

		GameObject[] results = l2.ToArray();

		if(priorSliceable != null)
		{
			priorSliceable.handleSlice(results);
		}

		return results;
	}
}
