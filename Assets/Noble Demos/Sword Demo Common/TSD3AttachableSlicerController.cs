using UnityEngine;
using System.Collections;

[RequireComponent(typeof(TSD3SwordVelocityFilter))]
public class TSD3AttachableSlicerController : MonoBehaviour
{
	private TSD3SwordVelocityFilter swordVelocityFilter;
	public GameObject slicer;
	
	void Start ()
	{
		swordVelocityFilter = GetComponent<TSD3SwordVelocityFilter>();
	}

	void Update ()
	{
		slicer.SetActive( swordVelocityFilter.IsFastEnoughToCut );
	}
}
