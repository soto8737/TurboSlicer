using UnityEngine;
using System.Collections;

namespace NobleMuffins.MuffinSlicer
{
	public class VectorAccumulator
	{
		private Vector3 aggregatedFigures = Vector3.zero;
		private int count = 0;
		
		public void addFigure(Vector3 v)
		{
			aggregatedFigures += v;
			count++;
		}
		
		public Vector3 mean {
			get {
				if(count == 0)
				{
					return Vector3.zero;
				}
				else
				{
					float f = (float) count;
					
					return aggregatedFigures / f;
				}
			}
		}
	}
}