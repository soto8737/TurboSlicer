using UnityEngine;
using System.Collections;

namespace NobleMuffins.MuffinSlicer
{
	public enum PlaneTriResult {
		PTR_FRONT, PTR_BACK, PTR_SPLIT
	};
		
	public class MuffinSliceCommon
	{
		//A const for internal use
		const float epsilon = 0f;
		
		public static Vector3 clampNormalToBicone(Vector3 input, Vector3 axis, float maximumDegrees)
		{
			float minimumDotProduct = Mathf.Cos( maximumDegrees * Mathf.Deg2Rad );
						
			float dotProduct = Vector3.Dot(input, axis);
		
			Vector3 result = input;
			
			if(Mathf.Abs(dotProduct) < minimumDotProduct)
			{
				float sign = Mathf.Sign(dotProduct);
				
				float differenceBetweenNowAndIdeal = minimumDotProduct - Mathf.Abs( dotProduct );
				
				Vector3 repairativeContribution = axis * differenceBetweenNowAndIdeal * sign;
							
				float currentCorrective = 1f;
				
				float lowCorrective = 1f;
				float highCorrective = 100f;
				
				int iterations = 16;
				
				while(iterations > 0)
				{
					result = (input + repairativeContribution * currentCorrective).normalized;
					
					float dp = Mathf.Abs( Vector3.Dot(result, axis) );
					
					if(dp > minimumDotProduct)
					{
						highCorrective = currentCorrective;
						currentCorrective = (currentCorrective + lowCorrective) / 2f;					
					}
					else if(dp < minimumDotProduct)
					{
						lowCorrective = currentCorrective;
						currentCorrective = (currentCorrective + highCorrective) / 2f;
					}
					
					iterations--;
				}
			}
			
			return result;
		}
		
		public static Vector4 planeFromPointAndNormal(Vector3 point, Vector3 normal)
		{
			Vector4 plane = (Vector4) normal.normalized;
			plane.w = -(normal.x * point.x + normal.y * point.y + normal.z * point.z);
			return plane;
		}
		
		public static float classifyPoint(ref Vector4 plane, ref Vector3 p)
		{
	    	return p.x * plane.x + p.y * plane.y + p.z * plane.z + plane.w;
		}
		
		public static PlaneTriResult getSidePlane(ref Vector3 p, ref Vector4 plane)
		{
		  double d = distanceToPoint(ref p, ref plane);
		
		  if ( (d+epsilon) > 0 )
				return PlaneTriResult.PTR_FRONT; // it is 'in front' within the provided epsilon value.
	
		  return PlaneTriResult.PTR_BACK;
		}
		
		public static float distanceToPoint(ref Vector3 p, ref Vector4 plane)
		{
			float d = p.x * plane.x + p.y * plane.y + p.z * plane.z + plane.w;
			return d;
		}
		
		public static float intersectCommon(ref Vector3 p1, ref Vector3 p2, ref Vector4 plane)
		{
			float dp1 = distanceToPoint(ref p1, ref plane);
			
			Vector3 dir = p2 - p1;
			
			float dot1 = dir.x * plane.x + dir.y * plane.y + dir.z * plane.z;
			float dot2 = dp1 - plane.w;
			
			float t = -(plane.w + dot2 ) / dot1;
			
			return t;
		}
	}

}