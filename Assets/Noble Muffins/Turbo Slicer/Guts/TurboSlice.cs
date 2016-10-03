using UnityEngine;
using System.Collections.Generic;
using NobleMuffins.MuffinSlicer;

public partial class TurboSlice : MonoBehaviour
{
	private readonly Dictionary<Mesh,MeshCache> meshCaches = new Dictionary<Mesh, MeshCache>();
	private readonly Queue<Mesh> meshDeletionQueue = new Queue<Mesh>();
	
	private static TurboSlice _instance;
	public static TurboSlice instance {
		get {
			if(_instance == null)
			{
				GameObject go = new GameObject();
				_instance = go.AddComponent<TurboSlice>();
			}
			return _instance;
		}
	}
	
	// Use this for initialization
	void Start ()
	{
		if(_instance == null)
		{
			_instance = this;
		}
		else if(_instance != this)
		{
			GameObject.Destroy(gameObject);
		}
	}
	
	private void releaseMesh(Mesh m)
	{		
		if(meshCaches != null && meshCaches.ContainsKey(m))
		{
			meshCaches.Remove(m);
		}
		
		GameObject.DestroyImmediate(m);
	}
	
	// Update is called once per frame
	void Update ()
	{
		const float meshCacheTimeout = 5f;
		
		float t = Time.time;

		var meshCacheDeletionQueue = new Queue<Mesh>();

		foreach(KeyValuePair<Mesh,MeshCache> kvp in meshCaches)
		{
			float age = t - kvp.Value.creationTime;
			
			bool timedOut = age > meshCacheTimeout;
			
			if(timedOut)
			{
				meshCacheDeletionQueue.Enqueue(kvp.Key);
			}
		}

		while(meshCacheDeletionQueue.Count > 0)
		{
			var key = meshCacheDeletionQueue.Dequeue();

			meshCaches.Remove(key);
		}

		while(meshDeletionQueue.Count > 0)
		{
			Mesh mesh = meshDeletionQueue.Dequeue();
			releaseMesh(mesh);
		}
	}
}
