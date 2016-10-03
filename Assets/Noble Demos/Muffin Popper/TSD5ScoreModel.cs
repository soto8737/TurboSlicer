using UnityEngine;
using System.Collections;

public class TSD5ScoreModel
{
	public readonly static TSD5ScoreModel instance = new TSD5ScoreModel();
	
	public int primarySlices = 0, secondarySlices = 0, missed = 0;
	
	public int slices { get { return primarySlices + secondarySlices; } }
}
