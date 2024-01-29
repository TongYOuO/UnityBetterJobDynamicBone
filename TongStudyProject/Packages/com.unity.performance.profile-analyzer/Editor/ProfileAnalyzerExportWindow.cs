using UnityEngine;
using System.IO;
using System;
using System.Collections.Generic;

namespace UnityEditor.Performance.ProfileAnalyzer
{
    internal class ProfileAnalyzerExportWindow : EditorWindow
    {
        internal static class Styles
        {
            public static readonly GUIContent markerTable = new GUIContent("Marker table", "Export data from the single view marker table");
            public static readonly GUIContent singleFrameTimes = new GUIContent("Single Frame Times", "Export frame time data from the single view");
            public static readonly GUIContent comparisonFrameTimes = new GUIContent("Comparison Frame Times", "Export frame time data from the comparison view");
            public static readonly GUIContent setMarkerCfg = new GUIContent("保存导出列表", "");
            public static readonly GUIContent readMarkerCfg = new GUIContent("加载导出列表", "");
            public static readonly GUIContent exportCfgMarkerData = new GUIContent("一键导出", "需要配置需要导出的函数名列表");
            public static readonly GUIContent exportCmpData = new GUIContent("导出对比数据");
        }

        ProfileDataView m_ProfileDataView;
        ProfileDataView m_LeftDataView;
        ProfileDataView m_RightDataView;
        ProfileAnalyzerWindow m_ProfileAnalyzerWindow;

        static public ProfileAnalyzerExportWindow FindOpenWindow()
        {
            UnityEngine.Object[] windows = Resources.FindObjectsOfTypeAll(typeof(ProfileAnalyzerExportWindow));
            if (windows != null && windows.Length > 0)
                return windows[0] as ProfileAnalyzerExportWindow;

            return null;
        }

        static public bool IsOpen()
        {
            if (FindOpenWindow() != null)
                return true;

            return false;
        }

        static public ProfileAnalyzerExportWindow Open(float screenX, float screenY, ProfileDataView profileSingleView, ProfileDataView profileLeftView, ProfileDataView profileRightView, ProfileAnalyzerWindow profileAnalyzerWindow)
        {
            ProfileAnalyzerExportWindow window = GetWindow<ProfileAnalyzerExportWindow>("Export");
            window.minSize = new Vector2(250, 350);
            window.position = new Rect(screenX, screenY, 250, 350);
            window.SetData(profileSingleView, profileLeftView, profileRightView, profileAnalyzerWindow);
            window.Show();

            return window;
        }

        static public void CloseAll()
        {
            ProfileAnalyzerExportWindow window = GetWindow<ProfileAnalyzerExportWindow>("Export");
            window.Close();
        }

        public void SetData(ProfileDataView profileDataView, ProfileDataView leftDataView, ProfileDataView rightDataView, ProfileAnalyzerWindow profileAnalyzerWindow)
        {
            m_ProfileDataView = profileDataView;
            m_LeftDataView = leftDataView;
            m_RightDataView = rightDataView;
            m_ProfileAnalyzerWindow = profileAnalyzerWindow;

            if (exportFuncNameListObject == null) exportFuncNameListObject = new SerializedObject(this);
            listProperty = exportFuncNameListObject.FindProperty("exportFuncNameList");
        }

        [SerializeField]
        protected List<string> exportFuncNameList = new List<string>();
        protected SerializedObject exportFuncNameListObject;
        protected SerializedProperty listProperty;
        protected Vector2 cfgListScrollPos = Vector2.zero;

        protected string exportCmpA = string.Empty;
        protected string exportCmpB = string.Empty;

        void OnGUI()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label("Export as CSV:");
            GUILayout.Label("");

            GUILayout.Label("Single View");

            bool enabled = GUI.enabled;
            if (m_ProfileDataView == null || !m_ProfileDataView.IsDataValid())
                GUI.enabled = false;
            if (GUILayout.Button(Styles.markerTable))
                SaveMarkerTableCSV();
            GUI.enabled = enabled;

            if (m_ProfileDataView == null || m_ProfileDataView.analysis == null)
                GUI.enabled = false;
            if (GUILayout.Button(Styles.singleFrameTimes))
                SaveFrameTimesCSV();
            GUI.enabled = enabled;

            GUILayout.Label("Comparison View");

            if (m_LeftDataView == null || !m_LeftDataView.IsDataValid() || m_RightDataView == null || !m_RightDataView.IsDataValid())
                GUI.enabled = false;
            if (GUILayout.Button(Styles.comparisonFrameTimes))
                SaveComparisonFrameTimesCSV();
            GUI.enabled = enabled;

            GUILayout.Label("");
            GUILayout.Label("【指定函数批量导出数据】");

