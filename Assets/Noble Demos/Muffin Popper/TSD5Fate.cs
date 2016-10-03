using UnityEngine;
using System.Collections;

public class TSD5Fate : MonoBehaviour
{
	public float lifetime = 3f;

	void Start ()
	{
		GameObject.Destroy(gameObject, lifetime);
	}
}
