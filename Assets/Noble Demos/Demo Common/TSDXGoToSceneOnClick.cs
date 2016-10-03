using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class TSDXGoToSceneOnClick : MonoBehaviour
{
	const float changeTime = 0.033f;
	
	public string targetScene;
	public new Camera camera;
	
	public AudioClip clickSound;
	
	private new Transform transform;
	private new Collider collider;
	private Vector3 scaleAtStart;
	
	private float size = 1f, sizeDelta = 0f;
	private bool pressedAsButton = false;
	
	void Start()
	{
		transform = GetComponent<Transform>();
		collider = GetComponent<Collider>();

		scaleAtStart = transform.localScale;
		
		if(camera == null) camera = Camera.main;
	}
	
	void Update()
	{
		Ray ray = camera.ScreenPointToRay(Input.mousePosition);
		
		RaycastHit hitInfo;
		
		bool hover = collider.Raycast(ray, out hitInfo, 2f);
		
		pressedAsButton |= hover && Input.GetMouseButtonDown(0);
		
		bool released = Input.GetMouseButtonUp(0);
		
		bool releasedAsButton = pressedAsButton && hover && released;
		
		if(released)
		{
			pressedAsButton = false;
		}
		
		if(releasedAsButton)
		{
			Application.LoadLevel(targetScene);
			
			if(clickSound != null)
			{
				/*GameObject go = new GameObject();
				AudioSource source = go.AddComponent<AudioSource>();
				source.clip = clickSound;
				GameObject.DontDestroyOnLoad(go);*/
				AudioSource.PlayClipAtPoint(clickSound, Vector3.zero);
			}
		}
		
		bool enlarge = hover || pressedAsButton;
		
		float idealSize = enlarge ? 1.1f : 1f;
		
		size = Mathf.SmoothDamp(size, idealSize, ref sizeDelta, changeTime);
		
		transform.localScale = size * scaleAtStart;
	}
}
