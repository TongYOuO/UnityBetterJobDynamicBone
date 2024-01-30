
#if  UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace DynamicBone.Scripts.SerializeData
{
    [CustomEditor(typeof(DynamicBone))]
    [CanEditMultipleObjects]
    public class DynamicBoneEditor:Editor
    {
        public override void OnInspectorGUI()
        {
            var dynamicBone = target as DynamicBone;

            // 显示一些状态属性
            // DispVersion();
            // DispStatus();
            // DispProxyMesh();
            
            serializedObject.Update();
            //兼容原本的序列化字段
            CopyValuesToSerializeData(dynamicBone);
            //支持撤销
            Undo.RecordObject(dynamicBone, "DynamicBone");
            EditorGUILayout.Space();
            ClothMainInspector();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            ClothParameterInspector();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            GizmoInspector();
            serializedObject.ApplyModifiedProperties();

          
        }
        
        /// <summary>
        /// 兼容原有序列化的字段支持新功能
        /// </summary>
        /// <param name="dynamicBone"></param>
        private void CopyValuesToSerializeData(DynamicBone dynamicBone)
        {
            if (dynamicBone.isConverted == false)
            {
                dynamicBone.isConverted = true;
            }
            else
            {
                return;
            }
            System.Reflection.FieldInfo[] fields = typeof(DynamicBone).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                var serializeDataField = typeof(DynamicBoneSerializeData).GetField(field.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (serializeDataField != null && serializeDataField.FieldType == field.FieldType)
                {
                    serializeDataField.SetValue(dynamicBone.SerializeData, field.GetValue(dynamicBone));
                }
            }
        }
        
        void ClothMainInspector()
        {
            var cloth = target as DynamicBone;
            bool runtime = EditorApplication.isPlaying;
            EditorGUILayout.LabelField("Main", EditorStyles.boldLabel);
            
            {
                using (new EditorGUI.IndentLevelScope())
                {
                
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_Root"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_Roots"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_EndLength"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_Colliders"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_Exclusions"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_ReferenceObject"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_DistantDisable"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_DistanceToObject"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_BlendWeight"));
                }
                
            }
            
        }
        void ClothParameterInspector()
        {
            var cloth = target as DynamicBone;
            DynamicBonePresetUtil.DrawPresetButton(cloth, cloth.SerializeData);
            Foldout("Force外力", null, () =>
            {
           
                EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_Gravity"), new GUIContent("Gravity"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_Force"));
            });
            Foldout("Motion移动限制", null, () =>
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_Inert"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_Damping"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_DampingDistrib"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_Friction"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_FrictionDistrib"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_FreezeAxis"));
            });
            Foldout("KeepShape形状约束", null, () =>
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_Elasticity"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_ElasticityDistrib"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_Stiffness"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_StiffnessDistrib"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_EndOffset"));
            });
            Foldout("CollideCollision碰撞约束", null, () =>
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_Colliders"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_Radius"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("serializeData.m_RadiusDistrib"));
            });
        }
        
        void GizmoInspector()
        {
            var cloth = target as DynamicBone;
        }
        
        public void Foldout(
            string foldKey,
            string title = null,
            System.Action drawAct = null,
            System.Action<bool> enableAct = null,
            bool enable = true
        )
        {
            var style = new GUIStyle("ShurikenModuleTitle");
            style.font = new GUIStyle(EditorStyles.label).font;
            style.border = new RectOffset(15, 7, 4, 4);
            style.fixedHeight = 22;
            style.contentOffset = new Vector2(20f, -2f);

            var rect = GUILayoutUtility.GetRect(16f, 22f, style);

            GUI.backgroundColor = Color.white;
            GUI.Box(rect, title ?? foldKey, style);

            var e = Event.current;
            bool foldOut = EditorPrefs.GetBool(foldKey);

            if (enableAct == null)
            {
                if (e.type == EventType.Repaint)
                {
                    var arrowRect = new Rect(rect.x + 4f, rect.y + 2f, 13f, 13f);
                    EditorStyles.foldout.Draw(arrowRect, false, false, foldOut, false);
                }
            }
            else
            {
                // 有効チェック
                var toggleRect = new Rect(rect.x + 4f, rect.y + 4f, 13f, 13f);
                bool sw = GUI.Toggle(toggleRect, enable, string.Empty, new GUIStyle("ShurikenCheckMark"));
                if (sw != enable)
                {
                    enableAct(sw);
                }
            }

            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                foldOut = !foldOut;
                EditorPrefs.SetBool(foldKey, foldOut);
                e.Use();
            }

            if (foldOut && drawAct != null)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    using (new EditorGUI.DisabledScope(!enable))
                    {
                        drawAct();
                    }
                }
            }
        }
    }

}
#endif