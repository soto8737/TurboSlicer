using UnityEngine;
using System.Collections;

public class TSDXWobblyCamera : MonoBehaviour
{
	private new Transform transform;
	private new Camera camera;
	
	private float startTime;
	
	public float tiltPeriod = 10f;
	public float tiltAmplitude = 10f;
	
	public float zoomPeriod = 13f;
	public float zoomAmplitude = 0.03f;
	
	
	// Use this for initialization
	void Start () {
		transform = GetComponent<Transform>();
		camera = GetComponent<Camera>();
		
		startTime = Time.time;
	}
	
	// Update is called once per frame
	void Update () {
		float t = 2f * Mathf.PI * (Time.time - startTime);
		
		float tilt = Mathf.Sin(t / tiltPeriod) * tiltAmplitude;
		
		transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
		
		camera.orthographicSize = 1f + Mathf.Sin(t / zoomPeriod) * zoomAmplitude;
	}
}
