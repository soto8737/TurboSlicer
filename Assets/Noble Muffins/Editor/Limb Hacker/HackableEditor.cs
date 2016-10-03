using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using NobleMuffins.MuffinSlicer;

[CustomEditor(typeof(Hackable))]
public class HackableEditor : Editor
{
	public override void OnInspectorGUI ()
	{
		Hackable s = (Hackable) target;

		try
		{
			var allBones = LimbHacker.FindBonesInTree(s.gameObject);
			
			s.alternatePrefab = EditorGUILayout.ObjectField("Alternate prefab", (Object) s.alternatePrefab, typeof(GameObject), false);
			
			s.infillMaterial = (Material) EditorGUILayout.ObjectField("Infill Material", (Object) s.infillMaterial, typeof(Material), false);
			
			if(s.infillMaterial != null)
			{
				s.infillMode = (LimbHacker.Infill) EditorGUILayout.EnumPopup("Infill Mode", s.infillMode);
			}

			EditorGUILayout.LabelField("Select which bones are severable:");
					
			var selectedBones = new List<Transform>();

			var selectAll = GUILayout.Button("Select all");
			
			foreach(var bone in allBones)
			{
				bool wasSelected = System.Array.IndexOf(s.severables, bone) >= 0;
				bool isSelected = EditorGUILayout.Toggle(bone.name, wasSelected) || selectAll;
				
				if(isSelected)
					selectedBones.Add(bone);
			}
			
			s.severables = selectedBones.ToArray();
			
	        if (GUI.changed)
			{
	            EditorUtility.SetDirty (target);
			}
		}
		catch(LimbHacker.ForestException ex)
		{
			Debug.LogError(ex.Message);
			EditorGUILayout.LabelField("This object must have SkinnedMeshRenderers referring to a single tree.");
		}
    }

}