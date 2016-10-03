using UnityEngine;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public class TSD4Target : MonoBehaviour
{
	private static readonly List<TSD4Target> _targets = new List<TSD4Target>();
	public static IList<TSD4Target> targets { get { return _targets.AsReadOnly(); } }
	
	[HideInInspector] public new Transform transform;
	[HideInInspector] public new Renderer renderer;
	
	void Awake()
	{
		transform = GetComponent<Transform>();
		renderer = GetComponent<Renderer>();
	}
	
	void OnEnable()
	{
		_targets.Add(this);
	}
	
	void OnDisable() 
	{
		_targets.Remove(this);
	}
}
