using UnityEngine;
using System.Collections;

[RequireComponent(typeof(TextMesh))]
public class TSD5ScoreView : MonoBehaviour
{
	private TextMesh textMesh;
	
	private int lastObservedScore = -1;
	
	public string format = "Score: {0}";
	
	void Start ()
	{
		textMesh = GetComponent<TextMesh>();
	}
	
	void Update ()
	{
		int observedScore = TSD5ScoreModel.instance.slices;
		
		if(observedScore != lastObservedScore)
		{
			textMesh.text = string.Format(format, observedScore);
			
			lastObservedScore = observedScore;
		}
	}
}