            cfgListScrollPos = GUILayout.BeginScrollView(cfgListScrollPos);
            exportFuncNameListObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(listProperty, true);
            if (EditorGUI.EndChangeCheck())
            {
                exportFuncNameListObject.ApplyModifiedProperties();
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Styles.setMarkerCfg))
            {
                SaveMarkerCfg2CSV();
            }
            if (GUILayout.Button(Styles.readMarkerCfg))
            {
                ReadMarkerCfgFromCSV();
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button(Styles.exportCfgMarkerData))
            {
                m_ProfileAnalyzerWindow.GetDataFromMarkerNames(exportFuncNameList);
            }
            GUI.enabled = enabled;

            GUILayout.Label("");
            GUILayout.Label("【导出数据自动生成对比】");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("对比文件名A");
            exportCmpA = EditorGUILayout.TextField(exportCmpA);
            if (GUILayout.Button("选择", GUILayout.Width(40f)))
            {
                exportCmpA = EditorUtility.OpenFilePanel("选取csv性能导出文件", "./AnalyzerDataCSV/", "csv");
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("对比文件名B");
            exportCmpB = EditorGUILayout.TextField(exportCmpB);
            if (GUILayout.Button("选择", GUILayout.Width(40f)))
            {
                exportCmpB = EditorUtility.OpenFilePanel("选取csv性能导出文件", "./AnalyzerDataCSV/", "csv");
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button(Styles.exportCmpData))
            {
                GetCmpData(exportCmpA, exportCmpB);
            }
            GUI.enabled = enabled;
            EditorGUILayout.EndVertical();
        }

        void SaveMarkerTableCSV()
        {
            if (m_ProfileDataView.analysis == null)
                return;

            string path = EditorUtility.SaveFilePanel("Save marker table CSV data", "", "markerTable.csv", "csv");
            if (path.Length != 0)
            {
                var analytic = ProfileAnalyzerAnalytics.BeginAnalytic();
                using (StreamWriter file = new StreamWriter(path))
                {
                    file.Write("Name, ");
                    file.Write("Median Time, Min Time, Max Time, ");
                    file.Write("Median Frame Index, Min Frame Index, Max Frame Index, ");
                    file.Write("Min Depth, Max Depth, ");
                    file.Write("Total Time, ");
                    file.Write("Mean Time, Time Lower Quartile, Time Upper Quartile, ");
                    file.Write("Count Total, Count Median, Count Min, Count Max, ");
                    file.Write("Number of frames containing Marker, ");
                    file.Write("First Frame Index, ");
                    file.Write("Time Min Individual, Time Max Individual, ");
                    file.Write("Min Individual Frame, Max Individual Frame, ");
                    file.WriteLine("Time at Median Frame");

                    List<MarkerData> markerData = m_ProfileDataView.analysis.GetMarkers();
                    markerData.Sort();
                    foreach (MarkerData marker in markerData)
                    {
                        file.Write("{0},", marker.name);
                        file.Write("{0},{1},{2},",
                            marker.msMedian, marker.msMin, marker.msMax);
                        file.Write("{0},{1},{2},",
                            marker.medianFrameIndex, marker.minFrameIndex, marker.maxFrameIndex);
                        file.Write("{0},{1},",
                            marker.minDepth, marker.maxDepth);
                        file.Write("{0},",
                            marker.msTotal);
                        file.Write("{0},{1},{2},",
                            marker.msMean, marker.msLowerQuartile, marker.msUpperQuartile);
                        file.Write("{0},{1},{2},{3},",
                            marker.count, marker.countMedian, marker.countMin, marker.countMax);
                        file.Write("{0},", marker.presentOnFrameCount);
                        file.Write("{0},", marker.firstFrameIndex);
                        file.Write("{0},{1},",
                            marker.msMinIndividual, marker.msMaxIndividual);
                        file.Write("{0},{1},",
                            marker.minIndividualFrameIndex, marker.maxIndividualFrameIndex);
                        file.WriteLine("{0}", marker.msAtMedian);
                    }
                }
                ProfileAnalyzerAnalytics.SendUIButtonEvent(ProfileAnalyzerAnalytics.UIButton.ExportSingleFrames, analytic);
            }
        }

