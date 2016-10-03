using UnityEngine;
using System.Collections.Generic;

partial class TurboSlice
{
	private void createResultObjects(GameObject go, Sliceable sliceable, bool forceCloning, Vector4 plane, out GameObject frontObject, out GameObject backObject)
	{
		Transform goTransform = go.transform;
		
		Dictionary<string,Transform> transformByName;
		Dictionary<string,bool> frontPresence, backPresence;
		
		determinePresence(goTransform, plane, out transformByName, out frontPresence, out backPresence);
		
		bool useAlternateForFront, useAlternateForBack;
		
		if(sliceable.alternatePrefab == null)
		{
			useAlternateForFront = false;
			useAlternateForBack = false;
		}
		else if(sliceable.alwaysCloneFromAlternate)
		{
			useAlternateForFront = true;
			useAlternateForBack = true;
		}
		else
		{
			useAlternateForFront = sliceable.cloneAlternate(frontPresence);
			useAlternateForBack = sliceable.cloneAlternate(backPresence);
		}
		
		Object frontSource = useAlternateForFront ? sliceable.alternatePrefab : go;
		Object backSource = useAlternateForBack ? sliceable.alternatePrefab : go;
		
		frontObject = (GameObject) GameObject.Instantiate(frontSource);
		backObject = (GameObject) GameObject.Instantiate(backSource);
		
		handleHierarchy(frontObject.transform, frontPresence, transformByName);
		handleHierarchy(backObject.transform, backPresence, transformByName);
		
		Transform parent = goTransform.parent;
		
		Vector3 position = goTransform.localPosition;
		Vector3 scale = goTransform.localScale;
		
		Quaternion rotation = goTransform.localRotation;
		
		frontObject.transform.parent = parent;
		frontObject.transform.localPosition = position;
		frontObject.transform.localScale = scale;
		
		backObject.transform.parent = parent;
		backObject.transform.localPosition = position;
		backObject.transform.localScale = scale;
		
		frontObject.transform.localRotation = rotation;
		backObject.transform.localRotation = rotation;
		
		frontObject.layer = go.layer;
		backObject.layer = go.layer;
		
		
		Rigidbody originalRigidBody = go.GetComponent<Rigidbody>();
		
		if(originalRigidBody != null)
		{
			Rigidbody frontRigidBody = frontObject.GetComponent<Rigidbody>();
			Rigidbody backRigidBody = backObject.GetComponent<Rigidbody>();
			
			if(frontRigidBody != null)
			{
				frontRigidBody.angularVelocity = originalRigidBody.angularVelocity;
				frontRigidBody.velocity = originalRigidBody.velocity;
			}
			
			if(backRigidBody != null)
			{
				backRigidBody.angularVelocity = originalRigidBody.angularVelocity;
				backRigidBody.velocity = originalRigidBody.velocity;
			}
		}
	}
	
}































