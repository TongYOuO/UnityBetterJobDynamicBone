﻿using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Transform))]
public class TransformGUI : Editor
{
    public override void OnInspectorGUI()
    {
        var transform = target as Transform;

        EditorGUI.BeginChangeCheck();

        var pos_prop = serializedObject.FindProperty("m_LocalPosition");
        var rot_prop = serializedObject.FindProperty("m_LocalRotation");
        var scal_prop = serializedObject.FindProperty("m_LocalScale");
        
        var pos = EditorGUILayout.Vector3Field("Position", pos_prop.vector3Value);
        var rot = EditorGUILayout.Vector3Field("Rotation", rot_prop.quaternionValue.eulerAngles);
        var scal = EditorGUILayout.Vector3Field("Scale", scal_prop.vector3Value);

        EditorGUILayout.HelpBox($"World Pos : {transform.position}", MessageType.Info);

        if (EditorGUI.EndChangeCheck())
        {
            Transform trans = target as Transform;
            var mr = trans.GetComponent<MeshRenderer>();
            var mf = trans.GetComponent<MeshFilter>();
            pos_prop.vector3Value = pos;
            rot_prop.quaternionValue = Quaternion.Euler(rot.x, rot.y, rot.z);
            scal_prop.vector3Value = scal;
            serializedObject.ApplyModifiedProperties();
        }
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.HelpBox("This Inspector has been overrided", MessageType.Info);
        EditorGUI.EndDisabledGroup();
    }

    public override bool RequiresConstantRepaint()
    {
        return true;
    }
}