using UnityEngine;
using System.Collections;

public class TSD3Fate : MonoBehaviour
{
	public float lifetime = 3f;

	void Start ()
	{
		GameObject.Destroy(gameObject, lifetime);
	}
}
