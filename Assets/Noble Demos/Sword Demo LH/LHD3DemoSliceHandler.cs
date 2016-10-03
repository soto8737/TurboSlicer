using UnityEngine;
using System.Collections;

public class LHD3DemoSliceHandler : AbstractSliceHandler
{
	public float explosiveForce = 50f, explosiveRadius = 50f;
	
	public override void handleSlice( GameObject[] results )
	{
		Vector3 otherPosition = transform.position;
	
		foreach(GameObject g in results)
		{
			if(g.GetComponent<Rigidbody>() != null)
				g.GetComponent<Rigidbody>().AddExplosionForce(explosiveForce, otherPosition, explosiveRadius);
		}
	}
}
