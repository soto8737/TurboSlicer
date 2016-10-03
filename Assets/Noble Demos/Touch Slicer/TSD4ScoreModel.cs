using UnityEngine;
using System.Collections;

public class TSD4ScoreModel
{
	public readonly static TSD4ScoreModel instance = new TSD4ScoreModel();
	
	public int primarySlices = 0, secondarySlices = 0, missed = 0;
	
	public int slices { get { return primarySlices + secondarySlices; } }
}
