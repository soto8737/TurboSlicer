using UnityEngine;
using System.Collections.Generic;

namespace NobleMuffins.MuffinSlicer
{
	public static class Extensions
	{
		public static Vector3 Sum(this IEnumerable<Vector3> set)
		{
			var sum = Vector3.zero;
			foreach(var v in set) {
				sum += v;
			}
			return sum;
		}

		public static Vector3 Average(this ICollection<Vector3> set)
		{
			var average = set.Sum () / (float) set.Count;
			return average;
		}
	}
}

