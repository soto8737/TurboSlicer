using UnityEngine;

[ExecuteInEditMode]
public class TSDXHorizontalAnchor : MonoBehaviour {

	public float x;
	
	// Update is called once per frame
	void Update () {
		Camera c = Camera.main;
		if(c != null)
		{
			Transform t = transform;
			Vector3 v = t.localPosition;
			v.x = x * Camera.main.aspect;
			t.localPosition = v;
		}
	}
}
