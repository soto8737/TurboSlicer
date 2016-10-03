using UnityEngine;
using System.Collections;

public class TSD3ScoreModel
{
	public readonly static TSD3ScoreModel instance = new TSD3ScoreModel();
	
	public int primarySlices = 0, secondarySlices = 0, missed = 0;
	
	public int slices { get { return primarySlices + secondarySlices; } }
}
