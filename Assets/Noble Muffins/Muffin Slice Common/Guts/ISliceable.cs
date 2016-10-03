using UnityEngine;
using System.Collections;

public interface ISliceable
{
	GameObject[] Slice(Vector3 positionInWorldSpace, Vector3 normalInWorldSpace);
}
