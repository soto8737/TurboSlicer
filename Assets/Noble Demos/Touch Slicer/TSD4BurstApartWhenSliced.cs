using UnityEngine;
using System.Collections;

public class TSD4BurstApartWhenSliced : AbstractSliceHandler
{
	public float burstForce = 100f;
	
	public override void handleSlice( GameObject[] results )
	{
		Vector3[] centers = new Vector3[results.Length];
		
		for(int i = 0; i < results.Length; i++) centers[i] = results[i].GetComponent<Collider>().bounds.center;
		
		Vector3 center = Vector3.zero;
		for(int i = 0; i < centers.Length; i++) center += centers[i];
		center /= (float) centers.Length;
		
		for(int i = 0; i < results.Length; i++)
		{
			GameObject go = results[i];
			Rigidbody rb = go.GetComponent<Rigidbody>();
			if(rb != null)
			{
				Vector3 v = centers[i] - center;
				v.Normalize();
				v *= burstForce;
				rb.AddForce(v);
			}
		}
	}
}
