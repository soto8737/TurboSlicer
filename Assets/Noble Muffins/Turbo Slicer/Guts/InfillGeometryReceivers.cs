using System;
using UnityEngine;

/*
 * This here sets up a callback requested by a customer. Any methods added to this delegate
 * will be called whenever an infill occrs, and will be given the vertices, UVs, normals and indices of
 * the infill.
 * 
 * I (developer at Noble Muffins) do not know the use case, but if you want to receive infill
 * geometry when the infill occurs, feel free to sign up to this delegate.
 * 
 * -Toby
 * 
 * */

public partial class TurboSlice
{
	public static System.Action<Vector3[],Vector2[],Vector3[],int[]> infillGeometryReceivers;
}
