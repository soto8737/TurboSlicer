using UnityEngine;
using System.Collections;

public class TSD4Fate : MonoBehaviour
{
	public float lifespan = 3f;

	// Use this for initialization
	void Start () {
		GameObject.Destroy(gameObject, lifespan);
	}
}