        void SaveFrameTimesCSV()
        {
            if (m_ProfileDataView == null)
                return;
            if (!m_ProfileDataView.IsDataValid())
                return;

            string path = EditorUtility.SaveFilePanel("Save frame time CSV data", "", "frameTime.csv", "csv");
            if (path.Length != 0)
            {
                var analytic = ProfileAnalyzerAnalytics.BeginAnalytic();
                using (StreamWriter file = new StreamWriter(path))
                {
                    file.WriteLine("Frame Offset, Frame Index, Frame Time (ms), Time from first frame (ms)");
                    float maxFrames = m_ProfileDataView.data.GetFrameCount();

                    var frame = m_ProfileDataView.data.GetFrame(0);
                    // msStartTime isn't very accurate so we don't use it

                    double msTimePassed = 0.0;
                    for (int frameOffset = 0; frameOffset < maxFrames; frameOffset++)
                    {
                        frame = m_ProfileDataView.data.GetFrame(frameOffset);
                        int frameIndex = m_ProfileDataView.data.OffsetToDisplayFrame(frameOffset);
                        float msFrame = frame.msFrame;
                        file.WriteLine("{0},{1},{2},{3}",
                            frameOffset, frameIndex, msFrame, msTimePassed);

                        msTimePassed += msFrame;
                    }
                }
                ProfileAnalyzerAnalytics.SendUIButtonEvent(ProfileAnalyzerAnalytics.UIButton.ExportSingleFrames, analytic);
            }
        }

        void SaveComparisonFrameTimesCSV()
        {
            if (m_LeftDataView == null || m_RightDataView == null)
                return;
            if (!m_LeftDataView.IsDataValid() || !m_RightDataView.IsDataValid())
                return;

            string path = EditorUtility.SaveFilePanel("Save comparison frame time CSV data", "", "frameTimeComparison.csv", "csv");
            if (path.Length != 0)
            {
                var analytic = ProfileAnalyzerAnalytics.BeginAnalytic();
                using (StreamWriter file = new StreamWriter(path))
                {
                    file.Write("Frame Offset, ");
                    file.Write("Left Frame Index, Right Frame Index, ");
                    file.Write("Left Frame Time (ms), Left time from first frame (ms), ");
                    file.Write("Right Frame Time (ms), Right time from first frame (ms), ");
                    file.WriteLine("Frame Time Diff (ms)");
                    float maxFrames = Math.Max(m_LeftDataView.data.GetFrameCount(), m_RightDataView.data.GetFrameCount());

                    var leftFrame = m_LeftDataView.data.GetFrame(0);
                    var rightFrame = m_RightDataView.data.GetFrame(0);

                    // msStartTime isn't very accurate so we don't use it

                    double msTimePassedLeft = 0.0;
                    double msTimePassedRight = 0.0;

                    for (int frameOffset = 0; frameOffset < maxFrames; frameOffset++)
                    {
                        leftFrame = m_LeftDataView.data.GetFrame(frameOffset);
                        rightFrame = m_RightDataView.data.GetFrame(frameOffset);
                        int leftFrameIndex = m_LeftDataView.data.OffsetToDisplayFrame(frameOffset);
                        int rightFrameIndex = m_RightDataView.data.OffsetToDisplayFrame(frameOffset);
                        float msFrameLeft = leftFrame != null ? leftFrame.msFrame : 0;
                        float msFrameRight = rightFrame != null ? rightFrame.msFrame : 0;
                        float msFrameDiff = msFrameRight - msFrameLeft;
                        file.Write("{0},", frameOffset);
                        file.Write("{0},{1},", leftFrameIndex, rightFrameIndex);
                        file.Write("{0},{1},", msFrameLeft, msTimePassedLeft);
                        file.Write("{0},{1},", msFrameRight, msTimePassedRight);
                        file.WriteLine("{0}", msFrameDiff);

                        msTimePassedLeft += msFrameLeft;
                        msTimePassedRight += msFrameRight;
                    }
                }
                ProfileAnalyzerAnalytics.SendUIButtonEvent(ProfileAnalyzerAnalytics.UIButton.ExportComparisonFrames, analytic);
            }
        }

        void SaveMarkerCfg2CSV()
        {
            string path = "./AnalyzerDataCSV/";
            string filename = path + "ExportMarkerCfg.csv";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            StreamWriter sw = new StreamWriter(filename, false, System.Text.Encoding.UTF8);
            foreach (string markername in exportFuncNameList)
            {
                sw.WriteLine(markername);
            }
            sw.Close();
        }

        void ReadMarkerCfgFromCSV()
        {
            string filename = "./AnalyzerDataCSV/ExportMarkerCfg.csv";
            StreamReader sr = new StreamReader(filename, System.Text.Encoding.GetEncoding("gb2312"));
            string tmp;
            exportFuncNameList.Clear();
            while (!String.IsNullOrEmpty(tmp = sr.ReadLine()))
            {
                exportFuncNameList.Add(tmp);
            }
            sr.Close();
        }

        class MyCmpData
        {
            public float meanCostPerFrameA = 0f;
            public float meanCostPerFrameB = 0f;
            public float meanCostPerCountA = 0f;
            public float meanCostPerCountB = 0f;
            public float meanTimesPerFrameA = 0f;
            public float meanTimesPerFrameB = 0f;
            public MyCmpData(bool isDataA, float _meanCostPerFrame, float _meanCostPerCount, float _meanTimesPerFrame)
            {
                if (isDataA)
                {
                    meanCostPerFrameA = _meanCostPerFrame;
                    meanCostPerCountA = _meanCostPerCount;
                    meanTimesPerFrameA = _meanTimesPerFrame;
                }
                else
                {
                    meanCostPerFrameB = _meanCostPerFrame;
                    meanCostPerCountB = _meanCostPerCount;
                    meanTimesPerFrameB = _meanTimesPerFrame;
                }
            }

