using UnityEngine;

public class TSCallbackOnDestroy : MonoBehaviour
{
	public Mesh mesh;
	public System.Action<Mesh> callWithMeshOnDestroy;
	
	void OnDestroy()
	{
		callWithMeshOnDestroy(mesh);
		mesh = null;
	}
}
