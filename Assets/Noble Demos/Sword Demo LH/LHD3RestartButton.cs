using UnityEngine;
using System.Collections;

[RequireComponent(typeof(TSDXButton))]
public class LHD3RestartButton : MonoBehaviour
{
	public LHD3Spawner spawner;

	private TSDXButton button;

	void Start() {
		button = gameObject.GetComponent<TSDXButton>();
	}

	void Update () {
		button.visible = spawner.CanInstantiate;
	}

	void OnClick() {
		spawner.Instantiate();
	}
}
