using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class TSD4FailSounder : MonoBehaviour
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
