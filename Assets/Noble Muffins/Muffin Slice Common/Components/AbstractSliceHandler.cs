using UnityEngine;
using System.Collections.Generic;

public abstract class AbstractSliceHandler : MonoBehaviour
{
	public virtual void handleSlice( GameObject[] results )
	{
		//Do nothing
	}
	
	public virtual bool cloneAlternate ( Dictionary<string,bool> hierarchyPresence )
	{
		return true;
	}
}
