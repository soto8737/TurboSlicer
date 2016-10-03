using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class TSD5FailSounder : MonoBehaviour
{
	private int lastObservedMissCount = 0;
	
	void Update ()
	{		
		int observedMissCount = TSD5ScoreModel.instance.missed;
		
		if(observedMissCount > lastObservedMissCount)
		{
			GetComponent<AudioSource>().Play();
		}
		
		lastObservedMissCount = observedMissCount;
	}
}
