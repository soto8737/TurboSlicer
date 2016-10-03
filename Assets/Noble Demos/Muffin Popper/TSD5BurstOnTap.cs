using UnityEngine;
using System.Collections;

public class TSD5BurstOnTap : MonoBehaviour
{
	void OnMouseDown() {
		TurboSlice.instance.shatter(gameObject, 3);
	}

}