            public float MeanCostPerFrameCmp
            {
                get
                {
                    if (meanCostPerFrameA == 0 || meanCostPerFrameB == 0) return 0;
                    return meanCostPerFrameB - meanCostPerFrameA;
                }
            }

            public float MeanCostPerFrameCmpPercent
            {
                get
                {
                    if (meanCostPerFrameA == 0 || meanCostPerFrameB == 0) return 0;
                    return MeanCostPerFrameCmp / meanCostPerFrameA * 100f;
                }
            }

            public float MeanCostPerCountCmp
            {
                get
                {
                    if (meanCostPerCountA == 0 || meanCostPerCountB == 0) return 0;
                    return meanCostPerCountB - meanCostPerCountA;
                }
            }

            public float MeanCostPerCountCmpPercent
            {
                get
                {
                    if (meanCostPerCountA == 0 || meanCostPerCountB == 0) return 0;
                    return MeanCostPerCountCmp / meanCostPerCountA * 100f;
                }
            }

            public float MeanTimesPerFrameCmp
            {
                get
                {
                    if (meanTimesPerFrameA == 0 || meanTimesPerFrameB == 0) return 0;
                    return meanTimesPerFrameB - meanTimesPerFrameA;
                }
            }

            public float MeanTimesPerFrameCmpPercent
            {
                get
                {
                    if (meanTimesPerFrameA == 0 || meanTimesPerFrameB == 0) return 0;
                    return MeanTimesPerFrameCmp / meanTimesPerFrameA * 100f;
                }
            }
        }
        void GetCmpData(string fileA, string fileB)
        {
            //读两个文件
            StreamReader srA = new StreamReader(fileA, System.Text.Encoding.GetEncoding("gb2312"));
            StreamReader srB = new StreamReader(fileB, System.Text.Encoding.GetEncoding("gb2312"));
            string tmp;
            Dictionary<string, MyCmpData> dataMap = new Dictionary<string, MyCmpData>();
            tmp = srA.ReadLine();
            while (!String.IsNullOrEmpty(tmp = srA.ReadLine()))
            {
                var arr = tmp.Split(',');
                dataMap.Add(arr[0], new MyCmpData(true, float.Parse(arr[2]), float.Parse(arr[3]), float.Parse(arr[6])));
            }
            srA.Close();
            tmp = srB.ReadLine();
            while (!String.IsNullOrEmpty(tmp = srB.ReadLine()))
            {
                var arr = tmp.Split(',');
                if (dataMap.TryGetValue(arr[0], out var data))
                {
                    data.meanCostPerFrameB = float.Parse(arr[2]);
                    data.meanCostPerCountB = float.Parse(arr[3]);
                    data.meanTimesPerFrameB = float.Parse(arr[6]);
                }
                else
                {
                    dataMap.Add(arr[0], new MyCmpData(false, float.Parse(arr[2]), float.Parse(arr[3]), float.Parse(arr[6])));
                }
            }
            srB.Close();

            //生成对比
            string cmpfilename = $"./AnalyzerDataCSV/{DateTime.Now.ToString("yyMMddHHmm")} Compare.csv";
            StreamWriter sw = new StreamWriter(cmpfilename, false, System.Text.Encoding.UTF8);
            sw.WriteLine("函数名,A每帧平均耗时(ms),B每帧平均耗时(ms),每帧平均耗时比较,每帧平均耗时比较百分比,A单次平均耗时(ms),B单次平均耗时(ms),单次平均耗时比较,单次平均耗时比较百分比,A有效帧平均次数,B有效帧平均次数,有效帧平均次数比较,有效帧平均次数比较百分比");
            foreach (var dataKVP in dataMap)
            {
                var data = dataKVP.Value;
                sw.WriteLine($"{dataKVP.Key}," +
                    $"{data.meanCostPerFrameA},{data.meanCostPerFrameB},{data.MeanCostPerFrameCmp},{data.MeanCostPerFrameCmpPercent}%," +
                    $"{data.meanCostPerCountA},{data.meanCostPerCountB},{data.MeanCostPerCountCmp},{data.MeanCostPerCountCmpPercent}%," +
                    $"{data.meanTimesPerFrameA},{data.meanTimesPerFrameB},{data.MeanTimesPerFrameCmp},{data.MeanTimesPerFrameCmpPercent}%");
            }
            sw.Close();
        }
    }
}
