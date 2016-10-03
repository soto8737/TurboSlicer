using UnityEngine;
using System.Collections;

public class TSD4SoundWhenSliced : AbstractSliceHandler
{
	public AudioClip clip;
	
	public override void handleSlice( GameObject[] results )
	{
		if(clip != null) {
			GameObject go = new GameObject();
			
			go.transform.position = transform.position;
			
			AudioSource source = go.AddComponent<AudioSource>();
			
			source.clip = clip;
			source.Play();
			
			GameObject.Destroy(go, clip.length);
		}
	}
}
