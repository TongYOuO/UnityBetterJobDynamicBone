using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DynamicBone.Scripts.SerializeData
{
    public class DynamicBonePresetUtil
    {
        #if UNITY_EDITOR
        const string prefix = "Preset";
        const string configName = "preset folder";

        public static void DrawPresetButton(JobifyDynamicBone cloth, DynamicBoneSerializeData sdata)
        {
            bool save = false;
            bool load = false;
            using (var horizontalScope = new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("参数", EditorStyles.boldLabel);

                GUI.backgroundColor = Color.green;
                if (EditorGUILayout.DropdownButton(new GUIContent("预设"), FocusType.Keyboard, GUILayout.Width(70), GUILayout.Height(16)))
                {
                    CreatePresetPopupMenu(cloth, sdata);
                    GUI.backgroundColor = Color.white;
                    //GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("保存", GUILayout.Width(40), GUILayout.Height(16)))
                {
                    //SavePreset(sdata);
                    //GUIUtility.ExitGUI();
                    save = true;
                }
                if (GUILayout.Button("加载", GUILayout.Width(40), GUILayout.Height(16)))
                {
                    //LoadPreset(cloth, sdata);
                    //GUIUtility.ExitGUI();
                    load = true;
                }
            }
            if (save)
                SavePreset(sdata);
            if (load)
                LoadPreset(cloth, sdata);
        }

        static string GetComponentTypeName(DynamicBoneSerializeData sdata)
        {
            //if (sdata.clothType == ClothProcess.ClothType.BoneCloth)
            //    return prefix + "BoneCloth";
            //else if (sdata.clothType == ClothProcess.ClothType.MeshCloth)
            //    return prefix + "MeshCloth";
            //return prefix;

            return prefix;
        }


        class PresetInfo
        {
            public string presetPath;
            public string presetName;
            public TextAsset text;
        }

        private static void CreatePresetPopupMenu(JobifyDynamicBone cloth, DynamicBoneSerializeData sdata)
        {
            var guidArray = AssetDatabase.FindAssets($"{prefix} t:{nameof(TextAsset)}");
            if (guidArray == null)
                return;

            Dictionary<string, List<PresetInfo>> dict = new Dictionary<string, List<PresetInfo>>();
            foreach (var guid in guidArray)
            {
                var filePath = AssetDatabase.GUIDToAssetPath(guid);

                // json确认
                if (filePath.EndsWith(".json") == false)
                    continue;

                var text = AssetDatabase.LoadAssetAtPath<TextAsset>(filePath);
                if (text)
                {
                    var info = new PresetInfo();
                    info.presetPath = filePath;
                    var fname = Path.GetFileNameWithoutExtension(filePath);
                    fname = fname.Replace(prefix, "");
                    if (fname.StartsWith("_"))
                        fname = fname.Remove(0, 1); // 头的_是删除的
                    info.presetName = fname;
                    info.text = text;

                    // 按目录记录
                    var dirName = Path.GetDirectoryName(filePath);
                    if (dict.ContainsKey(dirName) == false)
                    {
                        dict.Add(dirName, new List<PresetInfo>());
                    }
                    dict[dirName].Add(info);
                }
            }

            // 创建弹出菜单
            // 按目录分隔显示
            var menu = new GenericMenu();
            int line = 0;
            foreach (var kv in dict)
            {
                if (line > 0)
                {
                    menu.AddSeparator("");
                }
                foreach (var info in kv.Value)
                {
                    var textAsset = info.text;
                    var presetName = info.presetName;
                    var presetPath = info.presetPath;
                    menu.AddItem(new GUIContent(presetName), false, () =>
                    {
                        // 加载
                        Debug.Log("加载预设文件:" + presetPath);
                        if (sdata.ImportJson(textAsset.text))
                            Debug.Log("完成。");
                        else
                            Debug.LogError("预设加载错误！");

                        LoadPresetFinish(cloth);
                    });
                }
                line++;
            }
            menu.ShowAsContext();
        }

        ///<summary>
        /// 保存预设文件
        /// </summary>
        ///<param name="clothParam"></param>
        private static void SavePreset(DynamicBoneSerializeData sdata)
        {
            // 读取文件夹
            string folder = EditorUserSettings.GetConfigValue(configName);

            // 前缀
            string presetTypeName = GetComponentTypeName(sdata);

            // 保存对话框
            string path = EditorUtility.SaveFilePanelInProject(
                "保存预设",
                $"{presetTypeName}_(名称)",
                "json",
                "输入预设json的名称。",
                folder
                );
            if (string.IsNullOrEmpty(path))
                return;

            // 记录文件夹
            folder = Path.GetDirectoryName(path);
            EditorUserSettings.SetConfigValue(configName, folder);

            Debug.Log("保存预设文件:" + path);

            // 导出json
            string json = sdata.ExportJson();

            // 保存
            File.WriteAllText(path, json);

            AssetDatabase.Refresh();

            Debug.Log("完成。");
        }

        ///<summary>
        /// 加载预设文件
        /// </summary>
        ///<param name="clothParam"></param>
        private static void LoadPreset(JobifyDynamicBone cloth, DynamicBoneSerializeData sdata)
        {
            // 读取文件夹
            string folder = EditorUserSettings.GetConfigValue(configName);

            // 加载对话框
            string path = EditorUtility.OpenFilePanel("加载预设", folder, "json");
            if (string.IsNullOrEmpty(path))
                return;

            // 记录文件夹
            folder = Path.GetDirectoryName(path);
            EditorUserSettings.SetConfigValue(configName, folder);

            // 导入json
            Debug.Log("加载预设文件:" + path);
            string json = File.ReadAllText(path);

            // 加载
            if (sdata.ImportJson(json))
                Debug.Log("完成。");
            else
                Debug.LogError("预设加载错误！");

            LoadPresetFinish(cloth);
        }

        ///<summary>
        /// 预设文件加载后处理
        /// </summary>
        ///<param name="cloth"></param>
        private static void LoadPresetFinish(JobifyDynamicBone cloth)
        {
            if (EditorApplication.isPlaying)
            {
                // // 参数更新通知
                // cloth.SetParameterChange();
            }
            else
            {
                // 序列化更改通知
                EditorUtility.SetDirty(cloth);
            }
        }
 
        #endif
    }
    
}