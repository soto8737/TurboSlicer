using UnityEngine;
using System.Collections.Generic;

public class LHD3MarkOfCain : MonoBehaviour
{
	private readonly static List<GameObject> markedObjects = new List<GameObject>();
	
	void Start()
	{
		markedObjects.Add(gameObject);
	}
	
	public static void DestroyAllMarkedObjects()
	{
		foreach(GameObject go in markedObjects)
		{
			if(go != null) GameObject.Destroy(go);
		}
		
		markedObjects.Clear();
	}
}
