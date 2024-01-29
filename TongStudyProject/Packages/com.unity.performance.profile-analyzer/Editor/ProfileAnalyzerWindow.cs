using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Text;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;
using System.Reflection;
using UnityEngine.Profiling;

namespace UnityEditor.Performance.ProfileAnalyzer
{
    enum ThreadRange
    {
        Median,
        UpperQuartile,
        Max
    };

    enum ActiveTab
    {
        Summary,
        Compare,
    };

    enum ThreadActivity
    {
        None,
        Analyze,
        AnalyzeDone,
        Compare,
        CompareDone,
    };

    enum TopTenDisplay
    {
        Normalized,
        LongestTime,
    };

    enum NameFilterOperation
    {
        All,    // AND
        Any,    // OR
    };

    public class AnalyzerFunc
    {
        public string funcName;
        public string tag;
    }

    /// <summary>
    /// Main profile Analyzer UI window
    /// </summary>
    public class ProfileAnalyzerWindow : EditorWindow
    {
        internal static class Styles
        {
            public static readonly GUIContent emptyString = new GUIContent("", "");
            public static readonly GUIContent dash = new GUIContent("-", "");
            public static readonly GUIContent thread = new GUIContent("Thread", "");
            public static readonly GUIContent noThread = new GUIContent("", "Thread not present on this data set");

            public static readonly GUIContent max = new GUIContent("Max", "The peak value in the data set");
            public static readonly GUIContent upperQuartile = new GUIContent("Upper Quartile", "The middle value between the median and the highest value of the data set. I.e. at 75% of the ordered data.");
            public static readonly GUIContent mean = new GUIContent("Mean", "The average value in the data set");
            public static readonly GUIContent median = new GUIContent("Median", "The central value in the data set");
            public static readonly GUIContent lowerQuartile = new GUIContent("Lower Quartile", "The middle number between the smallest number and the median of the data set. I.e. at 25% of the ordered data.");
            public static readonly GUIContent min = new GUIContent("Min", "The minimum value in the data set");
            public static readonly GUIContent individualMin = new GUIContent("Individual Min", "The minimum value in the data set for an individual marker instance (not the total in the frame)");
            public static readonly GUIContent individualMax = new GUIContent("Individual Max", "The maximum value in the data set for an individual marker instance (not the total in the frame)");

            public static readonly GUIContent export = new GUIContent("Export", "Export profiler data as CSV files");
            public static readonly GUIContent exportFunc = new GUIContent("ExportFunc", "Export MSGameFunc as CSV files");
            public static readonly GUIContent pullOpen = new GUIContent("Pull Data", "Pull data from Unity profiler.\nFirst you must open Unity profiler to pull data from it");
            public static readonly GUIContent pullRange = new GUIContent("Pull Data", "Pull data from Unity profiler.\nFirst you must use the Unity profiler to capture data from application");
            public static readonly GUIContent pullRecording = new GUIContent("Pull Data", "Pull data from Unity profiler.\nStop Unity profiler recording to enable pulling data");
            public static readonly GUIContent pull = new GUIContent("Pull Data", "Pull data from Unity profiler");
            public static readonly GUIContent nameFilter = new GUIContent("Name Filter : ", "Only show markers containing the strings");
            public static readonly GUIContent nameExclude = new GUIContent("Exclude Names : ", "Excludes markers containing the strings");
            public static readonly GUIContent threadFilter = new GUIContent("Thread : ", "Select threads to focus on");
            public static readonly GUIContent threadFilterSelect = new GUIContent("Select", "Select threads to focus on");
            public static readonly GUIContent unitFilter = new GUIContent("Units : ", "Units to show in UI");
            public static readonly GUIContent timingFilter = new GUIContent("Analysis Type : ", TimingOptions.Tooltip);
            public static readonly GUIContent markerColumns = new GUIContent("Marker Columns : ");
            public static readonly GUIContent graphPairing = new GUIContent("Pair Graph Selection", "Selections on one graph will affect the other");

            public static readonly GUIContent frameSummary = new GUIContent("Frame Summary", "");
            public static readonly GUIContent frameCount = new GUIContent("Frame Count", "Frame Count");
            public static readonly GUIContent frameStart = new GUIContent("Start", "Frame Start");
            public static readonly GUIContent frameEnd = new GUIContent("End", "Frame End");
            public static readonly GUIContent threadSummary = new GUIContent("Thread Summary", "");
            public static readonly GUIContent markerSummary = new GUIContent("Marker Summary", "");
            public static readonly GUIContent filters = new GUIContent("Filters", "");
            public static readonly GUIContent profileTable = new GUIContent("Marker Details for currently selected range", "");
            public static readonly GUIContent comparisonTable = new GUIContent("Marker Comparison for currently selected range", "");

            public static readonly GUIContent depthTitle = new GUIContent("Depth Slice : ", "Marker callstack depth to analyze");
            public static readonly GUIContent leftDepthTitle = new GUIContent("Left : ", "Marker callstack depth to analyze");
            public static readonly GUIContent rightDepthTitle = new GUIContent(" Right : ", "Marker callstack depth to analyze");
            public static readonly GUIContent parentMarker = new GUIContent("Parent Marker : ", "Marker to start analysis from. Parent of the hierarchy to analyze.");
            public static readonly GUIContent selectParentMarker = new GUIContent("None", "Select using right click context menu on marker names in marker table");

            public static readonly GUIContent topMarkerRatio = new GUIContent("Ratio : ", "Normalize\tNormalized to time of the individual set\nLongest\tRatio based on longest time of the two");

            public static readonly GUIContent firstFrame = new GUIContent("First frame", "");

            public static readonly GUIContent[] topTenDisplayOptions = {
                new GUIContent("Normalized", "Ratio normalized to time of the individual data set"),
                new GUIContent("Longest", "Ratio based on longest time of the two data sets")
            };

            public static readonly GUIContent[] nameFilterOperation = {
                new GUIContent("All", "Marker name contains all strings"),
                new GUIContent("Any", "Marker name contains any of the strings")
            };

            public static readonly GUIContent menuItemSelectFramesInAll = new GUIContent("Select Frames that contain this marker (within whole data set)", "");
            public static readonly GUIContent menuItemSelectFramesInCurrent = new GUIContent("Select Frames that contain this marker (within current selection)", "");
            public static readonly GUIContent menuItemSelectFramesAll = new GUIContent("Clear Selection", "");

            public static readonly GUIContent frameCosts = new GUIContent(" by frame costs", "Contains accumulated marker cost within the frame");
            public static readonly GUIContent dataMissing = new GUIContent("Pull or load a data set for analysis", "Pull data from Unity Profiler or load a previously saved analysis data set");
            public static readonly GUIContent comparisonDataMissing = new GUIContent("Pull or load a data set for comparison", "Pull data from Unity Profiler or load previously saved analysis data sets");

            public static readonly string topMarkersTooltip = "Top markers for the median frame.\nThe length of this frame is the median of those in the data set.\nIt is likely to be the most representative frame.";
            public static readonly string medianFrameTooltip = "The length of this frame is the median of those in the data set.\nIt is likely to be the most representative frame.";

            public static readonly string helpText =
@"This tool can analyze Unity Profiler data, to find representative frames and perform comparisons of data sets.

To gather data to analyze:
* Open the Unity Profiler. Either via the Unity menu under 'Windows', 'Analysis' or via the 'Open Profile Window' in the tool bar.
* Capture some profiling data in the Unity Profiler by selecting a target application and click the 'Record' button.
* Stop the capture by clicking again on the 'Record' button.

To analyze the data:
* Pull the Unity Profiler data into this tool by clicking the 'Pull Data' button in the single or compare views.
* The analysis will be automatically triggered (in the compare view two data sets are required before analysis is performed).
* Select a marker to see more detailed information about its time utilization over the frame time range.
* Save off a data file from here to keep for future use. (Recommend saving the profile .data file in the same folder).

To compare two data sets:
* Click the compare tab. The data in the single tab will be used by default. You can also load previously saved analysis data.
* Drag select a region in the frame time graph (above) to choose 1 or more frames for each of the two data sets.
* The comparison will be automatically triggered as the selection is made.";
        }


        ProgressBarDisplay m_ProgressBar;
        ProfileAnalyzer m_ProfileAnalyzer;

        ProfilerWindowInterface m_ProfilerWindowInterface;
        string m_LastProfilerSelectedMarker;

        int m_TopNumber;
        string[] m_TopStrings;
        int[] m_TopValues;
        string[] m_DepthStrings;
        int[] m_DepthValues;
        string[] m_DepthStrings1;
        int[] m_DepthValues1;
        string[] m_DepthStrings2;
        int[] m_DepthValues2;

        [SerializeField]
        int m_DepthFilter = ProfileAnalyzer.kDepthAll;
        [SerializeField]
        int m_DepthFilter1 = ProfileAnalyzer.kDepthAll;
        [SerializeField]
        int m_DepthFilter2 = ProfileAnalyzer.kDepthAll;
        [SerializeField]
        bool m_DepthFilter2Auto = true;
        [SerializeField]
        TimingOptions.TimingOption m_TimingOption = TimingOptions.TimingOption.Time;

        [SerializeField]
        string m_ParentMarker = null;

        List<string> m_ThreadUINames = new List<string>();
        List<string> m_ThreadNames = new List<string>();
        Dictionary<string, string> m_ThreadNameToUIName;

        [SerializeField]
        ThreadSelection m_ThreadSelection = new ThreadSelection();
        ThreadSelection m_ThreadSelectionNew;
        string m_ThreadSelectionSummary;

        [SerializeField]
        DisplayUnits m_DisplayUnits = new DisplayUnits(Units.Milliseconds);
        string[] m_UnitNames;

        [SerializeField]
        string m_NameFilter = "";
        [SerializeField]
        string m_NameExclude = "";

        [SerializeField]
        MarkerColumnFilter m_SingleModeFilter = new MarkerColumnFilter(MarkerColumnFilter.Mode.TimeAndCount);
        [SerializeField]
        MarkerColumnFilter m_CompareModeFilter = new MarkerColumnFilter(MarkerColumnFilter.Mode.TimeAndCount);

        [SerializeField]
        TopTenDisplay m_TopTenDisplay = TopTenDisplay.Normalized;
        [SerializeField]
        NameFilterOperation m_NameFilterOperation = NameFilterOperation.All;
        [SerializeField]
        NameFilterOperation m_NameExcludeOperation = NameFilterOperation.Any;

        int m_ProfilerFirstFrameIndex = 0;
        int m_ProfilerLastFrameIndex = 0;
        int m_PullFirstFrameindex = 0;
        int m_PullLastFrameindex = 0;

        ActiveTab m_NextActiveTab = ActiveTab.Summary;
        ActiveTab m_ActiveTab = ActiveTab.Summary;
        bool m_OtherTabDirty = false;
        bool m_OtherTableDirty = false;

        [SerializeField]
        bool m_ShowFilters = true;
        [SerializeField]
        bool m_ShowTopNMarkers = true;
        [SerializeField]
        bool m_ShowFrameSummary = true;
        [SerializeField]
        bool m_ShowThreadSummary = false;
        [SerializeField]
        bool m_ShowMarkerSummary = true;
        [SerializeField]
        bool m_ShowMarkerTable = true;

        internal static class UIColor
        {
            static internal Color Color256(int r, int g, int b, int a)
            {
                return new Color((float)r / 255.0f, (float)g / 255.0f, (float)b / 255.0f, (float)a / 255.0f);
            }

            public static readonly Color white = new UnityEngine.Color(1.0f, 1.0f, 1.0f);
            public static readonly Color barBackground = new Color(0.5f, 0.5f, 0.5f);
            public static readonly Color barBackgroundSelected = new Color(0.6f, 0.6f, 0.6f);
            public static readonly Color boxAndWhiskerBoxColor = Color256(112, 112, 112, 255);
            public static readonly Color boxAndWhiskerLineColorLeft = Color256(206, 219, 238, 255);
            public static readonly Color boxAndWhiskerBoxColorLeft = Color256(59, 104, 144, 255);
            public static readonly Color boxAndWhiskerLineColorRight = Color256(247, 212, 201, 255);
            public static readonly Color boxAndWhiskerBoxColorRight = Color256(161, 83, 30, 255);
            public static readonly Color bar = new Color(0.95f, 0.95f, 0.95f);
            public static readonly Color barSelected = new Color(0.5f, 1.0f, 0.5f);
            public static readonly Color standardLine = new Color(1.0f, 1.0f, 1.0f);
            public static readonly Color gridLines = new Color(0.4f, 0.4f, 0.4f);

            public static readonly Color left = Color256(111, 163, 216, 255);
            public static readonly Color leftSelected = Color256(06, 219, 238, 255);
            public static readonly Color right = Color256(238, 134, 84, 255);
            public static readonly Color rightSelected = Color256(247, 212, 201, 255);
            public static readonly Color both = Color256(175, 150, 150, 255);
            public static readonly Color textTopMarkers = Color256(0, 0, 0, 255);
            public static readonly Color marker = new Color(0.0f, 0.5f, 0.5f);
            public static readonly Color markerSelected = new Color(0.0f, 0.6f, 0.6f);
            public static readonly Color thread = new Color(0.5f, 0.0f, 0.5f);
            public static readonly Color threadSelected = new Color(0.6f, 0.0f, 0.6f);
        }

        [SerializeField]
        ProfileDataView m_ProfileSingleView;
        [SerializeField]
        ProfileDataView m_ProfileLeftView;
        [SerializeField]
        ProfileDataView m_ProfileRightView;

        [SerializeField]
        int m_SelectedMarker = 0;
        [SerializeField]
        string m_SelectedMarkerName;

        FrameTimeGraphGlobalSettings m_FrameTimeGraphGlobalSettings;
        FrameTimeGraph m_FrameTimeGraph;
        FrameTimeGraph m_LeftFrameTimeGraph;
        FrameTimeGraph m_RightFrameTimeGraph;
        bool m_FrameTimeGraphsPaired = true;

        TopMarkers m_TopMarkers;
        TopMarkers m_TopMarkersLeft;
        TopMarkers m_TopMarkersRight;


        List<MarkerPairing> m_PairingsNew;
        int m_TotalCombinedMarkerCountNew;

        [SerializeField]
        List<MarkerPairing> m_Pairings = new List<MarkerPairing>();
        int m_TotalCombinedMarkerCount = 0;

        [SerializeField]
        int m_SelectedPairing = 0;

        [SerializeField]
        int m_DepthDiff = 0;

        [SerializeField]
        TreeViewState m_ProfileTreeViewState;
        [SerializeField]
        MultiColumnHeaderState m_ProfileMulticolumnHeaderState;
        ProfileTable m_ProfileTable;

        [SerializeField]
        TreeViewState m_ComparisonTreeViewState;
        [SerializeField]
        MultiColumnHeaderState m_ComparisonMulticolumnHeaderState;
        ComparisonTable m_ComparisonTable;

        internal static class LayoutSize
        {
            public static readonly int WidthColumn0 = 100;
            public static readonly int WidthColumn1 = 52;       // +2 to prevent some 
            public static readonly int WidthColumn2 = 52;
            public static readonly int WidthColumn3 = 52;
            public static readonly int WidthRHS = 290;        // Column widths + label padding between (276) + scrollbar width
            public static readonly int FilterOptionsLeftLabelWidth = 100;
            public static readonly int FilterOptionsEnumWidth = 50;
            public static readonly int FilterOptionsRightLabelWidth = 110;
            public static readonly int FilterOptionsRightEnumWidth = 150;
            public static readonly int HistogramWidth = 150;

            public static readonly int MinWindowWidth = 800 + WidthRHS;
            public static readonly int MinWindowHeight = 480;

            public static readonly int WindowWidth = MinWindowWidth;
            public static readonly int WindowHeight = 840;
        }

        Columns m_Columns = new Columns(LayoutSize.WidthColumn0, LayoutSize.WidthColumn1, LayoutSize.WidthColumn2, LayoutSize.WidthColumn3);

        [SerializeField]
        ThreadRange m_ThreadRange = ThreadRange.UpperQuartile;

        string[] m_ThreadRanges = { "Median frame time", "Upper quartile of frame time", "Max frame time" };

        internal Draw2D m_2D;

        bool m_Async = true;
        Thread m_BackgroundThread;
        ThreadActivity m_ThreadActivity;
        int m_ThreadPhase;
        int m_ThreadPhases;
        int m_ThreadProgress;

        bool m_RequestRepaint;
        bool m_RequestAnalysis;
        bool m_RequestCompare;
        bool m_FullAnalysisRequired;
        bool m_FullCompareRequired;

        [SerializeField]
        int m_TopNBars = 10;

        bool m_EnableAnalysisProfiling = false;
        int m_AnalyzeInUpdatePhase = 0;

        string m_LastAnalysisTime = "";
        string m_LastCompareTime = "";
        float m_LastAnalysisTimeMilliseconds;
        float m_LastCompareTimeMilliseconds;
        bool m_NewDataLoaded = false;
        bool m_NewComparisonDataLoaded = false;

        Vector2 m_HelpScroll = new Vector2(0, 0);
        Vector2 m_ThreadScroll = new Vector2(0, 0);
        Vector2 m_MarkerSummaryScroll = new Vector2(0, 0);

        Rect m_ThreadsAreaRect = new Rect();
        Rect m_ComparisonThreadsAreaRect = new Rect();

        Vector2 m_LastScreenSize = new Vector2(0, 0);
        bool m_ScreenSizeChanged;
        double m_ScreenSizeChangedTimeStarted;
        double m_ScreenSizeChangedTimeFinished;
        ActiveTab m_ScreenSizeChangedTab;

        GUIStyle m_StyleMiddleRight;
        GUIStyle m_StyleUpperLeft;
        bool m_StylesSetup = false;

        static Regex quotedStringWithoutQuotes = new Regex("\"([^\"]*)\"");
        static Regex quotedString = new Regex("(\"[^\"]*\")");
        static Regex stringWithoutWhiteSpace = new Regex("([^ \t]+)");
        /*
                static Regex lastSpace = new Regex("(.+)[ ]([^ ]*)");
        */

#if UNITY_2018_1_OR_NEWER
        [MenuItem("Window/Analysis/Profile Analyzer")]
#else
        [MenuItem("Window/Profile Analyzer")]
#endif
        static void Init()
        {
            var window = GetWindow<ProfileAnalyzerWindow>("Profile Analyzer");
            window.minSize = new Vector2(LayoutSize.MinWindowWidth, LayoutSize.MinWindowHeight);
            window.position.size.Set(LayoutSize.WindowWidth, LayoutSize.WindowHeight);
            window.Show();
            window.m_LastScreenSize = window.position.size;
        }

        /// <summary>
        /// Open profile analyzer window
        /// </summary>
        public static void OpenProfileAnalyzer()
        {
            Init();
        }

        void Awake()
        {
            m_ScreenSizeChanged = false;
            m_ScreenSizeChangedTimeStarted = 0.0;
            m_ScreenSizeChangedTimeFinished = 0.0;
            m_ScreenSizeChangedTab = ActiveTab.Summary;

            m_ProfileSingleView = new ProfileDataView();
            m_ProfileLeftView = new ProfileDataView();
            m_ProfileRightView = new ProfileDataView();

            m_RequestRepaint = false;
            m_RequestAnalysis = false;
            m_RequestCompare = false;

            m_FrameTimeGraphGlobalSettings = new FrameTimeGraphGlobalSettings();
        }

        bool IsSingleMode()
        {
            return m_ActiveTab == ActiveTab.Summary;
        }

        void UpdateGraphInfo(ThreadSelection selection)
        {
            if (selection != null && !selection.empty)
            {
                if (IsSingleMode())
                {
                    if (selection.IsHadThread("Main"))
                    {
                        m_FrameTimeGraph.SetData(GetFrameTimeData(m_ProfileSingleView.data));
                    }
                    else
                    {
                        m_FrameTimeGraph.SetData(GetThreadFrameTimeData(m_ProfileSingleView.data, selection.selection[0]));
                    }
                }
                else
                {
                    if (selection.IsHadThread("Main"))
                    {
                        m_LeftFrameTimeGraph.SetData(GetFrameTimeData(m_ProfileLeftView.data));
                        m_RightFrameTimeGraph.SetData(GetFrameTimeData(m_ProfileRightView.data));
                    }
                    else
                    {
                        m_LeftFrameTimeGraph.SetData(GetThreadFrameTimeData(m_ProfileLeftView.data, selection.selection[0]));
                        m_RightFrameTimeGraph.SetData(GetThreadFrameTimeData(m_ProfileRightView.data, selection.selection[0]));
                    }
                }
            }
        }

        void OnEnable()
        {
            // Update styles so we get the theme changes
            m_StylesSetup = false;

            ProfileAnalyzerAnalytics.EnableAnalytics();

            m_ProgressBar = new ProgressBarDisplay();

            m_ProfilerWindowInterface = new ProfilerWindowInterface(m_ProgressBar);
            if (!m_ProfilerWindowInterface.IsReady())
            {
                m_ProfilerWindowInterface.GetProfilerWindowHandle();
            }

            m_ProfileAnalyzer = new ProfileAnalyzer();

            ThreadIdentifier mainThreadSelection = new ThreadIdentifier("Main Thread", 1);
            m_ThreadSelection.Set(mainThreadSelection.threadNameWithIndex);

            m_2D = new Draw2D("Unlit/ProfileAnalyzerShader");
            FrameTimeGraph.SetGlobalSettings(m_FrameTimeGraphGlobalSettings);
            m_FrameTimeGraph = new FrameTimeGraph(0, m_2D, m_DisplayUnits.Units, UIColor.barBackground, UIColor.barBackgroundSelected, UIColor.bar, UIColor.barSelected, UIColor.marker, UIColor.markerSelected, UIColor.thread, UIColor.threadSelected, UIColor.gridLines);
            m_FrameTimeGraph.SetRangeCallback(SetRange);
            m_FrameTimeGraph.SetActiveCallback(GraphActive);
            m_LeftFrameTimeGraph = new FrameTimeGraph(1, m_2D, m_DisplayUnits.Units, UIColor.barBackground, UIColor.barBackgroundSelected, UIColor.left, UIColor.leftSelected, UIColor.marker, UIColor.markerSelected, UIColor.thread, UIColor.threadSelected, UIColor.gridLines);
            m_LeftFrameTimeGraph.SetRangeCallback(SetLeftRange);
            m_LeftFrameTimeGraph.SetActiveCallback(GraphActive);
            m_RightFrameTimeGraph = new FrameTimeGraph(2, m_2D, m_DisplayUnits.Units, UIColor.barBackground, UIColor.barBackgroundSelected, UIColor.right, UIColor.rightSelected, UIColor.marker, UIColor.markerSelected, UIColor.thread, UIColor.threadSelected, UIColor.gridLines);
            m_RightFrameTimeGraph.SetRangeCallback(SetRightRange);
            m_RightFrameTimeGraph.SetActiveCallback(GraphActive);
            m_LeftFrameTimeGraph.PairWith(m_FrameTimeGraphsPaired ? m_RightFrameTimeGraph : null);

            m_TopMarkers = new TopMarkers(this, m_2D, UIColor.barBackground, UIColor.textTopMarkers);
            m_TopMarkersLeft = new TopMarkers(this, m_2D, UIColor.barBackground, UIColor.textTopMarkers);
            m_TopMarkersRight = new TopMarkers(this, m_2D, UIColor.barBackground, UIColor.textTopMarkers);

            m_ThreadActivity = ThreadActivity.None;
            m_ThreadProgress = 0;
            m_ThreadPhase = 0;

            List<int> values = new List<int>();
            List<String> strings = new List<string>();
            for (int i = 1; i <= 15; i++)
            {
                values.Add(i);
                strings.Add(i.ToString());
            }
            m_TopValues = values.ToArray();
            m_TopStrings = strings.ToArray();
            m_TopNumber = 3;

            List<string> unitNames = new List<string>(DisplayUnits.UnitNames);
            unitNames.RemoveAt(unitNames.Count - 1);
            m_UnitNames = unitNames.ToArray();

            // Regrenerate analysis if just re initialised with the existing profile data reloaded from serialisation (e.g. on enter play mode)
            // As we don't serialise the analysis itself.
            // UpdateActiveTab(true);

            UpdateThreadNames();

            if (m_ProfileSingleView.analysis != null)
            {
                CreateProfileTable();
                m_RequestRepaint = true;
            }

            if (m_ProfileLeftView.analysis != null && m_ProfileRightView.analysis != null)
            {
                CreateComparisonTable();
                m_RequestRepaint = true;
            }

            // Mouse movement calls OnGui
            wantsMouseMove = true;
        }

        void OnDisable()
        {
            if (ProfileAnalyzerExportWindow.IsOpen())
                ProfileAnalyzerExportWindow.CloseAll();
        }

        bool DisplayCount()
        {
            switch (m_SingleModeFilter.mode)
            {
                case MarkerColumnFilter.Mode.CountTotals:
                case MarkerColumnFilter.Mode.CountPerFrame:
                    return true;
                default:
                    return false;
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.MouseMove)
            {
                m_2D.OnGUI();

                Draw();
            }

            ProcessInput();
        }

        void SetView(ProfileDataView dst, ProfileData data, string path, FrameTimeGraph graph)
        {
            if (!data.IsSame(dst.data))
            {
                if (dst == m_ProfileSingleView)
                    m_NewDataLoaded = true;
                else
                    m_NewComparisonDataLoaded = true;
            }

            dst.data = data;
            dst.path = path;
            dst.SelectFullRange();

            graph.Reset();
            graph.SetData(GetFrameTimeData(dst.data));

            // One of the views changed so make sure the export window knows if its open
            ProfileAnalyzerExportWindow exportWindow = ProfileAnalyzerExportWindow.FindOpenWindow();
            if (exportWindow != null)
            {
                exportWindow.SetData(m_ProfileSingleView, m_ProfileLeftView, m_ProfileRightView, this);
            }
        }

        void SetView(ProfileDataView dst, ProfileDataView src, FrameTimeGraph graph)
        {
            SetView(dst, src.data, src.path, graph);
        }

        void UpdateThreadNames()
        {
            // Update threads list
            switch (m_ActiveTab)
            {
                case ActiveTab.Summary:
                    GetThreadNames(m_ProfileSingleView.data, out m_ThreadUINames, out m_ThreadNames, out m_ThreadNameToUIName);
                    break;
                case ActiveTab.Compare:
                    GetThreadNames(m_ProfileLeftView.data, m_ProfileRightView.data, out m_ThreadUINames, out m_ThreadNames, out m_ThreadNameToUIName);
                    break;
            }

            UpdateThreadGroupSelection(m_ThreadNames, m_ThreadSelection);
            m_ThreadSelectionSummary = CalculateSelectedThreadsSummary();
        }

        void ProcessTabSwitch()
        {
            if (m_NextActiveTab != m_ActiveTab)
            {
                m_ActiveTab = m_NextActiveTab;

                // Copy data if none present for this tab
                switch (m_ActiveTab)
                {
                    case ActiveTab.Summary:
                        if (!m_ProfileSingleView.IsDataValid())
                        {
                            if (m_ProfileLeftView.IsDataValid())
                            {
                                SetView(m_ProfileSingleView, m_ProfileLeftView, m_FrameTimeGraph);

                                m_RequestAnalysis = true;
                                m_FullAnalysisRequired = true;
                            }
                            else if (m_ProfileRightView.IsDataValid())
                            {
                                SetView(m_ProfileSingleView, m_ProfileRightView, m_FrameTimeGraph);

                                m_RequestAnalysis = true;
                                m_FullAnalysisRequired = true;
                            }
                        }
                        break;
                    case ActiveTab.Compare:
                        if ((!m_ProfileLeftView.IsDataValid() || !m_ProfileRightView.IsDataValid()) && m_ProfileSingleView.IsDataValid())
                        {
                            if (!m_ProfileLeftView.IsDataValid())
                            {
                                SetView(m_ProfileLeftView, m_ProfileSingleView, m_LeftFrameTimeGraph);
                            }

                            if (!m_ProfileRightView.IsDataValid())
                            {
                                SetView(m_ProfileRightView, m_ProfileSingleView, m_RightFrameTimeGraph);
                            }

                            // Remove pairing of both left/right point at the same data
                            if (m_ProfileLeftView.path == m_ProfileRightView.path)
                            {
                                SetFrameTimeGraphPairing(false);
                            }

                            m_RequestCompare = true;
                            m_FullCompareRequired = true;
                        }
                        break;
                }

                UpdateThreadNames();

                if (!m_OtherTableDirty)
                    SelectMarker(m_SelectedMarkerName);

                if (m_OtherTabDirty)
                {
                    UpdateActiveTab(true, false);  // Make sure any depth/thread updates are applied when switching tabs, but don't dirty the other tab
                    m_OtherTabDirty = false;
                }

                if (m_OtherTableDirty)
                {
                    UpdateMarkerTable(false);  // Make sure any marker selection updates are applied when switching tabs, but don't dirty the other tab
                    m_OtherTableDirty = false;
                }
            }
        }

        bool IsDocked()
        {
            BindingFlags fullBinding = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            MethodInfo isDockedMethod = typeof(EditorWindow).GetProperty("docked", fullBinding).GetGetMethod(true);
            bool isDocked = (bool)isDockedMethod.Invoke(this, null);

            return isDocked;
        }

        void CheckScreenSizeChanges()
        {
            // We get a 5 pixel change in y height during initialization.
            // We could wait before considering size changes but using a delta is also useful
            float sizeDeltaForChange = 10;

            Vector2 sizeDiff = position.size - m_LastScreenSize;
            if (Math.Abs(sizeDiff.x) > sizeDeltaForChange || Math.Abs(sizeDiff.y) > sizeDeltaForChange)
            {
                if (m_LastScreenSize.x != 0) // At initialization time the screen size has not yet been recorded. Don't consider this a screen size change
                {
                    m_LastScreenSize = position.size;
                    if (!m_ScreenSizeChanged)
                    {
                        // Record when we started the change
                        m_ScreenSizeChanged = true;
                        m_ScreenSizeChangedTimeStarted = EditorApplication.timeSinceStartup;
                    }
                    // Record the last time of a change
                    m_ScreenSizeChangedTimeFinished = EditorApplication.timeSinceStartup;

                    // Record which tab we were on when it was changed
                    m_ScreenSizeChangedTab = m_ActiveTab;
                }
            }

            if (m_ScreenSizeChanged)
            {
                double secondsSinceChanged = (EditorApplication.timeSinceStartup - m_ScreenSizeChangedTimeFinished);
                double secondsToDelay = 3f;
                if (secondsSinceChanged > secondsToDelay)
                {
                    // Send analytic 
                    var uiResizeView = m_ScreenSizeChangedTab == ActiveTab.Summary ? ProfileAnalyzerAnalytics.UIResizeView.Single : ProfileAnalyzerAnalytics.UIResizeView.Comparison;
                    float durationInSeconds = (float)(m_ScreenSizeChangedTimeFinished - m_ScreenSizeChangedTimeStarted);
                    ProfileAnalyzerAnalytics.SendUIResizeEvent(uiResizeView, durationInSeconds, position.size.x, position.size.y, IsDocked());

                    m_ScreenSizeChanged = false;
                }
            }
        }

        internal void RequestRepaint()
        {
            m_RequestRepaint = true;
        }

        void ProcessInput()
        {
            FrameTimeGraph.State inputStatus = FrameTimeGraph.State.None;

            if (m_ActiveTab == ActiveTab.Summary)
            {
                inputStatus = m_FrameTimeGraph.ProcessInput();
            }
            else if (m_ActiveTab == ActiveTab.Compare)
            {
                if (m_ProfileLeftView.IsDataValid() && inputStatus == FrameTimeGraph.State.None)
                    inputStatus = m_LeftFrameTimeGraph.ProcessInput();

                if (m_ProfileRightView.IsDataValid() && inputStatus == FrameTimeGraph.State.None)
                    inputStatus = m_RightFrameTimeGraph.ProcessInput();
            }

            switch (inputStatus)
            {
                case FrameTimeGraph.State.Dragging:
                    m_RequestRepaint = true;
                    break;
                case FrameTimeGraph.State.DragComplete:
                    m_RequestCompare = true;
                    break;
            }

            if (Event.current.isKey && Event.current.type == EventType.KeyDown)
            {
                switch (Event.current.keyCode)
                {
                    case KeyCode.Alpha1:
                        if (m_ActiveTab == ActiveTab.Summary)
                        {
                            m_FrameTimeGraph.MakeGraphActive(true);
                            GUI.FocusControl("FrameTimeGraph");
                        }
                        else if (m_ActiveTab == ActiveTab.Compare)
                        {
                            m_LeftFrameTimeGraph.MakeGraphActive(true);
                            GUI.FocusControl("LeftFrameTimeGraph");
                        }

                        m_RequestRepaint = true;
                        break;
                    case KeyCode.Alpha2:
                        if (m_ActiveTab == ActiveTab.Compare)
                        {
                            m_RightFrameTimeGraph.MakeGraphActive(true);
                            GUI.FocusControl("RightFrameTimeGraph");
                        }
                        m_RequestRepaint = true;
                        break;
                }
            }
        }

        void Update()
        {
            CheckScreenSizeChanges();

            // Check if profiler is open
            if (m_ProfilerWindowInterface.IsReady())
            {
                // Check if the selected marker in the profiler has changed
                var selectedMarker = m_ProfilerWindowInterface.GetProfilerWindowMarkerName();
                // Can be "" if in play mode and no active timeline shown
                if (selectedMarker != null && selectedMarker != m_LastProfilerSelectedMarker && selectedMarker != "")
                {
                    m_LastProfilerSelectedMarker = selectedMarker;
                    SelectMarker(selectedMarker);
                }

                // Check if a new profile has been recorded (or loaded) by checking the frame index range.
                int first;
                int last;
                m_ProfilerWindowInterface.GetFrameRangeFromProfiler(out first, out last);
                if (first != m_ProfilerFirstFrameIndex || last != m_ProfilerLastFrameIndex)
                {
                    // Store the updated range and alter the pull range
                    m_ProfilerFirstFrameIndex = first;
                    m_ProfilerLastFrameIndex = last;
                    m_PullFirstFrameindex = m_ProfilerFirstFrameIndex;
                    m_PullLastFrameindex = m_ProfilerLastFrameIndex;
                }
            }
            else
            {
                m_ProfilerWindowInterface.GetProfilerWindowHandle();
            }

            // Deferred to here so drawing isn't messed up by changing tab half way through a function rendering the old tab
            ProcessTabSwitch();

            // Force repaint for the progress bar
            if (IsAnalysisRunning())
            {
                int progress = m_ProfileAnalyzer.GetProgress();
                if (m_ThreadPhases > 1)
                {
                    progress = ((100 * m_ThreadPhase) + progress) / m_ThreadPhases;
                }


                if (m_ThreadProgress != progress)
                {
                    m_ThreadProgress = progress;
                    m_RequestRepaint = true;
                }
            }

            if (m_ThreadSelectionNew != null)
            {
                m_ThreadSelection = new ThreadSelection(m_ThreadSelectionNew);
                m_ThreadSelectionNew = null;
                m_ThreadSelectionSummary = CalculateSelectedThreadsSummary();
                UpdateGraphInfo(m_ThreadSelection);

            }

            switch (m_ThreadActivity)
            {
                case ThreadActivity.AnalyzeDone:
                    // Create table when analysis complete
                    UpdateAnalysisFromAsyncProcessing(m_ProfileSingleView, m_FullAnalysisRequired);
                    m_FullAnalysisRequired = false;

                    UpdateThreadNames();

                    if (m_ProfileSingleView.analysis != null)
                    {
                        CreateProfileTable();
                        m_RequestRepaint = true;
                    }
                    m_ThreadActivity = ThreadActivity.None;

                    if (m_NewDataLoaded)
                    {
                        if (m_ProfileSingleView.IsDataValid())
                        {
                            // Don't bother sending an analytic if the data set is empty (should never occur anyway but consistent with comparison flow)
                            ProfileAnalyzerAnalytics.SendUIUsageModeEvent(ProfileAnalyzerAnalytics.UIUsageMode.Single, m_LastAnalysisTimeMilliseconds / 1000f);
                        }
                        m_NewDataLoaded = false;
                    }

                    SelectMarker(m_SelectedMarkerName);
                    break;

                case ThreadActivity.CompareDone:
                    UpdateAnalysisFromAsyncProcessing(m_ProfileLeftView, m_FullCompareRequired);
                    UpdateAnalysisFromAsyncProcessing(m_ProfileRightView, m_FullCompareRequired);
                    m_FullCompareRequired = false;
                    m_Pairings = m_PairingsNew;
                    m_TotalCombinedMarkerCount = m_TotalCombinedMarkerCountNew;

                    UpdateThreadNames();

                    if (m_ProfileLeftView.analysis != null && m_ProfileRightView.analysis != null)
                    {
                        CreateComparisonTable();
                        m_RequestRepaint = true;
                    }
                    m_ThreadActivity = ThreadActivity.None;

                    if (m_NewComparisonDataLoaded)
                    {
                        if (m_ProfileLeftView.IsDataValid() && m_ProfileRightView.IsDataValid())
                        {
                            // Don't bother sending an analytic when one (or more) of the data sets is blank (as no comparison is really made)
                            ProfileAnalyzerAnalytics.SendUIUsageModeEvent(ProfileAnalyzerAnalytics.UIUsageMode.Comparison, m_LastCompareTimeMilliseconds / 1000f);
                        }
                        m_NewComparisonDataLoaded = false;
                    }

                    SelectMarker(m_SelectedMarkerName);
                    break;
            }

            if (m_RequestAnalysis)
            {
                if (!IsAnalysisRunning())
                {
                    Analyze();
                    m_RequestAnalysis = false;
                }
            }
            if (m_RequestCompare)
            {
                if (!IsAnalysisRunning())
                {
                    Compare();
                    m_RequestCompare = false;
                }
            }

            if (m_RequestRepaint)
            {
                Repaint();
                m_RequestRepaint = false;
            }

            if (m_AnalyzeInUpdatePhase > 0)
            {
                switch (m_AnalyzeInUpdatePhase)
                {
                    case 1:
                        UnityEngine.Profiling.Profiler.enabled = true;
                        m_AnalyzeInUpdatePhase++;
                        return;
                    case 2:
                        AnalyzeSync();
                        UpdateAnalysisFromAsyncProcessing(m_ProfileSingleView, m_FullAnalysisRequired);
                        m_FullAnalysisRequired = false;
                        m_AnalyzeInUpdatePhase++;
                        return;
                    case 3:
                        m_AnalyzeInUpdatePhase++;
                        return;
                    case 4:
                        UnityEngine.Profiling.Profiler.enabled = false;
                        m_AnalyzeInUpdatePhase++;
                        return;
                    default:
                        m_AnalyzeInUpdatePhase = 0;
                        break;
                }
            }
        }

        void UpdateAnalysisFromAsyncProcessing(ProfileDataView view, bool full)
        {
            view.analysis = view.analysisNew;
            if (full)
            {
                if (view.selectedIndices != null && view.IsDataValid() && view.selectedIndices.Count == view.data.GetFrameCount())
                    view.analysisFull = view.analysis;
                else
                    view.analysisFull = view.analysisFullNew;
            }
        }

        List<FrameTimeGraph.Data> GetFrameTimeData(ProfileData profileData)
        {
            List<FrameTimeGraph.Data> data = new List<FrameTimeGraph.Data>();
            int frames = profileData.GetFrameCount();

            for (int frameOffset = 0; frameOffset < frames; frameOffset++)
            {
                float ms = profileData.GetFrame(frameOffset).msFrame;
                FrameTimeGraph.Data dataPoint = new FrameTimeGraph.Data(ms, frameOffset);
                data.Add(dataPoint);
            }

            return data;
        }

        List<FrameTimeGraph.Data> GetThreadFrameTimeData(ProfileData profileData, string threadName)
        {
            List<FrameTimeGraph.Data> data = new List<FrameTimeGraph.Data>();
            int frames = profileData.GetFrameCount();
            int threadIndex = profileData.GetThreadIndexByThreadName(threadName);
            if (threadIndex < 0)
            {
                return data;
            }
            for (int frameOffset = 0; frameOffset < frames; frameOffset++)
            {
                float ms = 0.0f;
                var frame = profileData.GetFrame(frameOffset);
                if (frame != null)
                {
                    ms = frame.GetThreadFrameTime(threadIndex);
                }

                FrameTimeGraph.Data dataPoint = new FrameTimeGraph.Data(ms, frameOffset);
                data.Add(dataPoint);
            }

            return data;
        }

        void Load()
        {
            string path = EditorUtility.OpenFilePanel("Load profile analyzer data file", "", "pdata");
            if (path.Length != 0)
            {
                ProfileData newData;
                if (ProfileData.Load(path, out newData))
                {
                    SetView(m_ProfileSingleView, newData, path, m_FrameTimeGraph);

                    m_RequestAnalysis = true;
                    m_FullAnalysisRequired = true;
                }
            }
        }

        void UpdateMatchingProfileData(ProfileData data, ref string path, ProfileAnalysis analysis, string newPath)
        {
            // Update left/right data if we are effectively overwriting it.
            if (m_ProfileLeftView.path == newPath)
            {
                SetView(m_ProfileLeftView, data, newPath, m_LeftFrameTimeGraph);

                m_RequestCompare = true;
                m_FullCompareRequired = true;
            }
            if (m_ProfileRightView.path == newPath)
            {
                SetView(m_ProfileRightView, data, newPath, m_RightFrameTimeGraph);

                m_RequestCompare = true;
                m_FullCompareRequired = true;
            }

            // Update single view if needed
            if (m_ProfileSingleView.path == newPath)
            {
                SetView(m_ProfileSingleView, data, newPath, m_FrameTimeGraph);

                m_ProfileSingleView.analysis = analysis;
            }

            path = newPath;
        }

        void Save()
        {
            string newPath = EditorUtility.SaveFilePanel("Save profile analyzer data file", "", "capture.pdata", "pdata");
            if (newPath.Length != 0)
            {
                if (ProfileData.Save(newPath, m_ProfileSingleView.data))
                {
                    UpdateMatchingProfileData(m_ProfileSingleView.data, ref m_ProfileSingleView.path, m_ProfileSingleView.analysis, newPath);
                }
            }
        }

        int GetTotalCombinedMarkerCount(ProfileData left, ProfileData right)
        {
            if (left == null)
                return 0;
            if (right == null)
                return 0;
            List<string> leftMarkers = left.GetMarkerNames();
            if (leftMarkers == null)
                return 0;
            List<string> rightMarkers = right.GetMarkerNames();
            if (rightMarkers == null)
                return 0;

            HashSet<string> markerPairs = new HashSet<string>();
            for (int index = 0; index < leftMarkers.Count; index++)
            {
                string markerName = leftMarkers[index];

                markerPairs.Add(markerName);
            }
            for (int index = 0; index < rightMarkers.Count; index++)
            {
                string markerName = rightMarkers[index];

                if (!markerPairs.Contains(markerName))
                {
                    markerPairs.Add(markerName);
                }
            }

            return markerPairs.Count;
        }

        List<MarkerPairing> GeneratePairings(ProfileAnalysis leftAnalysis, ProfileAnalysis rightAnalysis)
        {
            if (leftAnalysis == null)
                return null;
            if (rightAnalysis == null)
                return null;
            List<MarkerData> leftMarkers = leftAnalysis.GetMarkers();
            if (leftMarkers == null)
                return null;
            List<MarkerData> rightMarkers = rightAnalysis.GetMarkers();
            if (rightMarkers == null)
                return null;

            Dictionary<string, MarkerPairing> markerPairs = new Dictionary<string, MarkerPairing>();
            for (int index = 0; index < leftMarkers.Count; index++)
            {
                MarkerData marker = leftMarkers[index];

                MarkerPairing pair = new MarkerPairing
                {
                    name = marker.name,
                    leftIndex = index,
                    rightIndex = -1
                };
                markerPairs[marker.name] = pair;
            }
            for (int index = 0; index < rightMarkers.Count; index++)
            {
                MarkerData marker = rightMarkers[index];

                if (markerPairs.ContainsKey(marker.name))
                {
                    MarkerPairing pair = markerPairs[marker.name];
                    pair.rightIndex = index;
                    markerPairs[marker.name] = pair;
                }
                else
                {
                    MarkerPairing pair = new MarkerPairing
                    {
                        name = marker.name,
                        leftIndex = -1,
                        rightIndex = index
                    };
                    markerPairs[marker.name] = pair;
                }
            }

            List<MarkerPairing> pairings = new List<MarkerPairing>();
            foreach (MarkerPairing pair in markerPairs.Values)
                pairings.Add(pair);

            return pairings;
        }

        void SetThreadPhaseCount(ThreadActivity activity)
        {
            // Will be refined by the analysis functions
            if (activity == ThreadActivity.Compare)
            {
                m_ThreadPhases = 8;
            }
            else
            {
                m_ThreadPhases = 2;
            }
        }

        void BeginAsyncAction(ThreadActivity activity)
        {
            if (IsAnalysisRunning())
                return;

            m_ThreadActivity = activity;
            m_ThreadProgress = 0;
            m_ThreadPhase = 0;
            SetThreadPhaseCount(activity);

            m_BackgroundThread = new Thread(BackgroundThread);
            m_BackgroundThread.Start();
        }

        void CreateComparisonTable()
        {
            UpdateThreadNames();

            // Set default sorting state
            int sortedColumn = (int)ComparisonTable.MyColumns.AbsDiff;
            bool sortAscending = false;

            // Query last sorting state
            if (m_ComparisonMulticolumnHeaderState != null)
            {
                if (m_ComparisonMulticolumnHeaderState.sortedColumnIndex >= 0)
                {
                    sortedColumn = m_ComparisonMulticolumnHeaderState.sortedColumnIndex;
                    if (sortedColumn >= 0 && sortedColumn < m_ComparisonMulticolumnHeaderState.columns.Length)
                        sortAscending = m_ComparisonMulticolumnHeaderState.columns[sortedColumn].sortedAscending;
                }
            }

            if (m_ComparisonTreeViewState == null)
                m_ComparisonTreeViewState = new TreeViewState();

            m_ComparisonMulticolumnHeaderState = ComparisonTable.CreateDefaultMultiColumnHeaderState(m_CompareModeFilter);

            var multiColumnHeader = new MultiColumnHeader(m_ComparisonMulticolumnHeaderState);
            multiColumnHeader.SetSorting(sortedColumn, sortAscending);
            multiColumnHeader.ResizeToFit();
            m_ComparisonTable = new ComparisonTable(m_ComparisonTreeViewState, multiColumnHeader, m_ProfileLeftView.analysis, m_ProfileRightView.analysis, m_Pairings, this, m_2D, UIColor.left, UIColor.right);

            if (string.IsNullOrEmpty(m_SelectedMarkerName))
                SelectPairing(0);
            else
                SelectPairingByName(m_SelectedMarkerName);
        }

        void CalculatePairingbuckets(ProfileAnalysis left, ProfileAnalysis right, List<MarkerPairing> pairings)
        {
            var leftMarkers = left.GetMarkers();
            var rightMarkers = right.GetMarkers();
            foreach (var pairing in pairings)
            {
                float min = float.MaxValue;
                float max = 0.0f;
                MarkerData leftMarker = null;
                MarkerData rightMarker = null;
                if (pairing.leftIndex >= 0)
                {
                    leftMarker = leftMarkers[pairing.leftIndex];
                    max = Math.Max(max, leftMarker.msMax);
                    min = Math.Min(min, leftMarker.msMin);
                }
                if (pairing.rightIndex >= 0)
                {
                    rightMarker = rightMarkers[pairing.rightIndex];
                    max = Math.Max(max, rightMarker.msMax);
                    min = Math.Min(min, rightMarker.msMin);
                }

                int countMin = int.MaxValue;
                int countMax = 0;
                if (leftMarker != null)
                {
                    countMax = Math.Max(countMax, leftMarker.countMax);
                    countMin = Math.Min(countMin, leftMarker.countMin);
                }
                if (rightMarker != null)
                {
                    countMax = Math.Max(countMax, rightMarker.countMax);
                    countMin = Math.Min(countMin, rightMarker.countMin);
                }

                if (leftMarker != null)
                {
                    leftMarker.ComputeBuckets(min, max);
                    leftMarker.ComputeCountBuckets(countMin, countMax);
                }
                if (rightMarker != null)
                {
                    rightMarker.ComputeBuckets(min, max);
                    rightMarker.ComputeCountBuckets(countMin, countMax);
                }
            }
        }

        int CalculateDepthDifference(ProfileAnalysis leftAnalysis, ProfileAnalysis rightAnalysis, List<MarkerPairing> pairings)
        {
            if (pairings.Count <= 0)
            {
                m_DepthDiff = 0;
                return 0;
            }

            var leftMarkers = leftAnalysis.GetMarkers();
            var rightMarkers = rightAnalysis.GetMarkers();

            int totalCount = 0;
            Dictionary<int, int> depthDifferences = new Dictionary<int, int>();
            foreach (var pairing in pairings)
            {
                if (pairing.leftIndex >= 0 && pairing.rightIndex >= 0)
                {
                    MarkerData leftMarker = leftMarkers[pairing.leftIndex];
                    MarkerData rightMarker = rightMarkers[pairing.rightIndex];
                    int markerDepthDiff = rightMarker.minDepth - leftMarker.minDepth;

                    int value = 0;
                    depthDifferences.TryGetValue(markerDepthDiff, out value);
                    depthDifferences[markerDepthDiff] = value + 1;
                    totalCount += 1;
                }
            }

            m_DepthDiff = 0;

            // Find most common depth difference
            int maxCount = 0;
            foreach (var diff in depthDifferences.Keys)
            {
                if (depthDifferences[diff] > maxCount)
                {
                    maxCount = depthDifferences[diff];
                    m_DepthDiff = diff;
                }
            }

            return m_DepthDiff;
        }

        bool CompareSync()
        {
            if (!m_ProfileLeftView.IsDataValid())
                return false;
            if (!m_ProfileRightView.IsDataValid())
                return false;

            List<string> threadUINamesNew;
            List<string> threadNamesNew;
            Dictionary<string, string> threadNameToUINameNew;

            GetThreadNames(m_ProfileLeftView.data, m_ProfileRightView.data, out threadUINamesNew, out threadNamesNew, out threadNameToUINameNew);
            List<string> threadSelection = GetLimitedThreadSelection(threadNamesNew, m_ThreadSelection);

            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            int updateDepthPhase = m_NewComparisonDataLoaded ? 2 : 0;
            int fullLeftPhase = (m_FullCompareRequired && m_ProfileLeftView.selectedIndices.Count != m_ProfileLeftView.data.GetFrameCount()) ? 1 : 0;
            int fullRightPhase = (m_FullCompareRequired && m_ProfileLeftView.selectedIndices.Count != m_ProfileLeftView.data.GetFrameCount()) ? 1 : 0;
            m_ThreadPhases = 2 + updateDepthPhase + fullLeftPhase + fullRightPhase + 2;

            bool selfTimes = IsSelfTime();

            // First scan just the frames
            m_ThreadPhase = 0;
            var leftAnalysisNew = m_ProfileAnalyzer.Analyze(m_ProfileLeftView.data, m_ProfileLeftView.selectedIndices, null, m_DepthFilter1, selfTimes, m_ParentMarker);
            m_ThreadPhase++;
            var rightAnalysisNew = m_ProfileAnalyzer.Analyze(m_ProfileRightView.data, m_ProfileRightView.selectedIndices, null, m_DepthFilter2, selfTimes, m_ParentMarker);
            m_ThreadPhase++;

            if (leftAnalysisNew == null || rightAnalysisNew == null)
            {
                stopwatch.Stop();
                return false;
            }

            // Calculate the max frame time of the two scans 
            float timeScaleMax = Math.Max(leftAnalysisNew.GetFrameSummary().msMax, rightAnalysisNew.GetFrameSummary().msMax);

            // Need to recalculate the depth difference when thread filters change
            // For now do it always if the depth is auto and not 'all'
            if (updateDepthPhase != 0)
            {
                var leftAnalysis = m_ProfileAnalyzer.Analyze(m_ProfileLeftView.data, m_ProfileLeftView.selectedIndices, threadSelection, ProfileAnalyzer.kDepthAll, selfTimes, m_ParentMarker, timeScaleMax);
                m_ThreadPhase++;
                var rightAnalysis = m_ProfileAnalyzer.Analyze(m_ProfileRightView.data, m_ProfileRightView.selectedIndices, threadSelection, ProfileAnalyzer.kDepthAll, selfTimes, m_ParentMarker, timeScaleMax);
                m_ThreadPhase++;

                var pairings = GeneratePairings(leftAnalysis, rightAnalysis);

                int originalDepthDiff = m_DepthDiff;
                int newDepthDiff = CalculateDepthDifference(leftAnalysis, rightAnalysis, pairings);
                if (newDepthDiff != originalDepthDiff)
                {
                    UpdateAutoDepthFilter();

                    // New depth diff calculated to we need to do the full analysis
                    if (fullLeftPhase == 0)
                    {
                        fullLeftPhase = 1;
                        m_ThreadPhases++;
                    }
                    if (fullRightPhase == 0)
                    {
                        fullRightPhase = 1;
                        m_ThreadPhases++;
                    }
                }
            }

            // Now process the markers and setup buckets using the overall max frame time
            List<int> selection = new List<int>();
            if (fullLeftPhase != 0)
            {
                selection.Clear();
                for (int frameOffset = 0; frameOffset < m_ProfileLeftView.data.GetFrameCount(); frameOffset++)
                {
                    selection.Add(m_ProfileLeftView.data.OffsetToDisplayFrame(frameOffset));
                }

                // We don't pass timeScaleMax as that is only for the selected region.
                // Pass 0 to auto select full range
                m_ProfileLeftView.analysisFullNew = m_ProfileAnalyzer.Analyze(m_ProfileLeftView.data, selection, threadSelection, m_DepthFilter1, selfTimes, m_ParentMarker, 0);
                m_ThreadPhase++;
            }

            if (fullRightPhase != 0)
            {
                selection.Clear();
                for (int frameOffset = 0; frameOffset < m_ProfileRightView.data.GetFrameCount(); frameOffset++)
                {
                    selection.Add(m_ProfileRightView.data.OffsetToDisplayFrame(frameOffset));
                }

                // We don't pass timeScaleMax as that is only for the selected region.
                // Pass 0 to auto select full range
                m_ProfileRightView.analysisFullNew = m_ProfileAnalyzer.Analyze(m_ProfileRightView.data, selection, threadSelection, m_DepthFilter2, selfTimes, m_ParentMarker, 0);
                m_ThreadPhase++;
            }

            m_ProfileLeftView.analysisNew = m_ProfileAnalyzer.Analyze(m_ProfileLeftView.data, m_ProfileLeftView.selectedIndices, threadSelection, m_DepthFilter1, selfTimes, m_ParentMarker, timeScaleMax);
            m_ThreadPhase++;

            m_ProfileRightView.analysisNew = m_ProfileAnalyzer.Analyze(m_ProfileRightView.data, m_ProfileRightView.selectedIndices, threadSelection, m_DepthFilter2, selfTimes, m_ParentMarker, timeScaleMax);

            m_TotalCombinedMarkerCountNew = GetTotalCombinedMarkerCount(m_ProfileLeftView.data, m_ProfileRightView.data);
            m_PairingsNew = GeneratePairings(m_ProfileLeftView.analysisNew, m_ProfileRightView.analysisNew);

            CalculatePairingbuckets(m_ProfileLeftView.analysisNew, m_ProfileRightView.analysisNew, m_PairingsNew);

            stopwatch.Stop();
            m_LastCompareTimeMilliseconds = stopwatch.ElapsedMilliseconds;

            TimeSpan ts = stopwatch.Elapsed;
            if (ts.Minutes > 0)
                m_LastCompareTime = string.Format("Last compare time {0} mins {1} secs {2} ms ", ts.Minutes, ts.Seconds, ts.Milliseconds);
            else if (ts.Seconds > 0)
                m_LastCompareTime = string.Format("Last compare time {0} secs {1} ms ", ts.Seconds, ts.Milliseconds);
            else
                m_LastCompareTime = string.Format("Last compare time {0} ms ", ts.Milliseconds);

            return true;
        }

        void Compare()
        {
            if (m_Async)
            {
                m_ShowFilters = true;   // To see loading bar

                //m_comparisonTable = null;
                //m_ProfileLeftView.analysis = null;
                //m_ProfileRightView.analysis = null;
                BeginAsyncAction(ThreadActivity.Compare);
            }
            else
            {
                CompareSync();

                UpdateAnalysisFromAsyncProcessing(m_ProfileLeftView, m_FullCompareRequired);
                UpdateAnalysisFromAsyncProcessing(m_ProfileRightView, m_FullCompareRequired);
                m_FullCompareRequired = false;
            }
        }

        List<MarkerPairing> GetPairings()
        {
            return m_Pairings;
        }

        void GetFrameRangeFromProfiler()
        {
            m_ProfilerWindowInterface.GetFrameRangeFromProfiler(out m_PullFirstFrameindex, out m_PullLastFrameindex);
            m_ProfileAnalyzer.QuickScan();
        }

        int GetUnsavedIndex(string path)
        {
            if (path == null)
                return 0;

            Regex unsavedRegExp = new Regex(@"^Unsaved[\s*]([\d]*)", RegexOptions.IgnoreCase);
            Match match = unsavedRegExp.Match(path);
            if (match.Length <= 0)
                return 0;

            return Int32.Parse(match.Groups[1].Value);
        }

        void PullFromProfiler(int firstFrame, int lastFrame, ProfileDataView view, FrameTimeGraph frameTimeGraph)
        {
            m_ProgressBar.InitProgressBar("Pulling Frames from Profiler", "Please wait...", lastFrame - firstFrame);

            var analytic = ProfileAnalyzerAnalytics.BeginAnalytic();
            ProfileData newProfileData = m_ProfilerWindowInterface.PullFromProfiler(firstFrame, lastFrame);
            ProfileAnalyzerAnalytics.SendUIButtonEvent(ProfileAnalyzerAnalytics.UIButton.Pull, analytic);
            newProfileData.CalcPerFrameThreadTime();

            frameTimeGraph.Reset();
            frameTimeGraph.SetData(GetFrameTimeData(newProfileData));

            // Check if this is new data (rather than repulling the same data)
            if (!newProfileData.IsSame(view.data))
            {
                if (view == m_ProfileSingleView)
                    m_NewDataLoaded = true;
                else
                    m_NewComparisonDataLoaded = true;
            }

            // Update the path to use the same saved file name if this is the same data as another view
            if (newProfileData.IsSame(m_ProfileLeftView.data))
            {
                view.path = m_ProfileLeftView.path;
            }
            else if (newProfileData.IsSame(m_ProfileRightView.data))
            {
                view.path = m_ProfileRightView.path;
            }
            else if (newProfileData.IsSame(m_ProfileSingleView.data))
            {
                view.path = m_ProfileSingleView.path;
            }
            else
            {
                int lastIndex = 0;
                lastIndex = Math.Max(lastIndex, GetUnsavedIndex(m_ProfileLeftView.path));
                lastIndex = Math.Max(lastIndex, GetUnsavedIndex(m_ProfileRightView.path));
                view.path = string.Format("Unsaved {0}", lastIndex + 1);
            }

            view.data = newProfileData;
            view.SelectFullRange();

            // Remove pairing if both left/right point at the same data
            if (m_ProfileLeftView.path == m_ProfileRightView.path)
            {
                SetFrameTimeGraphPairing(false);
            }

            m_ProgressBar.ClearProgressBar();
        }

        void BackgroundThread()
        {
            switch (m_ThreadActivity)
            {
                case ThreadActivity.Analyze:
                    AnalyzeSync();
                    m_ThreadActivity = ThreadActivity.AnalyzeDone;
                    break;

                case ThreadActivity.Compare:
                    CompareSync();
                    m_ThreadActivity = ThreadActivity.CompareDone;
                    break;

                case ThreadActivity.AnalyzeDone:
                    break;

                case ThreadActivity.CompareDone:
                    break;

                default:
                    // m_threadActivity = ThreadActivity.None;
                    break;
            }
        }

        void SelectFirstMarkerInTable()
        {
            // SelectMarkerByIndex(0) would only select the first one found, not the first in the sorted list

            if (m_ProfileTable == null)
                return;

            var rows = m_ProfileTable.GetRows();
            if (rows == null || rows.Count < 1)
                return;

            SelectMarkerByName(rows[0].displayName);
        }

        void CreateProfileTable()
        {
            if (m_ProfileTreeViewState == null)
                m_ProfileTreeViewState = new TreeViewState();

            // Set default sorting state
            int sortedColumn = (int)ProfileTable.MyColumns.Median;
            bool sortAscending = false;

            // Query last sorting state
            if (m_ProfileMulticolumnHeaderState != null)
            {
                if (m_ProfileMulticolumnHeaderState.sortedColumnIndex >= 0)
                {
                    sortedColumn = m_ProfileMulticolumnHeaderState.sortedColumnIndex;
                    if (sortedColumn >= 0 && sortedColumn < m_ProfileMulticolumnHeaderState.columns.Length)
                        sortAscending = m_ProfileMulticolumnHeaderState.columns[sortedColumn].sortedAscending;
                }
            }

            m_ProfileMulticolumnHeaderState = ProfileTable.CreateDefaultMultiColumnHeaderState(m_SingleModeFilter);

            var multiColumnHeader = new MultiColumnHeader(m_ProfileMulticolumnHeaderState);
            multiColumnHeader.SetSorting(sortedColumn, sortAscending);
            multiColumnHeader.ResizeToFit();
            m_ProfileTable = new ProfileTable(m_ProfileTreeViewState, multiColumnHeader, m_ProfileSingleView.analysis, this, m_2D, UIColor.bar);

            if (string.IsNullOrEmpty(m_SelectedMarkerName))
                SelectFirstMarkerInTable();
            else
                SelectMarkerByName(m_SelectedMarkerName);

            UpdateThreadNames();
        }

        void AnalyzeSync()
        {
            if (!m_ProfileSingleView.IsDataValid())
                return;

            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            List<string> threadUINamesNew;
            List<string> threadNamesNew;
            Dictionary<string, string> threadNameToUINameNew;

            GetThreadNames(m_ProfileSingleView.data, out threadUINamesNew, out threadNamesNew, out threadNameToUINameNew);
            List<string> threadSelection = GetLimitedThreadSelection(threadNamesNew, m_ThreadSelection);

            int fullPhase = (m_FullAnalysisRequired && (m_ProfileSingleView.selectedIndices.Count != m_ProfileSingleView.data.GetFrameCount())) ? 1 : 0;
            m_ThreadPhases = 1 + fullPhase;

            bool selfTimes = IsSelfTime();

            m_ThreadPhase = 0;
            if (fullPhase == 1)
            {
                List<int> selection = new List<int>();
                for (int frameOffset = 0; frameOffset < m_ProfileSingleView.data.GetFrameCount(); frameOffset++)
                {
                    selection.Add(m_ProfileSingleView.data.OffsetToDisplayFrame(frameOffset));
                }

                m_ProfileSingleView.analysisFullNew = m_ProfileAnalyzer.Analyze(m_ProfileSingleView.data, selection, threadSelection, m_DepthFilter, selfTimes, m_ParentMarker);
                m_ThreadPhase++;
            }
            m_ProfileSingleView.analysisNew = m_ProfileAnalyzer.Analyze(m_ProfileSingleView.data, m_ProfileSingleView.selectedIndices, threadSelection, m_DepthFilter, selfTimes, m_ParentMarker);
            m_ThreadPhase++;
            stopwatch.Stop();
            m_LastAnalysisTimeMilliseconds = stopwatch.ElapsedMilliseconds;

            TimeSpan ts = stopwatch.Elapsed;
            if (ts.Minutes > 0)
                m_LastAnalysisTime = string.Format("Last analysis time {0} mins {1} secs {2} ms ", ts.Minutes, ts.Seconds, ts.Milliseconds);
            else if (ts.Seconds > 0)
                m_LastAnalysisTime = string.Format("Last analysis time {0} secs {1} ms ", ts.Seconds, ts.Milliseconds);
            else
                m_LastAnalysisTime = string.Format("Last analysis time {0} ms ", ts.Milliseconds);
        }

        void Analyze()
        {
            if (m_EnableAnalysisProfiling)
            {
                m_AnalyzeInUpdatePhase = 1;
                return;
            }

            if (m_Async)
            {
                m_ShowFilters = true;   // To see loading bar

                //m_profileTable = null;
                //m_ProfileSingleView.analysis = null;
                BeginAsyncAction(ThreadActivity.Analyze);
            }
            else
            {
                AnalyzeSync();
                UpdateAnalysisFromAsyncProcessing(m_ProfileSingleView, m_FullAnalysisRequired);
                m_FullAnalysisRequired = false;
            }
        }

        void GetThreadNames(ProfileData profleData, out List<string> threadUINames, out List<string> threadFilters, out Dictionary<string, string> threadNameToUIName)
        {
            GetThreadNames(profleData, null, out threadUINames, out threadFilters, out threadNameToUIName);
        }

        public string GetUIThreadName(string threadNameWithIndex)
        {
            string threadName = "";
            m_ThreadNameToUIName.TryGetValue(threadNameWithIndex, out threadName);
            return threadName;
        }

        string GetFriendlyThreadName(string threadNameWithIndex, bool single)
        {
            if (string.IsNullOrEmpty(threadNameWithIndex))
                return "";

            var info = threadNameWithIndex.Split(':');
            int threadGroupIndex = int.Parse(info[0]);
            var threadName = info[1].Trim();

            if (single) // Single instance of this thread name
            {
                return threadName;
            }
            else
            {
                // The original format was "Worker 0"
                // The internal formatting is 1:Worker (1+original value).
                // Hence the -1 here
                return string.Format("{0} {1}", threadName, threadGroupIndex - 1);
            }
        }

        internal int CompareUINames(string a, string b)
        {
            var aSpaceIndex = a.LastIndexOf(' ');
            var bSpaceIndex = b.LastIndexOf(' ');

            if (aSpaceIndex >= 0 && bSpaceIndex >= 0)
            {
                var aThreadName = a.Substring(0, aSpaceIndex);
                var bThreadName = b.Substring(0, bSpaceIndex);

                if (aThreadName == bThreadName)
                {
                    var aThreadIndex = a.Substring(aSpaceIndex + 1);
                    var bThreadIndex = b.Substring(bSpaceIndex + 1);

                    if (aThreadIndex == "All" && bThreadIndex != "All")
                        return -1;
                    if (aThreadIndex != "All" && bThreadIndex == "All")
                        return 1;

                    int aGroupIndex;
                    if (int.TryParse(aThreadIndex, out aGroupIndex))
                    {
                        int bGroupIndex;
                        if (int.TryParse(bThreadIndex, out bGroupIndex))
                        {
                            return aGroupIndex.CompareTo(bGroupIndex);
                        }
                    }
                }
            }

            return a.CompareTo(b);
        }

        void GetThreadNames(ProfileData leftData, ProfileData rightData, out List<string> threadUINames, out List<string> threadFilters, out Dictionary<string, string> threadNameToUIName)
        {
            List<string> threadNames = (leftData != null) ? new List<string>(leftData.GetThreadNames()) : new List<string>();
            if (rightData != null)
            {
                foreach (var threadNameWithIndex in rightData.GetThreadNames())
                {
                    if (!threadNames.Contains(threadNameWithIndex))
                    {
                        // TODO: Insert after last thread with same name (or at end)
                        threadNames.Add(threadNameWithIndex);
                    }
                }
            }

            Dictionary<string, string> threadNamesDict = new Dictionary<string, string>();
            for (int index = 0; index < threadNames.Count; index++)
            {
                var threadNameWithIndex = threadNames[index];
                var threadIdentifier = new ThreadIdentifier(threadNameWithIndex);

                if (threadIdentifier.index == 1)
                {
                    if (threadNames.Contains(string.Format("2:{0}", threadIdentifier.name)))
                    {
                        var threadGroupIdentifier = new ThreadIdentifier(threadIdentifier);
                        threadGroupIdentifier.SetAll();

                        // First thread name of a group with the same name
                        // Add an 'all' selection
                        threadNamesDict[string.Format("{0} : All", threadIdentifier.name)] = threadGroupIdentifier.threadNameWithIndex;
                        // And add the first item too
                        threadNamesDict[GetFriendlyThreadName(threadNameWithIndex, false)] = threadNameWithIndex;
                    }
                    else
                    {
                        // Single instance of this thread name
                        threadNamesDict[GetFriendlyThreadName(threadNameWithIndex, true)] = threadNameWithIndex;
                    }
                }
                else
                {
                    threadNamesDict[GetFriendlyThreadName(threadNameWithIndex, false)] = threadNameWithIndex;
                }
            }

            List<string> uiNames = new List<string>();
            foreach (string uiName in threadNamesDict.Keys)
                uiNames.Add(uiName);

            uiNames.Sort(CompareUINames);


            var allThreadIdentifier = new ThreadIdentifier();
            allThreadIdentifier.SetName("All");
            allThreadIdentifier.SetAll();

            threadUINames = new List<string>();
            threadFilters = new List<string>();
            threadNameToUIName = new Dictionary<string, string>();

            threadUINames.Add(allThreadIdentifier.name);
            threadFilters.Add(allThreadIdentifier.threadNameWithIndex);
            threadNameToUIName[allThreadIdentifier.name] = allThreadIdentifier.threadNameWithIndex;

            foreach (string uiName in uiNames)
            {
                // Strip off the group name
                // Note we don't do this in GetFriendlyThreadName else we would collapse the same named threads (in different groups) in the dict
                string groupName;
                string threadName = ProfileData.GetThreadNameWithoutGroup(uiName, out groupName);
                string threadFilter = threadNamesDict[uiName];
                threadUINames.Add(threadName);
                threadFilters.Add(threadFilter);

                threadNameToUIName[threadFilter] = threadName;
            }
        }

        void UpdateThreadGroupSelection(List<string> threadNames, ThreadSelection threadSelection)
        {
            // Make sure all members of active groups are present
            foreach (string threadGroupNameWithIndex in threadSelection.groups)
            {
                var threadGroupIdentifier = new ThreadIdentifier(threadGroupNameWithIndex);
                if (threadGroupIdentifier.name == "All" && threadGroupIdentifier.index == ThreadIdentifier.kAll)
                {
                    foreach (string threadNameWithIndex in threadNames)
                    {
                        var threadIdentifier = new ThreadIdentifier(threadNameWithIndex);
                        if (threadIdentifier.index != ThreadIdentifier.kAll)
                        {
                            if (!threadSelection.selection.Contains(threadNameWithIndex))
                                threadSelection.selection.Add(threadNameWithIndex);
                        }
                    }
                }
                else
                {
                    foreach (string threadNameWithIndex in threadNames)
                    {
                        var threadIdentifier = new ThreadIdentifier(threadNameWithIndex);
                        if (threadIdentifier.name == threadGroupIdentifier.name &&
                            threadIdentifier.index != ThreadIdentifier.kAll)
                        {
                            if (!threadSelection.selection.Contains(threadNameWithIndex))
                                threadSelection.selection.Add(threadNameWithIndex);
                        }
                    }
                }
            }
        }

        List<string> GetLimitedThreadSelection(List<string> threadNames, ThreadSelection threadSelection)
        {
            List<string> limitedThreadSelection = new List<string>();
            if (threadSelection.selection == null)
                return limitedThreadSelection;

            foreach (string threadNameWithIndex in threadSelection.selection)
            {
                if (threadNames.Contains(threadNameWithIndex))
                    limitedThreadSelection.Add(threadNameWithIndex);
            }

            // Make sure all members of active groups are present
            foreach (string threadGroupNameWithIndex in threadSelection.groups)
            {
                var threadGroupIdentifier = new ThreadIdentifier(threadGroupNameWithIndex);
                if (threadGroupIdentifier.name == "All" && threadGroupIdentifier.index == ThreadIdentifier.kAll)
                {
                    foreach (string threadNameWithIndex in threadNames)
                    {
                        var threadIdentifier = new ThreadIdentifier(threadNameWithIndex);
                        if (threadIdentifier.index != ThreadIdentifier.kAll)
                        {
                            if (!limitedThreadSelection.Contains(threadNameWithIndex))
                                limitedThreadSelection.Add(threadNameWithIndex);
                        }
                    }
                }
                else
                {
                    foreach (string threadNameWithIndex in threadNames)
                    {
                        var threadIdentifier = new ThreadIdentifier(threadNameWithIndex);
                        if (threadIdentifier.name == threadGroupIdentifier.name &&
                            threadIdentifier.index != ThreadIdentifier.kAll)
                        {
                            if (!limitedThreadSelection.Contains(threadNameWithIndex))
                                limitedThreadSelection.Add(threadNameWithIndex);
                        }
                    }
                }
            }

            return limitedThreadSelection;
        }


        int ClampToRange(int value, int min, int max)
        {
            if (value < min)
                value = min;
            if (value > max)
                value = max;

            return value;
        }

        void GraphActive(bool active)
        {
            RequestRepaint();
        }

        void SetRange(List<int> selectedOffsets, int clickCount, bool singleControlAction, FrameTimeGraph.State inputStatus)
        {
            if (inputStatus == FrameTimeGraph.State.Dragging)
                return;

            if (clickCount == 2)
            {
                if (selectedOffsets.Count > 0)
                    JumpToFrame(m_ProfileSingleView.data.OffsetToDisplayFrame(selectedOffsets[0]), false);
            }
            else
            {
                m_ProfileSingleView.selectedIndices.Clear();
                foreach (int offset in selectedOffsets)
                {
                    m_ProfileSingleView.selectedIndices.Add(m_ProfileSingleView.data.OffsetToDisplayFrame(offset));
                }
                // Keep indices sorted
                m_ProfileSingleView.selectedIndices.Sort();

                m_RequestAnalysis = true;
            }
        }

        internal void ClearSelection()
        {
            if (m_ActiveTab == ActiveTab.Summary)
            {
                m_ProfileSingleView.ClearSelection();

                m_RequestAnalysis = true;
            }
            if (m_ActiveTab == ActiveTab.Compare)
            {
                m_ProfileLeftView.ClearSelection();
                m_ProfileRightView.ClearSelection();

                m_RequestCompare = true;
            }
        }

        internal void SelectAllFrames()
        {
            if (m_ActiveTab == ActiveTab.Summary)
            {
                m_ProfileSingleView.SelectFullRange();

                m_RequestAnalysis = true;
            }
            if (m_ActiveTab == ActiveTab.Compare)
            {
                m_ProfileLeftView.SelectFullRange();
                m_ProfileRightView.SelectFullRange();

                m_RequestCompare = true;
            }
        }

        internal void SelectFramesContainingMarker(string markerName, bool inSelection)
        {
            if (m_ActiveTab == ActiveTab.Summary)
            {
                if (m_ProfileSingleView.SelectAllFramesContainingMarker(markerName, inSelection))
                {
                    m_RequestAnalysis = true;
                }
            }
            if (m_ActiveTab == ActiveTab.Compare)
            {
                if (m_ProfileLeftView.SelectAllFramesContainingMarker(markerName, inSelection))
                {
                    m_RequestCompare = true;
                }
                if (m_ProfileRightView.SelectAllFramesContainingMarker(markerName, inSelection))
                {
                    m_RequestCompare = true;
                }
            }
        }

        static List<string> GetNameFilters(string nameFilter)
        {
            List<string> nameFilters = new List<string>();
            if (string.IsNullOrEmpty(nameFilter))
                return nameFilters;

            // Get all quoted strings, without the quotes
            MatchCollection matches = quotedStringWithoutQuotes.Matches(nameFilter);
            foreach (Match match in matches)
            {
                var theData = match.Groups[1].Value;
                nameFilters.Add(theData);
            }

            // Get a new string with the quoted strings removed
            string remaining = quotedString.Replace(nameFilter, "");

            // Get all the remaining strings (that are space separated)
            matches = stringWithoutWhiteSpace.Matches(remaining);
            foreach (Match match in matches)
            {
                string theData = match.Groups[1].Value;
                nameFilters.Add(theData);
            }

            return nameFilters;
        }

        internal List<string> GetNameFilters()
        {
            return GetNameFilters(m_NameFilter);
        }

        internal List<string> GetNameExcludes()
        {
            return GetNameFilters(m_NameExclude);
        }

        internal bool NameInFilterList(string name, List<string> nameFilters)
        {
            switch (m_NameFilterOperation)
            {
                default:
                    //case NameFilterOperation.All:
                    return NameInAllFilterList(name, nameFilters);
                case NameFilterOperation.Any:
                    return NameInAnyFilterList(name, nameFilters);
            }
        }

        internal bool NameInExcludeList(string name, List<string> nameFilters)
        {
            switch (m_NameExcludeOperation)
            {
                default:
                    //case NameFilterOperation.All:
                    return NameInAllFilterList(name, nameFilters);
                case NameFilterOperation.Any:
                    return NameInAnyFilterList(name, nameFilters);
            }
        }

        internal bool NameInAllFilterList(string name, List<string> nameFilters)
        {
            foreach (string subString in nameFilters)
            {
                // As soon as name doesn't match one in the list then return false
                if (name.IndexOf(subString, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            // Name is matching all the filters in the list
            return true;
        }

        bool NameInAnyFilterList(string name, List<string> nameFilters)
        {
            foreach (string subString in nameFilters)
            {
                // As soon as names matches one in the list then return true
                if (name.IndexOf(subString, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        static string FilterWithQuotes(string markerName)
        {
            return markerName.Contains(" ") ? string.Format("\"{0}\"", markerName) : markerName;
        }

        static void AddFilter(ref string filter, string quotedMarkerName)
        {
            if (string.IsNullOrEmpty(filter))
                filter = quotedMarkerName;
            else
                filter = string.Format("{0} {1}", filter, quotedMarkerName);
        }

        static bool AddFilter(List<string> nameFilters, ref string filter, string markerName)
        {
            bool justAdded = false;

            string quotedMarkerName = FilterWithQuotes(markerName);
            if (!nameFilters.Contains(quotedMarkerName))
            {
                AddFilter(ref filter, quotedMarkerName);

                justAdded = true;
            }

            return justAdded;
        }

        internal void AddToIncludeFilter(string markerName)
        {
            if (AddFilter(GetNameFilters(), ref m_NameFilter, markerName))
                UpdateMarkerTable();

            // Remove from exclude list if in the include list
            RemoveFromExcludeFilter(markerName);
        }

        internal void AddToExcludeFilter(string markerName)
        {
            if (AddFilter(GetNameExcludes(), ref m_NameExclude, markerName))
                UpdateMarkerTable();

            // Remove from include list if in the include list
            RemoveFromIncludeFilter(markerName);
        }

        internal void RemoveFromFilter(string markerName, List<string> nameFilters, ref string newFilters)
        {
            if (nameFilters.Count == 0)
                return;

            string nameFilterString = "";
            bool updated = false;
            foreach (string filter in nameFilters)
            {
                if (filter != markerName)
                    AddFilter(ref nameFilterString, FilterWithQuotes(filter));
                else
                    updated = true;
            }

            if (updated)
            {
                newFilters = nameFilterString;

                UpdateMarkerTable();
            }
        }

        internal void RemoveFromIncludeFilter(string markerName)
        {
            RemoveFromFilter(markerName, GetNameFilters(), ref m_NameFilter);
        }

        internal void RemoveFromExcludeFilter(string markerName)
        {
            RemoveFromFilter(markerName, GetNameExcludes(), ref m_NameExclude);
        }

        internal void SetAsParentMarkerFilter(string markerName)
        {
            if (markerName != m_ParentMarker)
            {
                m_ParentMarker = markerName;
                UpdateActiveTab(true);
            }
        }

        float GetFilenameWidth(string path)
        {
            if (path == null)
                return 0f;

            string filename = System.IO.Path.GetFileName(path);
            GUIContent content = new GUIContent(filename, path);
            Vector2 size = GUI.skin.label.CalcSize(content);
            return size.x;
        }

        void ShowFilename(string path)
        {
            if (path != null)
            {
                string filename = System.IO.Path.GetFileNameWithoutExtension(path);
                GUIContent content = new GUIContent(filename, path);
                Vector2 size = GUI.skin.label.CalcSize(content);
                float width = Math.Min(size.x, 200f);
                EditorGUILayout.LabelField(content, GUILayout.MaxWidth(width));
            }
        }

        void DrawLoadSave()
        {
            EditorGUILayout.BeginHorizontal(GUILayout.MaxWidth(300), GUILayout.ExpandWidth(false));

            GUIStyle buttonStyle = GUI.skin.button;
            bool lastEnabled = GUI.enabled;
            GUI.enabled = !IsAnalysisRunning();
            if (GUILayout.Button("Load", buttonStyle, GUILayout.ExpandWidth(false), GUILayout.Width(50)))
                Load();
            if (GUILayout.Button("Save", buttonStyle, GUILayout.ExpandWidth(false), GUILayout.Width(50)))
                Save();
            GUI.enabled = lastEnabled;

            ShowFilename(m_ProfileSingleView.path);
            EditorGUILayout.EndHorizontal();
        }

        void ShowSelectedMarker()
        {
            bool valid = true;

            if (m_ActiveTab == ActiveTab.Summary)
            {
                if (IsAnalysisValid())
                {
                    valid = false;

                    List<MarkerData> markers = m_ProfileSingleView.analysis.GetMarkers();
                    if (markers != null)
                    {
                        int markerAt = m_SelectedMarker;
                        if (markerAt >= 0 && markerAt < markers.Count)
                        {
                            valid = true;
                        }
                    }
                }
            }
            else if (m_ActiveTab == ActiveTab.Compare)
            {
                if (IsAnalysisValid())
                {
                    valid = false;

                    List<MarkerData> leftMarkers = m_ProfileLeftView.analysis.GetMarkers();
                    List<MarkerData> rightMarkers = m_ProfileRightView.analysis.GetMarkers();
                    int pairingAt = m_SelectedPairing;
                    if (leftMarkers != null && rightMarkers != null && m_Pairings != null)
                    {
                        if (pairingAt >= 0 && pairingAt < m_Pairings.Count)
                        {
                            valid = true;
                        }
                    }
                }
            }

            if (valid)
            {
                DrawSelectedText(m_SelectedMarkerName);
            }
            else
            {
                string text = string.Format("{0} not in selection", m_SelectedMarkerName);

                GUIContent content = new GUIContent(text, text);
                Vector2 size = GUI.skin.label.CalcSize(content);
                Rect rect = EditorGUILayout.GetControlRect(GUILayout.MaxWidth(size.x), GUILayout.Height(size.y));
                if (Event.current.type == EventType.Repaint)
                {
                    GUI.Label(rect, content);
                }
            }
        }


        internal bool AllSelected()
        {
            if (m_ActiveTab == ActiveTab.Summary)
            {
                if (m_ProfileSingleView.AllSelected())
                    return true;
            }
            if (m_ActiveTab == ActiveTab.Compare)
            {
                if (m_ProfileLeftView.AllSelected() && m_ProfileRightView.AllSelected())
                    return true;
            }

            return false;
        }

        internal bool HasSelection()
        {
            if (m_ActiveTab == ActiveTab.Summary)
            {
                if (m_ProfileSingleView.HasSelection())
                    return true;
            }
            if (m_ActiveTab == ActiveTab.Compare)
            {
                if (m_ProfileLeftView.HasSelection())
                    return true;
                if (m_ProfileRightView.HasSelection())
                    return true;
            }

            return false;
        }

        void LogFuncToCSV()
        {
            List<int> selectedOffsets = new List<int>();
            
            int displayOffset = 0;
            int leftFrameCount = 0;
            int rightFrameCount = 0;
            int count = 0;
            if (m_ActiveTab == ActiveTab.Summary)
            {
                foreach (int index in m_ProfileSingleView.selectedIndices)
                {
                    selectedOffsets.Add(m_ProfileSingleView.data.DisplayFrameToOffset(index));
                }
                displayOffset = m_ProfileSingleView.data.OffsetToDisplayFrame(0);
                count = selectedOffsets.Count;
                if (count == 0)
                {
                    EditorUtility.DisplayDialog("Result", "至少选择一帧数据", "ok");
                    return;
                }
            }
            else
            {
                displayOffset = m_ProfileLeftView.data.OffsetToDisplayFrame(0);
                if (displayOffset == 0)
                {
                    displayOffset = m_ProfileRightView.data.OffsetToDisplayFrame(0);
                }

                leftFrameCount = m_ProfileLeftView.data.GetFrameCount();
                rightFrameCount = m_ProfileRightView.data.GetFrameCount();
                if (leftFrameCount == 0 || rightFrameCount == 0)
                {
                     EditorUtility.DisplayDialog("Result", "保证对比的两项都有数据", "ok");
                     return;
                }
            }


            AnalyzerFunc[] funcNames = {
                new AnalyzerFunc(){funcName="PlayerLoop",tag ="所有"},
                new AnalyzerFunc(){funcName="Initialization.PlayerUpdateTime",tag ="空闲"},
                new AnalyzerFunc(){funcName="BehaviourUpdate",tag="总Update"},
                new AnalyzerFunc(){funcName="MSGame.dll!::ClientRoot.Update() [Invoke]"},
                new AnalyzerFunc(){funcName="MS.GameLevel.ClientRoomManager",tag="单局总Update"},
                new AnalyzerFunc(){funcName = "[MSPreloadManager]MSPreloadManager.Tick",tag="单局"},
                new AnalyzerFunc(){funcName="PreLateUpdate.ParticleSystemBeginUpdateAll",tag="特效"},
                new AnalyzerFunc(){funcName="PostLateUpdate.ParticleSystemEndUpdateAll",tag="特效"},
                new AnalyzerFunc(){funcName = "ParticleSystem.Update",tag="特效数量"},
                new AnalyzerFunc(){funcName = "ParticleSystem.Update2",tag="特效数量"},
                new AnalyzerFunc(){funcName = "ParticleSystem.UpdateJob",tag="特效数量"},
                
                new AnalyzerFunc(){funcName="PreLateUpdate.DirectorUpdateAnimationBegin",tag="动画"},
                new AnalyzerFunc(){funcName="PreLateUpdate.DirectorUpdateAnimationEnd",tag="动画"},
                new AnalyzerFunc(){funcName="PreLateUpdate.LegacyAnimationUpdate",tag="动画"},
                new AnalyzerFunc(){funcName="Update.DirectorUpdate",tag="动画"},

                new AnalyzerFunc(){funcName="LateUpdate_MS.GameLevel.ClientRoomManager",tag="单局总LateUpdate"},
                new AnalyzerFunc(){funcName="_LateUpdate",tag ="总LateUpdate"},

                
                
                new AnalyzerFunc(){funcName="IgUIManager.Tick",tag ="单局"},
                new AnalyzerFunc(){funcName="InternalHelper.Tick",tag="单局"},
                new AnalyzerFunc(){funcName="BulletManager.LogicUpdate",tag ="单局BPart"},
                new AnalyzerFunc(){funcName="ActionManager.LogicUpdate",tag ="单局"},
                new AnalyzerFunc(){funcName="PhysicsWorld.Simulate",tag ="单局APart"},
                new AnalyzerFunc(){funcName="ActorManager.ControlHelperUpdate",tag ="单局"},
                new AnalyzerFunc(){funcName="MS.UI.UIManager",tag ="系统"},
                new AnalyzerFunc(){funcName="PostLateUpdate.PlayerUpdateCanvases",tag ="系统"},
                new AnalyzerFunc(){funcName ="Unity.ugui.dll!UnityEngine.EventSystems::EventSystem.Update() [Invoke]",tag="系统"},
                new AnalyzerFunc(){funcName="PostLateUpdate.PlayerEmitCanvasGeometry",tag ="系统"},
                new AnalyzerFunc(){funcName="PostLateUpdate.FinishFrameRendering",tag ="渲染"},
                new AnalyzerFunc(){funcName="PostLateUpdate.UpdateAllRenderers",tag ="渲染"},
                new AnalyzerFunc(){funcName="MSGame.dll!MS.GameLevel::ProjectorShadow.LateUpdate() [Invoke]",tag ="渲染"},
                new AnalyzerFunc(){funcName="[Auto]Skin.UpdateRenderingInfoPerformance",tag ="渲染"},
                new AnalyzerFunc(){funcName="GameCore.dll!MS.Framework.NewResource::ResLoaderWrap.Update() [Invoke]",tag ="资源"},
                new AnalyzerFunc(){funcName = "EarlyUpdate.UpdateTextureStreamingManager",tag="资源"},
                new AnalyzerFunc(){funcName = "[Auto]AudioMgr.Tick",tag="资源"},

                new AnalyzerFunc(){funcName = "[Auto]EnemyCamp.CreateEnemy",tag="单局数据"},
                new AnalyzerFunc(){funcName = "[Auto]ActionManager.PlayAction",tag="单局数据"},
                new AnalyzerFunc(){funcName = "[Auto]BulletManager.ShootBulletImmediately",tag="单局数据"},
                new AnalyzerFunc(){funcName = "[Auto]EmitBullet.ShootBullet",tag="单局数据"},
                new AnalyzerFunc(){funcName = "[Auto]CreateEffect.Enter",tag="单局数据"},
                new AnalyzerFunc(){funcName = "[Auto]BufferManager.AddBuffer",tag="单局数据"},
                new AnalyzerFunc(){funcName = "[Auto]BulletBase.LogicUpdate",tag="单局数据"},
                new AnalyzerFunc(){funcName = "BulletManager_Part.LogicUpdate",tag="单局数据"},

                new AnalyzerFunc(){funcName = "[Auto]HitedHelper.OnHited",tag="受击数量"},
                
                new AnalyzerFunc(){funcName = "GameAssetLoader.AllocSync",tag="同步加载数量"},
                new AnalyzerFunc(){funcName = "GameAssetLoader.Alloc",tag="异步加载数量"},
                new AnalyzerFunc(){funcName = "GameAssetLoader.LoadAssetSync",tag="异步加载数量"},
                new AnalyzerFunc(){funcName = "GameAssetLoader.ReleaseAsset",tag="异步卸载数量"},
                
                new AnalyzerFunc(){funcName = "PassiveSkill.OnTriggerEvent",tag="被动触发事件"},
                new AnalyzerFunc(){funcName = "[Auto]PassiveSkill.OnTriggerPassive",tag="被动触发事件"},
                
                new AnalyzerFunc(){funcName="GameLevel.Tick",tag ="单局"},
                new AnalyzerFunc(){funcName="GameLevel.LogicUpdate",tag ="单局"},
                new AnalyzerFunc(){funcName="GameLevel.LogicUpdate.APart",tag ="单局"},
                new AnalyzerFunc(){funcName="GameLevel.LogicUpdate.BPart",tag ="单局"},
                
                
                
                new AnalyzerFunc(){funcName="NavDataStorage.FixedUpdate",tag ="单局APart"},
                new AnalyzerFunc(){funcName="chunkManager.Tick",tag ="单局APart"},
                new AnalyzerFunc(){funcName="campManager.Tick",tag ="单局APart"},
                new AnalyzerFunc(){funcName="levelEnemyMgr.Tick",tag ="单局APart"},
                new AnalyzerFunc(){funcName="distanceDetect.Tick",tag ="单局APart"},
               
                new AnalyzerFunc(){funcName="ActorManager.UpdateActorOrder",tag ="单局APart"},
                new AnalyzerFunc(){funcName="ActorManager.LogicUpdate",tag ="单局APart"},
                new AnalyzerFunc(){funcName="ActorManager.UpdateMoveData",tag ="单局APart"},

                
                new AnalyzerFunc(){funcName="GameLevel.AITick",tag ="单局APart"},
                new AnalyzerFunc(){funcName="CurrentSubLevel.Tick",tag ="单局APart"},
                new AnalyzerFunc(){funcName="SubstituteManager.LogicUpdate",tag ="单局APart"},

                
                new AnalyzerFunc(){funcName="aiEventCenter.LateUpdate",tag ="单局BPart"},
                new AnalyzerFunc(){funcName="DropManager.Tick",tag ="单局BPart"},
                new AnalyzerFunc(){funcName="GrassManager.LogicUpdate",tag ="单局BPart"},
                new AnalyzerFunc(){funcName="GameLevelLogicManager.LogicUpdate",tag ="单局BPart"},
                new AnalyzerFunc(){funcName="levelProcessMgr.Tick",tag ="单局BPart"},
                new AnalyzerFunc(){funcName="BindLineRenderMgr.Update",tag ="单局BPart"},
                //BindLineRenderMgr
            };
            StringBuilder stringBuilder = new StringBuilder();
            if (m_ActiveTab == ActiveTab.Summary)
            {
                stringBuilder.Append($"funcName,tag,总耗时,self总耗时,平均每帧耗时,调用次数,平均每次耗时,每帧调用次数,self平均每帧耗时\n");
            }
            else
            {
                stringBuilder.Append($"funcName,tag,总耗时,self总耗时,平均每帧耗时,调用次数,平均每次耗时,每帧调用次数,self每帧耗时,,right总耗时,rightSelf总耗时,right平均每帧耗时,right调用次数,right平均每次耗时,right每帧调用次数,rightSelf每帧耗时,平均每帧调用次数增幅,每帧耗时增幅,每帧耗时diff,每次调用耗时增幅,每帧selfAvgDiff\n");
            }

            for (int i = 0; i < funcNames.Length; ++i)
            {
                if (m_ActiveTab == ActiveTab.Summary)
                {
                    int callCount = 0;
                    float selfTime = 0.0f;
                    float time = CalcSelectMarkerTimeAndCount(displayOffset, selectedOffsets, m_ProfileSingleView.analysis, funcNames[i].funcName,ref callCount,ref selfTime);
                    float avgPercall = 0.0f;
                
                    if (callCount != 0)
                    {
                        avgPercall = time / callCount;
                    }
                    stringBuilder.Append($"{funcNames[i].funcName},{funcNames[i].tag},{time},{selfTime},{time / count},{callCount},{avgPercall},{callCount*1.0f/count},{selfTime/count}\n");     
                }
                else
                {
                    
                    int leftCallCount = 0;
                    float leftSelfTime = 0.0f;
                    float leftTime = CalcAllMarkerTimeAndCount(displayOffset, leftFrameCount, m_ProfileLeftView.analysis, funcNames[i].funcName,ref leftCallCount,ref leftSelfTime);
                    float leftAvgPercall = 0.0f;
                
                    if (leftCallCount != 0)
                    {
                        leftAvgPercall = leftTime / leftCallCount;
                    }
                    
                    
                    int rightCallCount = 0;
                    float rightSelfTime = 0.0f;
                    float rightTime = CalcAllMarkerTimeAndCount(displayOffset, rightFrameCount, m_ProfileRightView.analysis, funcNames[i].funcName,ref rightCallCount,ref rightSelfTime);
                    float rightAvgPercall = 0.0f;
                
                    if (rightCallCount != 0)
                    {
                        rightAvgPercall = rightTime / rightCallCount;
                    }

                    float detlaCallsPercent = 0.0f;//(rightCallCount*1.0f/count - leftCallCount*1.0f/count);
                    if (leftCallCount * 1.0f / leftFrameCount > 0.00000f)
                    {
                        detlaCallsPercent = (rightCallCount*1.0f/rightFrameCount - leftCallCount*1.0f/leftFrameCount)/(leftCallCount*1.0f/leftFrameCount) * 100.0f;
                    }

                    float detlaTime = (rightTime / rightFrameCount - leftTime / leftFrameCount);
                    float detlaTimePercent = 0.0f;
                    if (leftTime / leftFrameCount > 0.000000f)
                    {
                        detlaTimePercent = detlaTime/(leftTime / leftFrameCount) * 100.0f;
                    }

                    float detlaCallTimePercent = 0.0f;
                    if (leftAvgPercall > 0.00000f)
                    {
                        detlaCallTimePercent = (rightAvgPercall - leftAvgPercall)/leftAvgPercall * 100.0f;
                    }

                    stringBuilder.Append($"{funcNames[i].funcName},{funcNames[i].tag},{leftTime},{leftSelfTime},{leftTime / leftFrameCount},{leftCallCount},{leftAvgPercall},{leftCallCount*1.0f/leftFrameCount},{leftSelfTime/leftFrameCount},,{rightTime},{rightSelfTime},{rightTime / rightFrameCount},{rightCallCount},{rightAvgPercall},{rightCallCount*1.0f/rightFrameCount},{rightSelfTime/rightFrameCount},{detlaCallsPercent}%,{detlaTimePercent}%,{detlaTime},{detlaCallTimePercent}%,{rightSelfTime/rightFrameCount-leftSelfTime/leftFrameCount}\n");  
                }

            }

            string path = Application.dataPath.Replace("Assets", string.Empty) + $"/func-{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.csv";
            File.WriteAllText(path, stringBuilder.ToString(), Encoding.UTF8);
            stringBuilder.Clear();
            Application.OpenURL(path);
        }

        float CalcSelectMarkerTime(int displayOffset, List<int> selectedOffsets, ProfileAnalysis analysisData, string markerName)
        {
            float totalTimes = 0.0f;
            if (!string.IsNullOrEmpty(markerName))
            {
                MarkerData selectedMarker = analysisData.GetMarkerByName(markerName);
                if (selectedMarker != null)
                {
                    for (int i = 0; i < selectedOffsets.Count; ++i)
                    {
                        int frameIndex = displayOffset + selectedOffsets[i];
                        totalTimes += selectedMarker.GetFrameMs(frameIndex);
                    }
                }

            }

            return totalTimes;
        }
        
        float CalcSelectMarkerTimeAndCount(int displayOffset, List<int> selectedOffsets, ProfileAnalysis analysisData, string markerName,ref int totalCount,ref float selfTimes)
        {
            selfTimes = 0.0f;
            float totalTimes = 0.0f;
            if (!string.IsNullOrEmpty(markerName))
            {
                MarkerData selectedMarker = analysisData.GetMarkerByName(markerName);
                if (selectedMarker != null)
                {
                    for (int i = 0; i < selectedOffsets.Count; ++i)
                    {
                        int frameIndex = displayOffset + selectedOffsets[i];
                        int count = 0;
                        float msSelfTime = 0.0f;

                        totalTimes += selectedMarker.GetFrameMsAndCount(frameIndex,ref count,ref msSelfTime);
                        totalCount += count;
                        selfTimes += msSelfTime;
                    }
                }

            }

            return totalTimes;
        }

        float CalcAllMarkerTimeAndCount(int displayOffset, int totalFrameCount, ProfileAnalysis analysisData, string markerName,ref int totalCount,ref float selfTimes)
        {
            float totalTimes = 0.0f;
            selfTimes = 0.0f;
            if (!string.IsNullOrEmpty(markerName))
            {
                MarkerData selectedMarker = analysisData.GetMarkerByName(markerName);
                if (selectedMarker != null)
                {
                    for (int i = 0; i < totalFrameCount; ++i)
                    {
                        int frameIndex = displayOffset + i;
                        int count = 0;
                        float msSelfTime = 0.0f;
                        totalTimes += selectedMarker.GetFrameMsAndCount(frameIndex,ref count,ref msSelfTime);
                        totalCount += count;
                        selfTimes += msSelfTime;
                    }
                }

            }

            return totalTimes;
        }
        
        void DrawFrameTimeGraph(float height)
        {
            Profiler.BeginSample("ProfileAnalyzer.DrawFrameTimeGraph");

            GUI.SetNextControlName("FrameTimeGraph");
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Height(height));

            if (m_ProfileSingleView.IsDataValid())
            {
                if (!m_FrameTimeGraph.HasData())
                    m_FrameTimeGraph.SetData(GetFrameTimeData(m_ProfileSingleView.data));
                if (!m_ProfileSingleView.HasValidSelection())
                    m_ProfileSingleView.SelectFullRange();


                List<int> selectedOffsets = new List<int>();
                foreach (int index in m_ProfileSingleView.selectedIndices)
                {
                    selectedOffsets.Add(m_ProfileSingleView.data.DisplayFrameToOffset(index));
                }

                float yRange = m_FrameTimeGraph.GetDataRange();
                int displayOffset = m_ProfileSingleView.data.OffsetToDisplayFrame(0);

                bool enabled = !IsAnalysisRunning();
                m_FrameTimeGraph.SetEnabled(enabled);
                m_FrameTimeGraph.Draw(rect, m_ProfileSingleView.analysis, selectedOffsets, yRange, displayOffset, m_SelectedMarkerName, 0, m_ProfileSingleView.analysisFull);

                if (m_ProfileSingleView.analysis != null)
                {
                    m_ProfileSingleView.analysis.SelectMarkerTimes = CalcSelectMarkerTime(displayOffset, selectedOffsets, m_ProfileSingleView.analysis, m_SelectedMarkerName);
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                ShowSelectedMarker();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                GUI.Label(rect, Styles.dataMissing, m_StyleUpperLeft);
            }

            Profiler.EndSample();
        }

        void SetDepthStrings(int maxDepth, out string[] strings, out int[] values)
        {
            List<string> depthStrings = new List<string>();
            List<int> depthValues = new List<int>();
            depthStrings.Add("All");
            depthValues.Add(ProfileAnalyzer.kDepthAll);
            for (int depth = 1; depth <= maxDepth; depth++)
            {
                depthStrings.Add(depth.ToString());
                depthValues.Add(depth);
            }
            strings = depthStrings.ToArray();
            values = depthValues.ToArray();
        }

        void DrawDepthFilter()
        {
            if (!IsAnalysisRunning())
            {
                if (m_ActiveTab == ActiveTab.Summary)
                {
                    int maxDepth = m_ProfileSingleView.GetMaxDepth();
                    if (m_DepthStrings == null || maxDepth != (1 + m_DepthStrings.Length))
                        SetDepthStrings(maxDepth, out m_DepthStrings, out m_DepthValues);
                }
                else
                {
                    int maxDepth = m_ProfileLeftView.GetMaxDepth();
                    if (m_DepthStrings1 == null || maxDepth != (1 + m_DepthStrings1.Length))
                        SetDepthStrings(maxDepth, out m_DepthStrings1, out m_DepthValues1);

                    maxDepth = m_ProfileRightView.GetMaxDepth();
                    if (m_DepthStrings2 == null || maxDepth != (1 + m_DepthStrings2.Length))
                        SetDepthStrings(maxDepth, out m_DepthStrings2, out m_DepthValues2);
                }
            }

            bool triggerRefresh = false;

            bool lastEnabled = GUI.enabled;
            bool enabled = !IsAnalysisRunning();

            EditorGUILayout.BeginHorizontal();// GUILayout.MaxWidth(300));
            if (m_ActiveTab == ActiveTab.Summary)
            {
                if (m_DepthStrings != null)
                {
                    EditorGUILayout.LabelField(Styles.depthTitle, GUILayout.Width(LayoutSize.FilterOptionsLeftLabelWidth));
                    int lastDepthFilter = m_DepthFilter;
                    m_DepthFilter = m_DepthFilter == ProfileAnalyzer.kDepthAll ? ProfileAnalyzer.kDepthAll : m_ProfileSingleView.ClampToValidDepthValue(m_DepthFilter);

                    GUI.enabled = enabled;
                    m_DepthFilter = EditorGUILayout.IntPopup(m_DepthFilter, m_DepthStrings, m_DepthValues, GUILayout.Width(LayoutSize.FilterOptionsEnumWidth));
                    GUI.enabled = lastEnabled;
                    if (m_DepthFilter != lastDepthFilter)
                        triggerRefresh = true;
                }
            }
            else
            {
                EditorGUILayout.LabelField(Styles.depthTitle, GUILayout.Width(LayoutSize.FilterOptionsLeftLabelWidth));

                EditorGUILayout.BeginHorizontal(GUILayout.MaxWidth(200));
                if (m_DepthStrings1 != null)
                {
                    EditorGUILayout.LabelField(Styles.leftDepthTitle, GUILayout.Width(LayoutSize.FilterOptionsEnumWidth));
                    int lastDepthFilter1 = m_DepthFilter1;
                    m_DepthFilter1 = m_DepthFilter1 == ProfileAnalyzer.kDepthAll ? ProfileAnalyzer.kDepthAll : m_ProfileLeftView.ClampToValidDepthValue(m_DepthFilter1);

                    GUI.enabled = enabled;
                    m_DepthFilter1 = EditorGUILayout.IntPopup(m_DepthFilter1, m_DepthStrings1, m_DepthValues1, GUILayout.Width(50));
                    GUI.enabled = lastEnabled;
                    if (m_DepthFilter1 != lastDepthFilter1)
                        triggerRefresh = true;
                }

                if (m_DepthStrings2 != null)
                {
                    int lastDepthFilter2 = m_DepthFilter2;

                    EditorGUILayout.LabelField(Styles.rightDepthTitle, GUILayout.Width(45));
                    m_DepthFilter2 = m_DepthFilter2 == ProfileAnalyzer.kDepthAll ? ProfileAnalyzer.kDepthAll : m_ProfileRightView.ClampToValidDepthValue(m_DepthFilter2);

                    GUI.enabled = enabled && !m_DepthFilter2Auto;
                    m_DepthFilter2 = EditorGUILayout.IntPopup(m_DepthFilter2, m_DepthStrings2, m_DepthValues2, GUILayout.Width(50));
                    GUI.enabled = lastEnabled;
                    if (m_DepthFilter2 != lastDepthFilter2)
                        triggerRefresh = true;
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                bool lastDepthFilter2Auto = m_DepthFilter2Auto;
                GUI.enabled = enabled;
                var content = new GUIContent(string.Format("Auto Right ({0:+##;-##;Depths Match})", m_DepthDiff), "Automatically calculate difference between marker depths between data sets");
                m_DepthFilter2Auto = EditorGUILayout.ToggleLeft(content, m_DepthFilter2Auto);
                GUI.enabled = lastEnabled;
                if (m_DepthFilter2Auto != lastDepthFilter2Auto)
                {
                    if (UpdateDepthFilters())
                        UpdateActiveTab(true);
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndHorizontal();

            if (triggerRefresh)
            {
                UpdateDepthFilters();
                UpdateActiveTab(true);
            }
        }

        void DrawParentFilter()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Styles.parentMarker, GUILayout.Width(100));

            if (!string.IsNullOrEmpty(m_ParentMarker))
            {
                bool lastEnabled = GUI.enabled;
                bool enabled = !IsAnalysisRunning();
                GUI.enabled = enabled;
                if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.MaxWidth(LayoutSize.FilterOptionsEnumWidth)))
                {
                    SetAsParentMarkerFilter("");
                }
                GUI.enabled = lastEnabled;

                DrawSelectedText(m_ParentMarker);
            }
            else
            {
                EditorGUILayout.LabelField(Styles.selectParentMarker);
            }

            EditorGUILayout.EndHorizontal();
        }

        bool UpdateAutoDepthFilter()
        {
            if (m_DepthFilter2Auto)
            {
                int newDepthFilter2 = (m_DepthFilter1 == ProfileAnalyzer.kDepthAll) ? ProfileAnalyzer.kDepthAll : m_ProfileRightView.ClampToValidDepthValue(m_DepthFilter1 + m_DepthDiff);
                if (m_DepthFilter2 != newDepthFilter2)
                {
                    m_DepthFilter2 = newDepthFilter2;
                    return true;
                }
            }

            return false;
        }

        bool UpdateDepthFilters()
        {
            bool changed = false;

            if (m_ActiveTab == ActiveTab.Compare)
            {
                // First respect the auto flag
                if (UpdateAutoDepthFilter())
                    changed = true;

                // Make sure Single matches the updated comparison view
                if (m_ProfileLeftView.path == m_ProfileSingleView.path)
                {
                    // Use same filter on single view if its the same file
                    if (m_DepthFilter != m_DepthFilter1)
                    {
                        m_DepthFilter = m_DepthFilter1;
                        changed = true;
                    }
                }
                if (m_ProfileRightView.path == m_ProfileSingleView.path)
                {
                    // Use same filter on single view if its the same file
                    if (m_DepthFilter != m_DepthFilter2)
                    {
                        m_DepthFilter = m_DepthFilter2;
                        changed = true;
                    }
                }
            }
            else
            {
                // Make sure comparisons match updated single view

                if (m_ProfileLeftView.path == m_ProfileSingleView.path)
                {
                    // Use same filter on comparison left view if its the same file
                    if (m_DepthFilter1 != m_DepthFilter)
                    {
                        m_DepthFilter1 = m_DepthFilter;
                        changed = true;
                    }

                    if (UpdateAutoDepthFilter())
                        changed = true;
                }

                if (m_ProfileRightView.path == m_ProfileSingleView.path)
                {
                    // Use same filter on comparison right view if its the same file
                    if (m_DepthFilter2Auto)
                    {
                        // When auto selected we have to set the left filter to new depth - diff
                        int newDepthFilter1 = (m_DepthFilter == ProfileAnalyzer.kDepthAll) ? ProfileAnalyzer.kDepthAll : m_ProfileLeftView.ClampToValidDepthValue(m_DepthFilter - m_DepthDiff);
                        if (m_DepthFilter1 != newDepthFilter1)
                        {
                            m_DepthFilter1 = newDepthFilter1;
                            changed = true;
                        }

                        if (UpdateAutoDepthFilter())
                            changed = true;
                    }
                    else
                    {
                        if (m_DepthFilter2 != m_DepthFilter)
                        {
                            m_DepthFilter2 = m_DepthFilter;
                            changed = true;
                        }
                    }
                }
            }

            return changed;
        }

        internal void SetThreadSelection(ThreadSelection threadSelection)
        {
            m_ThreadSelectionNew = new ThreadSelection(threadSelection);
            UpdateActiveTab(true);
        }

        string CalculateSelectedThreadsSummary()
        {
            if (m_ThreadSelection.selection == null || m_ThreadSelection.selection.Count == 0)
                return "None";

            // Count all threads in a group
            var threadDict = new Dictionary<string, int>();
            var threadSelectionDict = new Dictionary<string, int>();
            foreach (var threadNameWithIndex in m_ThreadNames)
            {
                var threadIdentifier = new ThreadIdentifier(threadNameWithIndex);
                if (threadIdentifier.index == ThreadIdentifier.kAll)
                    continue;

                int count;
                if (threadDict.TryGetValue(threadIdentifier.name, out count))
                    threadDict[threadIdentifier.name] = count + 1;
                else
                    threadDict[threadIdentifier.name] = 1;

                threadSelectionDict[threadIdentifier.name] = 0;
            }

            // Count all the threads we have 'selected' in a group
            foreach (var threadNameWithIndex in m_ThreadSelection.selection)
            {
                var threadIdentifier = new ThreadIdentifier(threadNameWithIndex);

                if (threadDict.ContainsKey(threadIdentifier.name) &&
                    threadSelectionDict.ContainsKey(threadIdentifier.name) &&
                    threadIdentifier.index <= threadDict[threadIdentifier.name])
                {
                    // Selected thread valid and in the thread list
                    // and also within the range of valid threads for this data set
                    threadSelectionDict[threadIdentifier.name]++;
                }
            }

            // Count all thread groups where we have 'selected all the threads'
            int threadsSelected = 0;
            foreach (var threadName in threadDict.Keys)
            {
                if (threadSelectionDict[threadName] != threadDict[threadName])
                    continue;

                threadsSelected++;
            }

            // If we've just added all the thread names we have everything selected
            // Note we don't compare against the m_ThreadNames directly as this contains the 'all' versions
            if (threadsSelected == threadDict.Keys.Count)
                return "All";

            // Add all the individual threads were we haven't already added the group
            List<string> threads = new List<string>();
            foreach (var threadName in threadSelectionDict.Keys)
            {
                int selectionCount = threadSelectionDict[threadName];
                if (selectionCount <= 0)
                    continue;
                int threadCount = threadDict[threadName];
                if (threadCount == 1)
                    threads.Add(threadName);
                else if (selectionCount != threadCount)
                    threads.Add(string.Format("{0} ({1} of {2})", threadName, selectionCount, threadCount));
                else
                    threads.Add(string.Format("{0} (All)", threadName));
            }

            // Maintain alphabetical order
            threads.Sort(CompareUINames);

            if (threads.Count == 0)
                return "None";

            string threadsSelectedText = string.Join(", ", threads.ToArray());
            return threadsSelectedText;
        }

        string GetSelectedThreadsSummary()
        {
            return m_ThreadSelectionSummary;
        }

        void DrawThreadFilter(ProfileData profileData)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Styles.threadFilter, GUILayout.Width(LayoutSize.FilterOptionsLeftLabelWidth));
            if (profileData != null)
            {
                if (m_ThreadNames.Count > 0)
                {
                    bool lastEnabled = GUI.enabled;
                    bool enabled = !IsAnalysisRunning() && !ThreadSelectionWindow.IsOpen();
                    GUI.enabled = enabled;
                    if (GUILayout.Button(Styles.threadFilterSelect, GUILayout.Width(LayoutSize.FilterOptionsEnumWidth)))
                    {
                        // Note: Window auto closes as it loses focus so this isn't strictly required
                        if (ThreadSelectionWindow.IsOpen())
                        {
                            ThreadSelectionWindow.CloseAll();
                        }
                        else
                        {
                            Vector2 windowPosition = new Vector2(Event.current.mousePosition.x + LayoutSize.FilterOptionsEnumWidth, Event.current.mousePosition.y + GUI.skin.label.lineHeight);
                            Vector2 screenPosition = GUIUtility.GUIToScreenPoint(windowPosition);

                            ThreadSelectionWindow.Open(screenPosition.x, screenPosition.y, this, m_ThreadSelection, m_ThreadNames, m_ThreadUINames);
                        }
                    }

                    GUI.enabled = lastEnabled;
                    ShowSelectedThreads();
                    GUILayout.FlexibleSpace();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        void DrawSelectedText(string text)
        {
            if (text == null)
                return;

#if UNITY_2019_1_OR_NEWER
            GUIStyle treeViewSelectionStyle = "TV Selection";
            GUIStyle backgroundStyle = new GUIStyle(treeViewSelectionStyle);

            GUIStyle treeViewLineStyle = "TV Line";
            GUIStyle textStyle = new GUIStyle(treeViewLineStyle);
#else
            GUIStyle textStyle = GUI.skin.label;
#endif

            GUIContent content = new GUIContent(text, text);
            Vector2 size = textStyle.CalcSize(content);
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.MaxWidth(size.x), GUILayout.Height(size.y));
            if (Event.current.type == EventType.Repaint)
            {
#if UNITY_2019_1_OR_NEWER
                backgroundStyle.Draw(rect, false, false, true, true);
#endif
                GUI.Label(rect, content, textStyle);
            }
        }

        void ShowSelectedThreads()
        {
            string threadsSelected = GetSelectedThreadsSummary();

            DrawSelectedText(threadsSelected);
        }

        void DrawUnitFilter()
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Width(LayoutSize.FilterOptionsRightLabelWidth + LayoutSize.FilterOptionsRightEnumWidth));
            EditorGUILayout.LabelField(Styles.unitFilter, m_StyleMiddleRight, GUILayout.Width(LayoutSize.FilterOptionsRightEnumWidth));

            bool lastEnabled = GUI.enabled;
            bool enabled = !IsAnalysisRunning();
            GUI.enabled = enabled;
            //Units units = (Units)EditorGUILayout.EnumPopup(m_DisplayUnits.Units, GUILayout.Width(LayoutSize.FilterOptionsRightEnumWidth));
            Units units = (Units)EditorGUILayout.Popup((int)m_DisplayUnits.Units, m_UnitNames, GUILayout.Width(LayoutSize.FilterOptionsRightEnumWidth));

            GUI.enabled = lastEnabled;
            if (units != m_DisplayUnits.Units)
            {
                SetUnits(units);
                m_FrameTimeGraph.SetUnits(m_DisplayUnits.Units);
                m_LeftFrameTimeGraph.SetUnits(m_DisplayUnits.Units);
                m_RightFrameTimeGraph.SetUnits(m_DisplayUnits.Units);
                UpdateMarkerTable();
            }
            EditorGUILayout.EndHorizontal();
        }

        bool IsSelfTime()
        {
            return (m_TimingOption == TimingOptions.TimingOption.Self) ? true : false;
        }

        void DrawTimingFilter()
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Width(LayoutSize.FilterOptionsRightLabelWidth + LayoutSize.FilterOptionsRightEnumWidth));

            EditorGUILayout.LabelField(Styles.timingFilter, m_StyleMiddleRight, GUILayout.Width(LayoutSize.FilterOptionsRightLabelWidth));

            bool lastEnabled = GUI.enabled;
            bool enabled = !IsAnalysisRunning();
            GUI.enabled = enabled;
            var timingOption = (TimingOptions.TimingOption)EditorGUILayout.Popup((int)m_TimingOption, TimingOptions.TimingOptionNames, GUILayout.Width(LayoutSize.FilterOptionsRightEnumWidth));
            GUI.enabled = lastEnabled;
            if (timingOption != m_TimingOption)
            {
                m_TimingOption = timingOption;
                UpdateActiveTab(true, true);
            }
            EditorGUILayout.EndHorizontal();
        }

        internal string GetDisplayUnits()
        {
            return m_DisplayUnits.Postfix();
        }

        internal string ToDisplayUnits(float ms, bool showUnits = false, int limitToDigits = 5, bool showFullValueWhenBelowZero = false)
        {
            return m_DisplayUnits.ToString(ms, showUnits, limitToDigits, showFullValueWhenBelowZero);
        }

        internal string ToDisplayUnits(double ms, bool showUnits = false, int limitToDigits = 5, bool showFullValueWhenBelowZero = false)
        {
            return m_DisplayUnits.ToString((float)ms, showUnits, limitToDigits, showFullValueWhenBelowZero);
        }

        void SetUnits(Units units)
        {
            m_DisplayUnits = new DisplayUnits(units);
        }

        void UpdateActiveTab(bool fullAnalysisRequired = false, bool markOtherDirty = true)
        {
            switch (m_ActiveTab)
            {
                case ActiveTab.Summary:
                    m_RequestAnalysis = true;
                    m_FullAnalysisRequired = fullAnalysisRequired;
                    break;
                case ActiveTab.Compare:
                    m_RequestCompare = true;
                    m_FullCompareRequired = fullAnalysisRequired;
                    break;
            }

            if (markOtherDirty)
                m_OtherTabDirty = true;
        }

        void UpdateMarkerTable(bool markOtherDirty = true)
        {
            switch (m_ActiveTab)
            {
                case ActiveTab.Summary:
                    if (m_ProfileTable != null)
                        m_ProfileTable.Reload();
                    break;
                case ActiveTab.Compare:
                    if (m_ComparisonTable != null)
                        m_ComparisonTable.Reload();
                    break;
            }

            if (markOtherDirty)
                m_OtherTableDirty = true;
        }

        void DrawNameFilter()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Styles.nameFilter, GUILayout.Width(LayoutSize.FilterOptionsLeftLabelWidth));

            NameFilterOperation lastNameFilterOperation = m_NameFilterOperation;

            bool lastEnabled = GUI.enabled;
            bool enabled = !IsAnalysisRunning();
            GUI.enabled = enabled;
            m_NameFilterOperation = (NameFilterOperation)EditorGUILayout.Popup((int)m_NameFilterOperation, Styles.nameFilterOperation, GUILayout.MaxWidth(LayoutSize.FilterOptionsEnumWidth));
            GUI.enabled = lastEnabled;

            if (m_NameFilterOperation != lastNameFilterOperation)
            {
                UpdateMarkerTable();
            }
            string lastFilter = m_NameFilter;
            GUI.enabled = enabled;
            GUI.SetNextControlName("NameFilter");
            m_NameFilter = EditorGUILayout.TextField(m_NameFilter, GUILayout.MinWidth(200 - LayoutSize.FilterOptionsEnumWidth));
            GUI.enabled = lastEnabled;
            if (m_NameFilter != lastFilter)
            {
                UpdateMarkerTable();
            }

            EditorGUILayout.LabelField(Styles.nameExclude, GUILayout.Width(LayoutSize.FilterOptionsLeftLabelWidth));
            NameFilterOperation lastNameExcludeOperation = m_NameExcludeOperation;
            GUI.enabled = enabled;
            m_NameExcludeOperation = (NameFilterOperation)EditorGUILayout.Popup((int)m_NameExcludeOperation, Styles.nameFilterOperation, GUILayout.MaxWidth(LayoutSize.FilterOptionsEnumWidth));
            GUI.enabled = lastEnabled;
            if (m_NameExcludeOperation != lastNameExcludeOperation)
            {
                UpdateMarkerTable();
            }
            string lastExclude = m_NameExclude;
            GUI.enabled = enabled;
            GUI.SetNextControlName("ExcludeFilter");
            m_NameExclude = EditorGUILayout.TextField(m_NameExclude, GUILayout.MinWidth(200 - LayoutSize.FilterOptionsEnumWidth));
            GUI.enabled = lastEnabled;
            if (m_NameExclude != lastExclude)
            {
                UpdateMarkerTable();
            }
            EditorGUILayout.EndHorizontal();
        }

        internal void SetMode(MarkerColumnFilter.Mode newMode)
        {
            m_SingleModeFilter.mode = newMode;
            m_CompareModeFilter.mode = newMode;

            if (m_ProfileTable != null)
                m_ProfileTable.SetMode(m_SingleModeFilter);
            if (m_ComparisonTable != null)
                m_ComparisonTable.SetMode(m_CompareModeFilter);
        }

        internal void SetSingleModeColumns(int[] visibleColumns)
        {
            // If selecting the columns manually then override the currently stored selection with the current
            m_ProfileMulticolumnHeaderState.visibleColumns = visibleColumns;

            m_SingleModeFilter.mode = MarkerColumnFilter.Mode.Custom;
            m_SingleModeFilter.visibleColumns = visibleColumns;

            // Also move other view to the custom set
            m_CompareModeFilter.mode = MarkerColumnFilter.Mode.Custom;
        }
        internal void SetComparisonModeColumns(int[] visibleColumns)
        {
            // If selecting the columns manually then override the currently stored selection with the current
            m_ComparisonMulticolumnHeaderState.visibleColumns = visibleColumns;

            m_CompareModeFilter.mode = MarkerColumnFilter.Mode.Custom;
            m_CompareModeFilter.visibleColumns = visibleColumns;

            // Also move other view to the custom set
            m_SingleModeFilter.mode = MarkerColumnFilter.Mode.Custom;
        }

        void DrawMarkerColumnFilter()
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Width(LayoutSize.FilterOptionsRightLabelWidth + LayoutSize.FilterOptionsRightEnumWidth));
            EditorGUILayout.LabelField(Styles.markerColumns, m_StyleMiddleRight, GUILayout.Width(LayoutSize.FilterOptionsRightLabelWidth));

            bool lastEnabled = GUI.enabled;
            bool enabled = !IsAnalysisRunning();
            GUI.enabled = enabled;
            MarkerColumnFilter.Mode newMode = (MarkerColumnFilter.Mode)EditorGUILayout.IntPopup((int)m_SingleModeFilter.mode, MarkerColumnFilter.ModeNames, MarkerColumnFilter.ModeValues, GUILayout.Width(LayoutSize.FilterOptionsRightEnumWidth));
            GUI.enabled = lastEnabled;
            if (newMode != m_SingleModeFilter.mode)
            {
                SetMode(newMode);
            }

            EditorGUILayout.EndHorizontal();
        }
        enum InDataSet
        {
            Left,
            Both,
            Right
        };

        int GetCombinedThreadCount(out int matchingCount, out int uniqueLeft, out int uniqueRight)
        {
            var threads = new Dictionary<string, InDataSet>();
            foreach (var threadName in m_ProfileLeftView.data.GetThreadNames())
            {
                threads[threadName] = InDataSet.Left;
            }
            foreach (var threadName in m_ProfileRightView.data.GetThreadNames())
            {
                if (threads.ContainsKey(threadName))
                    threads[threadName] = InDataSet.Both;
                else
                    threads[threadName] = InDataSet.Right;
            }

            matchingCount = 0;
            uniqueLeft = 0;
            uniqueRight = 0;
            int total = 0;
            foreach (var thread in threads)
            {
                switch (thread.Value)
                {
                    case InDataSet.Left:
                        uniqueLeft++;
                        break;
                    case InDataSet.Both:
                        matchingCount++;
                        break;
                    case InDataSet.Right:
                        uniqueRight++;
                        break;
                }
                total++;
            }

            return total;
        }

        void DrawMarkerCount()
        {
            if (!IsAnalysisValid())
                return;

            if (m_ActiveTab == ActiveTab.Summary)
            {
                int markersCount = m_ProfileSingleView.analysis.GetFrameSummary().totalMarkers;
                int filteredMarkersCount = (m_ProfileTable != null) ? m_ProfileTable.GetRows().Count : 0;

                var content = new GUIContent(
                    String.Format("{0} of {1} markers", filteredMarkersCount, markersCount),
                    "Number of markers in the filtered set, compared to the total in the data set");
                Vector2 size = GUI.skin.label.CalcSize(content);
                Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(size.x), GUILayout.Height(size.y));
                EditorGUI.LabelField(rect, content);
            }
            if (m_ActiveTab == ActiveTab.Compare)
            {
                int markersCount = m_TotalCombinedMarkerCount;
                int filteredMarkersCount = (m_ComparisonTable != null) ? m_ComparisonTable.GetRows().Count : 0;

                var content = new GUIContent(
                    String.Format("{0} of {1} markers", filteredMarkersCount, markersCount),
                    "Number of markers in the filtered set, compared to total unique markers in the combined data sets");
                Vector2 size = GUI.skin.label.CalcSize(content);
                Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(size.x), GUILayout.Height(size.y));
                EditorGUI.LabelField(rect, content);
            }
        }

        string GetThreadCountToolTipUnion(int allThreadsCount, int matchingCount)
        {
            return String.Format(
                "Total\n{0} Union : Combined over both data sets\n{1} Intersection : Matching in both data sets",
                allThreadsCount,
                matchingCount
                );
        }
        string GetThreadCountToolTipDifference(int allThreadsCount, int matchingCount, int uniqueLeft, int uniqueRight)
        {
            return String.Format(
                "Difference\n{0}\n{1} Unique to left\n{2} Unique to right",
                allThreadsCount - matchingCount,
                uniqueLeft,
                uniqueRight);
        }
        string GetThreadCountToolTip(int allThreadsCount, int matchingCount, int uniqueLeft, int uniqueRight)
        {
            return String.Format(
                "{0}\n\n{1}",
                GetThreadCountToolTipUnion(allThreadsCount, matchingCount),
                GetThreadCountToolTipDifference(allThreadsCount, matchingCount, uniqueLeft, uniqueRight)
                );
        }

        void DrawThreadCount()
        {
            if (!IsAnalysisValid())
                return;

            if (m_ActiveTab == ActiveTab.Summary)
            {
                int allThreadsCount = m_ProfileSingleView.data.GetThreadNames().Count;
                List<string> threadSelection = GetLimitedThreadSelection(m_ThreadNames, m_ThreadSelection);
                int selectedThreads = threadSelection.Count;

                var content = new GUIContent(
                    String.Format("{0} of {1} threads", selectedThreads, allThreadsCount),
                    "Number of threads in the filtered set, compared to the total in the data set");
                Vector2 size = GUI.skin.label.CalcSize(content);
                Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(size.x), GUILayout.Height(size.y));
                EditorGUI.LabelField(rect, content);
            }
            if (m_ActiveTab == ActiveTab.Compare)
            {
                int matchingCount, uniqueLeft, uniqueRight;
                int allThreadsCount = GetCombinedThreadCount(out matchingCount, out uniqueLeft, out uniqueRight);
                List<string> threadSelection = GetLimitedThreadSelection(m_ThreadNames, m_ThreadSelection);
                int selectedThreads = threadSelection.Count;

                string partialTooltip = GetThreadCountToolTip(allThreadsCount, matchingCount, uniqueLeft, uniqueRight);

                var content = new GUIContent(
                    String.Format("{0} of {1} threads", selectedThreads, allThreadsCount),
                    String.Format("Number of threads in the filtered set, compared to total unique threads in the combined data sets\n\n{0}", partialTooltip)
                    );
                Vector2 size = GUI.skin.label.CalcSize(content);
                Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(size.x), GUILayout.Height(size.y));
                EditorGUI.LabelField(rect, content);
            }
        }

        void DrawAnalysisOptions()
        {
            Profiler.BeginSample("ProfileAnalyzer.DrawAnalysisOptions");

            EditorGUILayout.BeginVertical(GUI.skin.box);

            bool lastShowFilters = m_ShowFilters;
            m_ShowFilters = BoldFoldout(m_ShowFilters, Styles.filters);
            var analytic = ProfileAnalyzerAnalytics.BeginAnalytic();
            if (m_ShowFilters)
            {
                DrawNameFilter();
                EditorGUILayout.BeginHorizontal();
                DrawThreadFilter(m_ProfileSingleView.data);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                DrawDepthFilter();
                DrawTimingFilter();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                DrawParentFilter();
                DrawUnitFilter();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                bool lastEnabled = GUI.enabled;
                GUI.enabled = !IsAnalysisRunning();
                if (GUILayout.Button(new GUIContent("Analyze", m_LastAnalysisTime), GUILayout.Width(100)))
                    m_RequestAnalysis = true;
                GUI.enabled = lastEnabled;
                DrawMarkerCount();
                EditorGUILayout.LabelField(",", GUILayout.Width(10), GUILayout.ExpandWidth(false));
                DrawThreadCount();
                DrawProgress();
                GUILayout.FlexibleSpace();
                DrawMarkerColumnFilter();
                EditorGUILayout.EndHorizontal();
            }

            if (m_ShowFilters != lastShowFilters)
            {
                ProfileAnalyzerAnalytics.SendUIVisibilityEvent(ProfileAnalyzerAnalytics.UIVisibility.Filters, analytic.GetDurationInSeconds(), m_ShowFilters);
            }

            EditorGUILayout.EndVertical();

            Profiler.EndSample();
        }

        internal bool IsAnalysisRunning()
        {
            if (m_ThreadActivity != ThreadActivity.None)
                return true;

            return false;
        }

        int GetProgress()
        {
            // We return the value from the update loop so the data doesn't change over the time onGui is called for layout and repaint
            return m_ThreadProgress;
        }

        bool IsAnalysisValid(bool checkFrameCount = false)
        {
            switch (m_ActiveTab)
            {
                case ActiveTab.Summary:
                    if (!m_ProfileSingleView.IsDataValid())
                        return false;

                    if (m_ProfileSingleView.analysis == null)
                        return false;

                    if (checkFrameCount)
                    {
                        if (m_ProfileSingleView.analysis.GetFrameSummary().frames.Count <= 0)
                            return false;
                    }
                    break;

                case ActiveTab.Compare:
                    if (!m_ProfileLeftView.IsDataValid())
                        return false;
                    if (!m_ProfileRightView.IsDataValid())
                        return false;

                    if (m_ProfileLeftView.analysis == null)
                        return false;
                    if (m_ProfileRightView.analysis == null)
                        return false;

                    if (checkFrameCount)
                    {
                        if (m_ProfileLeftView.analysis.GetFrameSummary().frames.Count <= 0)
                            return false;
                        if (m_ProfileRightView.analysis.GetFrameSummary().frames.Count <= 0)
                            return false;
                    }
                    break;
            }

            //if (IsAnalysisRunning())
            //    return false;

            return true;
        }

        void DrawProgress()
        {
            if (IsAnalysisRunning())
            {
                int progress = GetProgress();

                EditorGUILayout.BeginHorizontal(GUILayout.MaxWidth(140));
                float x = 0;
                float y = 0;
                float width = 100;
                float height = GUI.skin.label.lineHeight;
                if (m_2D.DrawStart(width, height, Draw2D.Origin.TopLeft, GUI.skin.label))
                {
                    float barLength = (width * progress) / 100;
                    m_2D.DrawFilledBox(x, y, barLength, height, UIColor.white);

                    m_2D.DrawEnd();
                }
                EditorGUILayout.LabelField(string.Format("{0}%", progress), GUILayout.MaxWidth(40));
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField("", GUILayout.Width(140));
            }
        }

        void DrawPullButton(Color color, ProfileDataView view, FrameTimeGraph frameTimeGraph)
        {
            bool lastEnabled = GUI.enabled;
            GUI.enabled = !IsAnalysisRunning();

            GUIContent content;
            if (!IsProfilerWindowOpen())
            {
                content = Styles.pullOpen;
                GUI.enabled = false;
            }
            else if (m_PullFirstFrameindex == 0 && m_PullLastFrameindex == 0)
            {
                content = Styles.pullRange;
                GUI.enabled = false;
            }
            /*
            // Commented out so we can capture even if recording            
            else if (m_ProfilerWindowInterface.IsRecording())
            {
                content = Styles.pullRecording;
                GUI.enabled = false;
            }
            */
            else
            {
                content = Styles.pull;
            }

            Color oldColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            bool pull = GUILayout.Button(content, GUILayout.Width(100));
            GUI.backgroundColor = oldColor;
            if (pull)
            {
                PullFromProfiler(m_PullFirstFrameindex, m_PullLastFrameindex, view, frameTimeGraph);
                UpdateActiveTab(true, false);
            }
            GUI.enabled = lastEnabled;
        }

        void DrawFilesLoaded()
        {
            Profiler.BeginSample("ProfileAnalyzer.DrawFilesLoaded");

            EditorGUILayout.BeginVertical(GUI.skin.box);

            if (m_ActiveTab == ActiveTab.Summary)
            {
                EditorGUILayout.BeginHorizontal(GUILayout.Height(100 + GUI.skin.label.lineHeight + (2 * (GUI.skin.label.margin.vertical + GUI.skin.label.padding.vertical))));

                float filenameWidth = GetFilenameWidth(m_ProfileSingleView.path);
                filenameWidth = Math.Min(filenameWidth, 200);
                EditorGUILayout.BeginVertical(GUILayout.MaxWidth(100 + filenameWidth), GUILayout.ExpandWidth(false));
                DrawPullButton(GUI.backgroundColor, m_ProfileSingleView, m_FrameTimeGraph);
                DrawLoadSave();
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                DrawFrameTimeGraph(100);
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
            }

            if (m_ActiveTab == ActiveTab.Compare)
            {
                DrawComparisonLoadSave();
            }

            EditorGUILayout.EndVertical();

            Profiler.EndSample();
        }

        void ShowHelp()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            m_HelpScroll = EditorGUILayout.BeginScrollView(m_HelpScroll, GUILayout.ExpandHeight(true));
#if UNITY_2019_3_OR_NEWER
            GUILayout.TextArea(Styles.helpText);
#else
            GUIStyle helpStyle = new GUIStyle(EditorStyles.textField);
            helpStyle.wordWrap = true;
            EditorGUILayout.LabelField(Styles.helpText, helpStyle);
#endif
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        void DrawAnalysis()
        {
            Profiler.BeginSample("ProfileAnalyzer.DrawAnalysis");

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();

            DrawFilesLoaded();

            if (m_ProfileSingleView.IsDataValid() && m_ProfileSingleView.data.GetFrameCount() > 0)
            {
                DrawAnalysisOptions();

                if (IsAnalysisValid())
                {
                    EditorGUILayout.BeginVertical(GUI.skin.box);

                    string title = string.Format("Top {0} markers on median frame", m_TopNBars);
                    GUIContent markersTitle = new GUIContent(title, Styles.topMarkersTooltip);
                    bool lastShowTopMarkers = m_ShowTopNMarkers;
                    m_ShowTopNMarkers = BoldFoldout(m_ShowTopNMarkers, markersTitle);
                    var analytic = ProfileAnalyzerAnalytics.BeginAnalytic();
                    if (m_ShowTopNMarkers)
                    {
                        Profiler.BeginSample("ProfileAnalyzer.TopNMarkers");
                        m_TopMarkers.SetData(m_ProfileSingleView.analysis, m_DepthFilter, GetNameFilters(), GetNameExcludes());

                        EditorGUILayout.BeginVertical(GUILayout.Height(20));

                        EditorGUILayout.BeginHorizontal();
                        FrameSummary frameSummary = m_ProfileSingleView.analysis.GetFrameSummary();
                        if (frameSummary.count > 0)
                            DrawFrameIndexButton(frameSummary.medianFrameIndex);
                        else
                            GUILayout.Label("", GUILayout.MinWidth(50));

                        Rect rect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                        float range = m_TopMarkers.GetTopMarkerTimeRange();
                        m_TopMarkers.Draw(rect, UIColor.bar, m_TopNBars, range, UIColor.barBackground, Color.black, Color.white, true, true);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();

                        EditorGUILayout.BeginVertical(GUILayout.Height(20));
                        GUIContent info;
                        if (m_DepthFilter == ProfileAnalyzer.kDepthAll)
                            info = new GUIContent("(All depths shown)", string.Format("{0}\n\nSet depth 1 to get an overview of the frame", Styles.medianFrameTooltip));
                        else
                            info = new GUIContent(string.Format("(Filtered to depth '{0}' only)", m_DepthFilter), Styles.medianFrameTooltip);
                        GUILayout.Label(info);

                        if (IsSingleMode())
                        {
                            int idleCount = 0;
                            if (m_ProfileSingleView.analysisNew != null)
                            {
                                idleCount = m_ProfileSingleView.analysisNew.IdleTimesInSelectThreadFrame;
                            }

                            string markerTip = string.Empty;
                            if (!string.IsNullOrEmpty(m_SelectedMarkerName))
                            {
                                float selectMarkerMs = 0.0f;
                                if (m_ProfileSingleView.analysis != null)
                                {
                                    selectMarkerMs = m_ProfileSingleView.analysis.SelectMarkerTimes;
                                }

                                string avgString = string.Empty;
                                if (m_ProfileSingleView.selectedIndices.Count > 0)
                                {
                                    avgString = $",avg:{selectMarkerMs / m_ProfileSingleView.selectedIndices.Count}";
                                }

                                markerTip = $",{m_SelectedMarkerName}:{selectMarkerMs}{avgString}";
                            }


                            GUIContent idleCountInfo = new GUIContent($"idleCount:{idleCount}{markerTip}", "idle count in selected thread");
                            GUILayout.Label(idleCountInfo);
                        }

                        EditorGUILayout.EndVertical();

                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label("");
                        EditorGUILayout.EndHorizontal();

                        Profiler.EndSample();
                    }

                    if (m_ShowTopNMarkers != lastShowTopMarkers)
                    {
                        ProfileAnalyzerAnalytics.SendUIVisibilityEvent(ProfileAnalyzerAnalytics.UIVisibility.TopTen, analytic.GetDurationInSeconds(), m_ShowTopNMarkers);
                    }

                    EditorGUILayout.EndVertical();

                    if (m_ProfileTable != null)
                    {
                        m_ShowMarkerTable = BoldFoldout(m_ShowMarkerTable, Styles.profileTable);
                        if (m_ShowMarkerTable)
                        {
                            Profiler.BeginSample("ProfileAnalyzer.DrawMarkerTable");
                            Rect r = EditorGUILayout.GetControlRect(GUILayout.ExpandHeight(true));

                            float scrollBarWidth = GUI.skin.verticalScrollbar.fixedWidth + GUI.skin.verticalScrollbar.border.horizontal + GUI.skin.verticalScrollbar.margin.horizontal + GUI.skin.verticalScrollbar.padding.horizontal;
                            scrollBarWidth += 6;
                            Rect clipRect = new Rect(r.x, 0, r.width - scrollBarWidth, 32767);
                            m_2D.SetClipRect(clipRect);

                            m_ProfileTable.OnGUI(r);
                            Profiler.EndSample();

                            m_2D.ClearClipRect();
                        }
                    }
                }
            }
            else
            {
                ShowHelp();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.Width(LayoutSize.WidthRHS));
            GUILayout.Space(4);
            DrawFrameSummary();
            DrawThreadSummary();
            DrawSelected();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            Profiler.EndSample();
        }

        void SetRange(List<int> selectedOffsets, int clickCount, bool singleControlAction, FrameTimeGraph.State inputStatus, ProfileData mainData, List<int> selectedIndices)
        {
            if (inputStatus == FrameTimeGraph.State.Dragging)
                return;

            if (clickCount == 2)
            {
                int index = mainData.OffsetToDisplayFrame(selectedOffsets[0]);
                JumpToFrame(index, false);
            }
            else
            {
                selectedIndices.Clear();
                foreach (int offset in selectedOffsets)
                {
                    selectedIndices.Add(mainData.OffsetToDisplayFrame(offset));
                }
                // Keep indices sorted
                selectedIndices.Sort();

                m_RequestCompare = true;
            }
        }

        void SetLeftRange(List<int> selectedOffsets, int clickCount, bool singleControlAction, FrameTimeGraph.State inputStatus)
        {
            SetRange(selectedOffsets, clickCount, singleControlAction, inputStatus, m_ProfileLeftView.data, m_ProfileLeftView.selectedIndices);
        }

        void SetRightRange(List<int> selectedOffsets, int clickCount, bool singleControlAction, FrameTimeGraph.State inputStatus)
        {
            SetRange(selectedOffsets, clickCount, singleControlAction, inputStatus, m_ProfileRightView.data, m_ProfileRightView.selectedIndices);
        }

        void DrawComparisonLoadSaveButton(Color color, ProfileDataView view, FrameTimeGraph frameTimeGraph)
        {
            bool lastEnabled = GUI.enabled;
            bool enabled = !IsAnalysisRunning();

            EditorGUILayout.BeginHorizontal(GUILayout.MaxWidth(300), GUILayout.ExpandWidth(false));

            GUIStyle buttonStyle = GUI.skin.button;
            Color oldColor = GUI.backgroundColor;

            GUI.backgroundColor = color;
            GUI.enabled = enabled;
            bool load = GUILayout.Button("Load", buttonStyle, GUILayout.ExpandWidth(false), GUILayout.Width(50));
            GUI.enabled = lastEnabled;
            GUI.backgroundColor = oldColor;
            if (load)
            {
                string newPath = EditorUtility.OpenFilePanel("Load profile analyzer data file", "", "pdata");
                if (newPath.Length != 0)
                {
                    ProfileData newData;
                    if (ProfileData.Load(newPath, out newData))
                    {
                        SetView(view, newData, newPath, frameTimeGraph);

                        // Remove pairing if both left/right point at the same data
                        if (m_ProfileLeftView.path == m_ProfileRightView.path)
                        {
                            SetFrameTimeGraphPairing(false);
                        }

                        m_FullCompareRequired = true;
                        m_RequestCompare = true;
                    }
                }
            }

            GUI.backgroundColor = color;
            GUI.enabled = enabled;
            bool save = GUILayout.Button("Save", buttonStyle, GUILayout.ExpandWidth(false), GUILayout.Width(50));
            GUI.enabled = lastEnabled;
            GUI.backgroundColor = oldColor;
            if (save)
            {
                string newPath = EditorUtility.SaveFilePanel("Save profile analyzer data file", "", "capture.pdata", "pdata");
                if (newPath.Length != 0)
                {
                    view.path = newPath;
                    if (ProfileData.Save(newPath, view.data))
                    {
                        UpdateMatchingProfileData(view.data, ref view.path, view.analysis, newPath);
                    }
                }
            }

            ShowFilename(view.path);

            EditorGUILayout.EndHorizontal();
        }

        float GetComparisonYRange()
        {
            float yRangeLeft = m_ProfileLeftView.IsDataValid() ? m_LeftFrameTimeGraph.GetDataRange() : 0f;
            float yRangeRight = m_ProfileRightView.IsDataValid() ? m_RightFrameTimeGraph.GetDataRange() : 0f;
            float yRange = Math.Max(yRangeLeft, yRangeRight);

            return yRange;
        }

        void SetFrameTimeGraphPairing(bool paired)
        {
            if (paired != m_FrameTimeGraphsPaired)
            {
                m_FrameTimeGraphsPaired = paired;
                m_LeftFrameTimeGraph.PairWith(m_FrameTimeGraphsPaired ? m_RightFrameTimeGraph : null);
            }
        }

        void DrawComparisonLoadSave()
        {
            GUIStyle buttonStyle = GUI.skin.button;

            Color oldColor = GUI.backgroundColor;

            int leftFrames = m_ProfileLeftView.IsDataValid() ? m_ProfileLeftView.data.GetFrameCount() : 0;
            int rightFrames = m_ProfileRightView.IsDataValid() ? m_ProfileRightView.data.GetFrameCount() : 0;
            int maxFrames = Math.Max(leftFrames, rightFrames);

            float yRange = GetComparisonYRange();

            EditorGUILayout.BeginHorizontal(GUILayout.Height(100 + GUI.skin.label.lineHeight + (2 * (GUI.skin.label.margin.vertical + GUI.skin.label.padding.vertical))));

            float leftFilenameWidth = GetFilenameWidth(m_ProfileLeftView.path);
            float rightFilenameWidth = GetFilenameWidth(m_ProfileRightView.path);
            float filenameWidth = Math.Max(leftFilenameWidth, rightFilenameWidth);
            filenameWidth = Math.Min(filenameWidth, 200);

            EditorGUILayout.BeginVertical(GUILayout.MaxWidth(100 + filenameWidth), GUILayout.ExpandWidth(false));
            DrawPullButton(UIColor.left, m_ProfileLeftView, m_LeftFrameTimeGraph);
            DrawComparisonLoadSaveButton(UIColor.left, m_ProfileLeftView, m_LeftFrameTimeGraph);
            DrawPullButton(UIColor.right, m_ProfileRightView, m_RightFrameTimeGraph);
            DrawComparisonLoadSaveButton(UIColor.right, m_ProfileRightView, m_RightFrameTimeGraph);
            EditorGUILayout.EndVertical();


            bool lastEnabled = GUI.enabled;
            bool enabled = !IsAnalysisRunning();

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            GUI.SetNextControlName("LeftFrameTimeGraph");
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Height(50));
            if (m_ProfileLeftView.IsDataValid())
            {
                if (!m_LeftFrameTimeGraph.HasData())
                    m_LeftFrameTimeGraph.SetData(GetFrameTimeData(m_ProfileLeftView.data));
                if (!m_ProfileLeftView.HasValidSelection())
                    m_ProfileLeftView.SelectFullRange();

                List<int> selectedOffsets = new List<int>();
                foreach (int index in m_ProfileLeftView.selectedIndices)
                {
                    selectedOffsets.Add(m_ProfileLeftView.data.DisplayFrameToOffset(index));
                }

                int displayOffset = m_ProfileLeftView.data.OffsetToDisplayFrame(0);
                m_LeftFrameTimeGraph.SetEnabled(enabled);
                m_LeftFrameTimeGraph.Draw(rect, m_ProfileLeftView.analysis, selectedOffsets, yRange, displayOffset, m_SelectedMarkerName, maxFrames, m_ProfileLeftView.analysisFull);
            }
            else
            {
                GUI.Label(rect, Styles.comparisonDataMissing, m_StyleUpperLeft);
            }

            GUI.SetNextControlName("RightFrameTimeGraph");
            rect = EditorGUILayout.GetControlRect(GUILayout.Height(50));
            if (m_ProfileRightView.IsDataValid())
            {
                if (!m_RightFrameTimeGraph.HasData())
                    m_RightFrameTimeGraph.SetData(GetFrameTimeData(m_ProfileRightView.data));
                if (!m_ProfileRightView.HasValidSelection())
                    m_ProfileRightView.SelectFullRange();

                List<int> selectedOffsets = new List<int>();
                foreach (int index in m_ProfileRightView.selectedIndices)
                {
                    selectedOffsets.Add(m_ProfileRightView.data.DisplayFrameToOffset(index));
                }

                int displayOffset = m_ProfileRightView.data.OffsetToDisplayFrame(0);
                m_RightFrameTimeGraph.SetEnabled(enabled);
                m_RightFrameTimeGraph.Draw(rect, m_ProfileRightView.analysis, selectedOffsets, yRange, displayOffset, m_SelectedMarkerName, maxFrames, m_ProfileRightView.analysisFull);
            }
            else
            {
                GUI.Label(rect, Styles.comparisonDataMissing, m_StyleUpperLeft);
            }

            EditorGUILayout.BeginHorizontal();
            if (m_ProfileLeftView.IsDataValid() && m_ProfileRightView.IsDataValid() && m_ProfileLeftView.data.GetFrameCount() > 0 && m_ProfileRightView.data.GetFrameCount() > 0)
            {
                GUIStyle lockButtonStyle = "IN LockButton";
                GUIStyle style = new GUIStyle(lockButtonStyle);
                style.padding.left = 20;
                //bool paired = GUILayout.Toggle(m_frameTimeGraphsPaired, Styles.graphPairing, style);
                GUI.enabled = enabled;
                bool paired = EditorGUILayout.ToggleLeft(Styles.graphPairing, m_FrameTimeGraphsPaired, style, GUILayout.MaxWidth(200));
                GUI.enabled = lastEnabled;
                SetFrameTimeGraphPairing(paired);

                GUILayout.FlexibleSpace();
                ShowSelectedMarker();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        void DrawComparisonHistogram(float height, float minValue, float maxValue, int bucketCount, int[] leftBuckets, int[] rightBuckets, int leftCount, int rightCount, bool leftValid, bool rightValid, DisplayUnits displayUnits)
        {
            Histogram histogram = new Histogram(m_2D, displayUnits.Units);
            float width = LayoutSize.HistogramWidth;
            float min = minValue;
            float max = maxValue;
            float spacing = 2;

            float range = max - min;
            // bucketCount = (range == 0f) ? 1 : bucketCount;

            float x = (spacing / 2);
            float y = 0;
            float w = ((width + spacing) / bucketCount) - spacing;
            float h = height;

            histogram.DrawStart(width);

            if (m_2D.DrawStart(width, height, Draw2D.Origin.BottomLeft))
            {
                float bucketWidth = (range / bucketCount);
                Rect rect = GUILayoutUtility.GetLastRect();

                histogram.DrawBackground(width, height, bucketCount, min, max, spacing);

                if (!IsAnalysisRunning())
                {
                    for (int bucketAt = 0; bucketAt < bucketCount; bucketAt++)
                    {
                        float leftBarCount = leftValid ? leftBuckets[bucketAt] : 0;
                        float rightBarCount = rightValid ? rightBuckets[bucketAt] : 0;
                        float leftBarHeight = leftValid ? ((h * leftBarCount) / leftCount) : 0;
                        float rightBarHeight = rightValid ? ((h * rightBarCount) / rightCount) : 0;

                        if (leftBarCount > 0)  // Make sure we always slow a small bar if non zero
                            leftBarHeight = Mathf.Max(1.0f, leftBarHeight);
                        if (rightBarCount > 0)  // Make sure we always slow a small bar if non zero
                            rightBarHeight = Mathf.Max(1.0f, rightBarHeight);

                        if ((int)rightBarHeight == (int)leftBarHeight)
                        {
                            m_2D.DrawFilledBox(x, y, w, leftBarHeight, UIColor.both);
                        }
                        else if (rightBarHeight > leftBarHeight)
                        {
                            m_2D.DrawFilledBox(x, y, w, rightBarHeight, UIColor.right);
                            m_2D.DrawFilledBox(x, y, w, leftBarHeight, UIColor.both);
                        }
                        else
                        {
                            m_2D.DrawFilledBox(x, y, w, leftBarHeight, UIColor.left);
                            m_2D.DrawFilledBox(x, y, w, rightBarHeight, UIColor.both);
                        }

                        float bucketStart = min + (bucketAt * bucketWidth);
                        float bucketEnd = bucketStart + bucketWidth;

                        string tooltip = string.Format(
                            "{0}-{1}\nLeft: {2} {3}\nRight: {4} {5}\n\nBar width: {6}",
                            displayUnits.ToString(bucketStart, false, 5),
                            displayUnits.ToString(bucketEnd, true, 5),
                            leftBarCount, leftBarCount == 1 ? "frame" : "frames",
                            rightBarCount, rightBarCount == 1 ? "frame" : "frames",
                            displayUnits.ToString(bucketWidth, true, 5, true));
                        GUI.Label(new Rect(rect.x + x, rect.y + y, w, h),
                                    new GUIContent("", tooltip)
                                    );

                        x += w;
                        x += spacing;
                    }
                }

                m_2D.DrawEnd();
            }

            histogram.DrawEnd(width, min, max, spacing);
        }

        void DrawComparisonFrameSummary()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(LayoutSize.WidthRHS));

            bool lastShowFrameSummary = m_ShowFrameSummary;
            m_ShowFrameSummary = BoldFoldout(m_ShowFrameSummary, Styles.frameSummary);
            var analytic = ProfileAnalyzerAnalytics.BeginAnalytic();
            if (m_ShowFrameSummary)
            {
                EditorGUILayout.BeginVertical(); // To match indenting on the marker summary where a scroll area is present

                if (IsAnalysisValid())
                {
                    var leftFrameSummary = m_ProfileLeftView.analysis.GetFrameSummary();
                    var rightFrameSummary = m_ProfileRightView.analysis.GetFrameSummary();

                    m_Columns.SetColumnSizes(LayoutSize.WidthColumn0, LayoutSize.WidthColumn1, LayoutSize.WidthColumn2, LayoutSize.WidthColumn3);
                    m_Columns.Draw4("", "Left", "Right", "Diff");

                    int diff = rightFrameSummary.count - leftFrameSummary.count;
                    m_Columns.Draw4(Styles.frameCount, GetFrameCountText(m_ProfileLeftView.analysis), GetFrameCountText(m_ProfileRightView.analysis), new GUIContent(diff.ToString(), ""));
                    m_Columns.Draw3(Styles.frameStart, GetFirstFrameText(m_ProfileLeftView.analysis), GetFirstFrameText(m_ProfileRightView.analysis));
                    m_Columns.Draw3(Styles.frameEnd, GetLastFrameText(m_ProfileLeftView.analysis), GetLastFrameText(m_ProfileRightView.analysis));

                    m_Columns.Draw(0, "");
                    string units = GetDisplayUnits();
                    m_Columns.Draw4("", units, units, units);

                    Draw4DiffMs(Styles.max, leftFrameSummary.msMax, leftFrameSummary.maxFrameIndex, rightFrameSummary.msMax, rightFrameSummary.maxFrameIndex);
                    Draw4DiffMs(Styles.upperQuartile, leftFrameSummary.msUpperQuartile, rightFrameSummary.msUpperQuartile);
                    Draw4DiffMs(Styles.median, leftFrameSummary.msMedian, leftFrameSummary.medianFrameIndex, rightFrameSummary.msMedian, rightFrameSummary.medianFrameIndex);
                    Draw4DiffMs(Styles.mean, leftFrameSummary.msMean, rightFrameSummary.msMean);
                    Draw4DiffMs(Styles.lowerQuartile, leftFrameSummary.msLowerQuartile, rightFrameSummary.msLowerQuartile);
                    Draw4DiffMs(Styles.min, leftFrameSummary.msMin, leftFrameSummary.minFrameIndex, rightFrameSummary.msMin, rightFrameSummary.minFrameIndex);

                    GUIStyle style = GUI.skin.label;
                    GUILayout.Space(style.lineHeight);

                    EditorGUILayout.BeginHorizontal();
                    int leftBucketCount = leftFrameSummary.buckets.Length;
                    int rightBucketCount = rightFrameSummary.buckets.Length;

                    float msFrameMax = Math.Max(leftFrameSummary.msMax, rightFrameSummary.msMax);
                    float yRange = msFrameMax;

                    if (leftBucketCount != rightBucketCount)
                    {
                        Debug.Log("Error left frame summary bucket count doesn't equal right summary");
                    }
                    else
                    {
                        DrawComparisonHistogram(40, 0, yRange, leftBucketCount, leftFrameSummary.buckets, rightFrameSummary.buckets, leftFrameSummary.count, rightFrameSummary.count, true, true, m_DisplayUnits);
                    }

                    BoxAndWhiskerPlot boxAndWhiskerPlot = new BoxAndWhiskerPlot(m_2D, m_DisplayUnits.Units);

                    float plotWidth = 40 + GUI.skin.box.padding.horizontal;
                    float plotHeight = 40;
                    plotWidth /= 2.0f;
                    boxAndWhiskerPlot.Draw(plotWidth, plotHeight, leftFrameSummary.msMin, leftFrameSummary.msLowerQuartile,
                        leftFrameSummary.msMedian, leftFrameSummary.msUpperQuartile, leftFrameSummary.msMax, 0, yRange,
                        UIColor.boxAndWhiskerLineColorLeft, UIColor.boxAndWhiskerBoxColorLeft);
                    boxAndWhiskerPlot.Draw(plotWidth, plotHeight, rightFrameSummary.msMin, rightFrameSummary.msLowerQuartile,
                        rightFrameSummary.msMedian, rightFrameSummary.msUpperQuartile, rightFrameSummary.msMax, 0, yRange,
                        UIColor.boxAndWhiskerLineColorRight, UIColor.boxAndWhiskerBoxColorRight);

                    boxAndWhiskerPlot.DrawText(m_Columns.GetColumnWidth(3), plotHeight, 0, yRange,
                        "Min frame time for selected frames in the 2 data sets",
                        "Max frame time for selected frames in the 2 data sets");

                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.LabelField("No analysis data selected");
                }

                EditorGUILayout.EndVertical();
            }

            if (m_ShowFrameSummary != lastShowFrameSummary)
            {
                ProfileAnalyzerAnalytics.SendUIVisibilityEvent(ProfileAnalyzerAnalytics.UIVisibility.Frames, analytic.GetDurationInSeconds(), m_ShowFrameSummary);
            }

            EditorGUILayout.EndVertical();
        }

        void ShowThreadRange()
        {
            EditorGUILayout.BeginHorizontal();
            m_Columns.Draw(0, "Graph Scale : ");
            m_ThreadRange = (ThreadRange)EditorGUILayout.Popup((int)m_ThreadRange, m_ThreadRanges, GUILayout.Width(160));
            EditorGUILayout.EndHorizontal();
        }

        float GetThreadTimeRange(ProfileAnalysis profileAnalysis)
        {
            if (profileAnalysis == null)
                return 0.0f;

            var frameSummary = profileAnalysis.GetFrameSummary();
            float range = frameSummary.msMax;
            switch (m_ThreadRange)
            {
                case ThreadRange.Median:
                    range = frameSummary.msMedian;
                    break;
                case ThreadRange.UpperQuartile:
                    range = frameSummary.msUpperQuartile;
                    break;
                case ThreadRange.Max:
                    range = frameSummary.msMax;
                    break;
            }

            return range;
        }

        int GetThreadSelectionCount(out int leftSelectionCount, out int rightSelectionCount)
        {
            List<string> threadSelection = GetLimitedThreadSelection(m_ThreadNames, m_ThreadSelection);
            leftSelectionCount = 0;
            foreach (var threadName in m_ProfileLeftView.data.GetThreadNames())
            {
                if (threadSelection.Contains(threadName))
                {
                    leftSelectionCount++;
                }
            }
            rightSelectionCount = 0;
            foreach (var threadName in m_ProfileRightView.data.GetThreadNames())
            {
                if (threadSelection.Contains(threadName))
                {
                    rightSelectionCount++;
                }
            }
            return threadSelection.Count;
        }

        void DrawComparisonThreadSummary()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(LayoutSize.WidthRHS));

            bool lastShowThreadSummary = m_ShowThreadSummary;
            m_ShowThreadSummary = BoldFoldout(m_ShowThreadSummary, Styles.threadSummary);
            var analytic = ProfileAnalyzerAnalytics.BeginAnalytic();
            if (m_ShowThreadSummary)
            {
                EditorGUILayout.BeginVertical(); // To match indenting on the marker summary where a scroll area is present

                if (IsAnalysisValid())
                {
                    m_Columns.SetColumnSizes(LayoutSize.WidthColumn0, LayoutSize.WidthColumn1, LayoutSize.WidthColumn2, LayoutSize.WidthColumn3);
                    EditorGUILayout.BeginHorizontal();
                    m_Columns.Draw4("", "Left", "Right", "Total");
                    EditorGUILayout.EndHorizontal();

                    int matchingCount, uniqueLeft, uniqueRight;
                    int allThreadsCount = GetCombinedThreadCount(out matchingCount, out uniqueLeft, out uniqueRight);

                    EditorGUILayout.BeginHorizontal();
                    m_Columns.Draw(0, "Total Count : ");
                    m_Columns.Draw(1, new GUIContent(m_ProfileLeftView.data.GetThreadCount().ToString(), "Total threads in left data set"));
                    m_Columns.Draw(2, new GUIContent(m_ProfileRightView.data.GetThreadCount().ToString(), "Total threads in right data set"));
                    string tooltip = GetThreadCountToolTipUnion(allThreadsCount, matchingCount);
                    m_Columns.Draw(3, new GUIContent(allThreadsCount.ToString(), tooltip));
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    m_Columns.Draw(0, "Unique Count : ");
                    m_Columns.Draw(1, new GUIContent(uniqueLeft.ToString(), "Unique to left data set"));
                    m_Columns.Draw(2, new GUIContent(uniqueRight.ToString(), "Unique to right data set"));
                    tooltip = GetThreadCountToolTipDifference(allThreadsCount, matchingCount, uniqueLeft, uniqueRight);
                    m_Columns.Draw(3, new GUIContent((allThreadsCount - matchingCount).ToString(), tooltip));
                    EditorGUILayout.EndHorizontal();

                    int leftSelectionCount, rightSelectionCount;
                    int selectedThreads = GetThreadSelectionCount(out leftSelectionCount, out rightSelectionCount);

                    EditorGUILayout.BeginHorizontal();
                    m_Columns.Draw(0, "Selected : ");
                    m_Columns.Draw(1, new GUIContent(leftSelectionCount.ToString(), "Left selected"));
                    m_Columns.Draw(2, new GUIContent(rightSelectionCount.ToString(), "Right selected"));
                    m_Columns.Draw(3, new GUIContent(selectedThreads.ToString(), "Total selected"));
                    EditorGUILayout.EndHorizontal();

                    m_Columns.SetColumnSizes(LayoutSize.WidthColumn0, LayoutSize.WidthColumn1, LayoutSize.WidthColumn2 + LayoutSize.WidthColumn3, 0);
                    ShowThreadRange();

                    float width = 100;
                    float height = GUI.skin.label.lineHeight;

                    float xAxisMin = 0.0f;
                    float xAxisMax = GetThreadTimeRange(m_ProfileLeftView.analysis);

                    m_Columns.Draw3(Styles.emptyString, Styles.median, Styles.thread);

                    m_ThreadScroll = EditorGUILayout.BeginScrollView(m_ThreadScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
                    Rect clipRect = new Rect(m_ThreadScroll.x, m_ThreadScroll.y, m_ComparisonThreadsAreaRect.width, m_ComparisonThreadsAreaRect.height);
                    m_2D.SetClipRect(clipRect);
                    for (int i = 0; i < m_ThreadUINames.Count; i++)
                    {
                        string threadNameWithIndex = m_ThreadNames[i];

                        bool include = ProfileAnalyzer.MatchThreadFilter(threadNameWithIndex, m_ThreadSelection.selection);
                        if (!include)
                            continue;

                        ThreadData threadLeft = m_ProfileLeftView.analysis.GetThreadByName(threadNameWithIndex);
                        ThreadData threadRight = m_ProfileRightView.analysis.GetThreadByName(threadNameWithIndex);

                        ThreadData thread = threadLeft != null ? threadLeft : threadRight;
                        if (thread == null)
                            continue;

                        bool singleThread = thread.threadsInGroup > 1 ? false : true;

                        BoxAndWhiskerPlot boxAndWhiskerPlot = new BoxAndWhiskerPlot(m_2D, m_DisplayUnits.Units);
                        EditorGUILayout.BeginHorizontal();
                        if (threadLeft != null)
                            boxAndWhiskerPlot.DrawHorizontal(width, height, threadLeft.msMin, threadLeft.msLowerQuartile, threadLeft.msMedian, threadLeft.msUpperQuartile, threadLeft.msMax, xAxisMin, xAxisMax, UIColor.boxAndWhiskerLineColorLeft, UIColor.boxAndWhiskerBoxColorLeft, GUI.skin.label);
                        else
                            EditorGUILayout.LabelField(Styles.noThread, GUILayout.Width(width));
                        m_Columns.Draw(1, (threadLeft != null) ? ToDisplayUnitsWithTooltips(threadLeft.msMedian) : Styles.noThread);
                        m_Columns.Draw(2, GetThreadNameWithGroupTooltip(thread.threadNameWithIndex, singleThread));
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.BeginHorizontal();
                        if (threadRight != null)
                            boxAndWhiskerPlot.DrawHorizontal(width, height, threadRight.msMin, threadRight.msLowerQuartile, threadRight.msMedian, threadRight.msUpperQuartile, threadRight.msMax, xAxisMin, xAxisMax, UIColor.boxAndWhiskerLineColorRight, UIColor.boxAndWhiskerBoxColorRight, GUI.skin.label);
                        else
                            EditorGUILayout.LabelField(Styles.noThread, GUILayout.Width(width));
                        m_Columns.Draw(1, (threadRight != null) ? ToDisplayUnitsWithTooltips(threadRight.msMedian) : Styles.noThread);
                        m_Columns.Draw(2, "");
                        EditorGUILayout.EndHorizontal();
                    }
                    m_2D.ClearClipRect();
                    EditorGUILayout.EndScrollView();

                    if (Event.current.type == EventType.Repaint)
                    {
                        // This value is not valid at layout phase
                        m_ComparisonThreadsAreaRect = GUILayoutUtility.GetLastRect();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No analysis data selected");
                }

                EditorGUILayout.EndVertical();
            }

            if (m_ShowThreadSummary != lastShowThreadSummary)
            {
                ProfileAnalyzerAnalytics.SendUIVisibilityEvent(ProfileAnalyzerAnalytics.UIVisibility.Threads, analytic.GetDurationInSeconds(), m_ShowThreadSummary);
            }

            EditorGUILayout.EndVertical();
        }

        void DrawCompareOptions()
        {
            Profiler.BeginSample("ProfileAnalyzer.DrawCompareOptions");

            EditorGUILayout.BeginVertical(GUI.skin.box);

            bool lastShowFilters = m_ShowFilters;
            m_ShowFilters = BoldFoldout(m_ShowFilters, Styles.filters);
            var analytic = ProfileAnalyzerAnalytics.BeginAnalytic();
            if (m_ShowFilters)
            {
                DrawNameFilter();
                EditorGUILayout.BeginHorizontal();
                DrawThreadFilter(m_ProfileLeftView.data);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                DrawDepthFilter();
                DrawTimingFilter();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                DrawParentFilter();
                DrawUnitFilter();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (m_ProfileLeftView.IsDataValid() && m_ProfileRightView.IsDataValid())
                {
                    bool lastEnabled = GUI.enabled;
                    GUI.enabled = !IsAnalysisRunning();
                    if (GUILayout.Button(new GUIContent("Compare", m_LastCompareTime), GUILayout.Width(100)))
                        m_RequestCompare = true;
                    GUI.enabled = lastEnabled;
                }
                DrawMarkerCount();
                EditorGUILayout.LabelField(",", GUILayout.Width(10), GUILayout.ExpandWidth(false));
                DrawThreadCount();
                DrawProgress();
                GUILayout.FlexibleSpace();
                DrawMarkerColumnFilter();
                EditorGUILayout.EndHorizontal();
            }
            if (m_ShowFilters != lastShowFilters)
            {
                ProfileAnalyzerAnalytics.SendUIVisibilityEvent(ProfileAnalyzerAnalytics.UIVisibility.Filters, analytic.GetDurationInSeconds(), m_ShowFilters);
            }

            EditorGUILayout.EndVertical();

            Profiler.EndSample();
        }

        void DrawComparison()
        {
            Profiler.BeginSample("ProfileAnalyzer.DrawComparison");

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();

            DrawFilesLoaded();

            if (m_ProfileLeftView.IsDataValid() && m_ProfileRightView.IsDataValid() && m_ProfileLeftView.data.GetFrameCount() > 0 && m_ProfileRightView.data.GetFrameCount() > 0)
            {
                DrawCompareOptions();

                if (m_ComparisonTable != null)
                {
                    EditorGUILayout.BeginVertical(GUI.skin.box);

                    string title = string.Format("Top {0} markers on median frames", m_TopNBars);
                    GUIContent markersTitle = new GUIContent(title, Styles.topMarkersTooltip);

                    bool lastShowTopMarkers = m_ShowTopNMarkers;
                    m_ShowTopNMarkers = BoldFoldout(m_ShowTopNMarkers, markersTitle);
                    var analytic = ProfileAnalyzerAnalytics.BeginAnalytic();
                    if (m_ShowTopNMarkers)
                    {
                        Profiler.BeginSample("ProfileAnalyzer.TopNMarkers");

                        EditorGUILayout.BeginVertical(GUILayout.Height(40));
                        Rect rect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                        rect.height = rect.height / 2;

                        var nameFilters = GetNameFilters();
                        var nameExcludes = GetNameExcludes();

                        m_TopMarkersLeft.SetData(m_ProfileLeftView.analysis, m_DepthFilter1, nameFilters, nameExcludes);
                        m_TopMarkersRight.SetData(m_ProfileRightView.analysis, m_DepthFilter2, nameFilters, nameExcludes);


                        float leftRange = m_TopMarkersLeft.GetTopMarkerTimeRange();
                        float rightRange = m_TopMarkersRight.GetTopMarkerTimeRange();
                        if (m_TopTenDisplay == TopTenDisplay.LongestTime)
                        {
                            float max = Math.Max(leftRange, rightRange);
                            leftRange = max;
                            rightRange = max;
                        }

                        int leftMedian = 0;
                        int rightMedian = 0;
                        if (m_ProfileLeftView.analysis != null)
                        {
                            FrameSummary frameSummary = m_ProfileLeftView.analysis.GetFrameSummary();
                            if (frameSummary.count > 0)
                                leftMedian = frameSummary.medianFrameIndex;
                        }
                        if (m_ProfileRightView.analysis != null)
                        {
                            FrameSummary frameSummary = m_ProfileRightView.analysis.GetFrameSummary();
                            if (frameSummary.count > 0)
                                rightMedian = frameSummary.medianFrameIndex;
                        }
                        int maxMedian = Math.Max(leftMedian, rightMedian);

                        Rect frameIndexRect = new Rect(rect);
                        Vector2 size = GUI.skin.button.CalcSize(new GUIContent(string.Format("{0}", maxMedian)));
                        frameIndexRect.width = Math.Max(size.x, 50);    // DrawFrameIndexButton should always be at least 50 wide

                        if (leftMedian != 0f)
                            DrawFrameIndexButton(frameIndexRect, leftMedian);
                        else
                            GUI.Label(frameIndexRect, "");

                        float padding = 2;
                        rect.x += frameIndexRect.width + padding;
                        rect.width -= frameIndexRect.width;
                        m_TopMarkersLeft.Draw(rect, UIColor.left, m_TopNBars, leftRange, UIColor.barBackground, Color.black, Color.white, true, true);
                        rect.y += rect.height;

                        frameIndexRect.y += rect.height;
                        if (rightMedian != 0f)
                            DrawFrameIndexButton(frameIndexRect, rightMedian);
                        else
                            GUI.Label(frameIndexRect, "");

                        m_TopMarkersRight.Draw(rect, UIColor.right, m_TopNBars, rightRange, UIColor.barBackground, Color.black, Color.white, true, true);
                        EditorGUILayout.EndVertical();

                        EditorGUILayout.BeginHorizontal();
                        GUIContent info;
                        if (m_DepthFilter1 == ProfileAnalyzer.kDepthAll && m_DepthFilter2 == ProfileAnalyzer.kDepthAll)
                        {
                            info = new GUIContent("(All depths)", string.Format("{0}\n\nSet depth 1 to get an overview of the frame", Styles.medianFrameTooltip));
                        }
                        else
                        {
                            if (m_DepthFilter1 != m_DepthFilter2)
                            {
                                if (m_DepthFilter1 == ProfileAnalyzer.kDepthAll)
                                    info = new GUIContent(string.Format("(Filtered to 'all' depths in the first data set, and depth '{0}' in the second)", m_DepthFilter2), Styles.medianFrameTooltip);
                                else if (m_DepthFilter2 == ProfileAnalyzer.kDepthAll)
                                    info = new GUIContent(string.Format("(Filtered to depth '{0}' in the first data set, and 'all' depths in the second)", m_DepthFilter1), Styles.medianFrameTooltip);
                                else
                                    info = new GUIContent(string.Format("(Filtered to depth '{0}' in the first data set, and depth '{1}' in the second)", m_DepthFilter1, m_DepthFilter2), Styles.medianFrameTooltip);
                            }
                            else
                                info = new GUIContent(string.Format("(Filtered to depth '{0}' only)", m_DepthFilter1), Styles.medianFrameTooltip);
                        }
                        GUILayout.Label(info, GUILayout.ExpandWidth(true));
                        GUILayout.Label(Styles.topMarkerRatio, GUILayout.ExpandWidth(false));
                        m_TopTenDisplay = (TopTenDisplay)EditorGUILayout.Popup((int)m_TopTenDisplay, Styles.topTenDisplayOptions, GUILayout.MaxWidth(100));
                        EditorGUILayout.EndHorizontal();

                        Profiler.EndSample();
                    }

                    if (m_ShowTopNMarkers != lastShowTopMarkers)
                    {
                        ProfileAnalyzerAnalytics.SendUIVisibilityEvent(ProfileAnalyzerAnalytics.UIVisibility.Markers, analytic.GetDurationInSeconds(), m_ShowTopNMarkers);
                    }

                    EditorGUILayout.EndVertical();

                    if (m_ComparisonTable != null)
                    {
                        m_ShowMarkerTable = BoldFoldout(m_ShowMarkerTable, Styles.comparisonTable);
                        if (m_ShowMarkerTable)
                        {
                            Profiler.BeginSample("ProfileAnalyzer.DrawComparisonTable");
                            Rect r = EditorGUILayout.GetControlRect(GUILayout.ExpandHeight(true));

                            float scrollBarWidth = GUI.skin.verticalScrollbar.fixedWidth + GUI.skin.verticalScrollbar.border.horizontal + GUI.skin.verticalScrollbar.margin.horizontal + GUI.skin.verticalScrollbar.padding.horizontal;
                            scrollBarWidth += 6;
                            Rect clipRect = new Rect(r.x, 0, r.width - scrollBarWidth, 32767);
                            m_2D.SetClipRect(clipRect);

                            m_ComparisonTable.OnGUI(r);
                            Profiler.EndSample();

                            m_2D.ClearClipRect();
                        }
                    }
                }
            }
            else
            {
                ShowHelp();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.Width(LayoutSize.WidthRHS));
            GUILayout.Space(4);
            DrawComparisonFrameSummary();
            DrawComparisonThreadSummary();
            DrawComparisonSelected();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            Profiler.EndSample();
        }

        bool BoldFoldout(bool toggle, GUIContent content)
        {
            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
            foldoutStyle.fontStyle = FontStyle.Bold;
            return EditorGUILayout.Foldout(toggle, content, foldoutStyle);
        }

        void DrawComparisonSelectedStats(MarkerData leftMarker, MarkerData rightMarker)
        {
            GUIStyle style = GUI.skin.label;

            string units = GetDisplayUnits();
            m_Columns.Draw4("", units, units, units);
            Draw4DiffMs(Styles.max, MarkerData.GetMsMax(leftMarker), MarkerData.GetMaxFrameIndex(leftMarker), MarkerData.GetMsMax(rightMarker), MarkerData.GetMaxFrameIndex(rightMarker));
            Draw4DiffMs(Styles.upperQuartile, MarkerData.GetMsUpperQuartile(leftMarker), MarkerData.GetMsUpperQuartile(rightMarker));
            Draw4DiffMs(Styles.median, MarkerData.GetMsMedian(leftMarker), MarkerData.GetMedianFrameIndex(leftMarker), MarkerData.GetMsMedian(rightMarker), MarkerData.GetMedianFrameIndex(rightMarker));
            Draw4DiffMs(Styles.mean, MarkerData.GetMsMean(leftMarker), MarkerData.GetMsMean(rightMarker));
            Draw4DiffMs(Styles.lowerQuartile, MarkerData.GetMsLowerQuartile(leftMarker), MarkerData.GetMsLowerQuartile(rightMarker));
            Draw4DiffMs(Styles.min, MarkerData.GetMsMin(leftMarker), MarkerData.GetMinFrameIndex(leftMarker), MarkerData.GetMsMin(rightMarker), MarkerData.GetMinFrameIndex(rightMarker));

            GUILayout.Space(style.lineHeight);

            Draw4DiffMs(Styles.individualMax, MarkerData.GetMsMaxIndividual(leftMarker), MarkerData.GetMsMaxIndividual(rightMarker));
            Draw4DiffMs(Styles.individualMin, MarkerData.GetMsMinIndividual(leftMarker), MarkerData.GetMsMinIndividual(rightMarker));
        }

        void DrawComparisonSelected()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(LayoutSize.WidthRHS));

            GUIStyle style = GUI.skin.label;

            bool lastMarkerSummary = m_ShowMarkerSummary;
            m_ShowMarkerSummary = BoldFoldout(m_ShowMarkerSummary, Styles.markerSummary);
            var analytic = ProfileAnalyzerAnalytics.BeginAnalytic();
            if (m_ShowMarkerSummary)
            {
                EditorGUILayout.BeginVertical(); // To match indenting on the marker summary where a scroll area is present

                if (IsAnalysisValid())
                {
                    List<MarkerData> leftMarkers = m_ProfileLeftView.analysis.GetMarkers();
                    List<MarkerData> rightMarkers = m_ProfileRightView.analysis.GetMarkers();
                    int pairingAt = m_SelectedPairing;
                    if (leftMarkers != null && rightMarkers != null && m_Pairings != null)
                    {
                        if (pairingAt >= 0 && pairingAt < m_Pairings.Count)
                        {
                            m_MarkerSummaryScroll = GUILayout.BeginScrollView(m_MarkerSummaryScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
                            Rect clipRect = new Rect(m_MarkerSummaryScroll.x, m_MarkerSummaryScroll.y, LayoutSize.WidthRHS, 500);
                            m_2D.SetClipRect(clipRect);

                            EditorGUILayout.BeginVertical();

                            var pairing = m_Pairings[pairingAt];

                            var leftMarker = (pairing.leftIndex >= 0 && pairing.leftIndex < leftMarkers.Count) ? leftMarkers[pairing.leftIndex] : null;
                            var rightMarker = (pairing.rightIndex >= 0 && pairing.rightIndex < rightMarkers.Count) ? rightMarkers[pairing.rightIndex] : null;

                            EditorGUILayout.LabelField(pairing.name,
                                GUILayout.MaxWidth(LayoutSize.WidthRHS -
                                                   (GUI.skin.box.padding.horizontal + GUI.skin.box.margin.horizontal)));
                            DrawComparisonFrameRatio(leftMarker, rightMarker);

                            m_Columns.SetColumnSizes(LayoutSize.WidthColumn0, LayoutSize.WidthColumn1, LayoutSize.WidthColumn2, LayoutSize.WidthColumn3);

                            EditorGUILayout.BeginHorizontal();
                            m_Columns.Draw(0, Styles.firstFrame);
                            if (leftMarker != null)
                                DrawFrameIndexButton(leftMarker.firstFrameIndex);
                            else
                                m_Columns.Draw(1, Styles.emptyString);
                            if (rightMarker != null)
                                DrawFrameIndexButton(rightMarker.firstFrameIndex);
                            else
                                m_Columns.Draw(2, Styles.emptyString);
                            EditorGUILayout.EndHorizontal();

                            DrawTopComparison(leftMarker, rightMarker);

                            GUILayout.Space(style.lineHeight);

                            EditorGUILayout.BeginHorizontal();

                            int leftBucketCount = leftMarker != null ? leftMarker.buckets.Length : 0;
                            int rightBucketCount = rightMarker != null ? rightMarker.buckets.Length : 0;

                            float leftMin = MarkerData.GetMsMin(leftMarker);
                            float rightMin = MarkerData.GetMsMin(rightMarker);

                            float leftMax = MarkerData.GetMsMax(leftMarker);
                            float rightMax = MarkerData.GetMsMax(rightMarker);

                            int[] leftBuckets = leftMarker != null ? leftMarker.buckets : new int[0];
                            int[] rightBuckets = rightMarker != null ? rightMarker.buckets : new int[0];

                            Units units = m_DisplayUnits.Units;
                            string unitName = "marker time";
                            if (DisplayCount())
                            {
                                units = Units.Count;
                                unitName = "count";

                                leftBucketCount = leftMarker != null ? leftMarker.countBuckets.Length : 0;
                                rightBucketCount = rightMarker != null ? rightMarker.countBuckets.Length : 0;

                                leftMin = MarkerData.GetCountMin(leftMarker);
                                rightMin = MarkerData.GetCountMin(rightMarker);

                                leftMax = MarkerData.GetCountMax(leftMarker);
                                rightMax = MarkerData.GetCountMax(rightMarker);

                                leftBuckets = leftMarker != null ? leftMarker.countBuckets : new int[0];
                                rightBuckets = rightMarker != null ? rightMarker.countBuckets : new int[0];
                            }

                            DisplayUnits displayUnits = new DisplayUnits(units);

                            float minValue;
                            float maxValue;
                            if (leftMarker != null && rightMarker != null)
                            {
                                minValue = Math.Min(leftMin, rightMin);
                                maxValue = Math.Max(leftMax, rightMax);
                            }
                            else if (leftMarker != null)
                            {
                                minValue = leftMin;
                                maxValue = leftMax;
                            }
                            else // Either valid or 0
                            {
                                minValue = rightMin;
                                maxValue = rightMax;
                            }

                            if (leftBucketCount > 0 && rightBucketCount > 0 && leftBucketCount != rightBucketCount)
                            {
                                Debug.Log("Error - number of buckets doesn't match in the left and right marker analysis");
                            }
                            else
                            {
                                int bucketCount = Math.Max(leftBucketCount, rightBucketCount);
                                int leftFrameCount = MarkerData.GetPresentOnFrameCount(leftMarker);
                                int rightFrameCount = MarkerData.GetPresentOnFrameCount(rightMarker);

                                DrawComparisonHistogram(100, minValue, maxValue, bucketCount, leftBuckets, rightBuckets, leftFrameCount, rightFrameCount, leftMarker != null, rightMarker != null, displayUnits);
                            }

                            float plotWidth = 40 + GUI.skin.box.padding.horizontal;
                            float plotHeight = 100;
                            plotWidth /= 2.0f;
                            BoxAndWhiskerPlot boxAndWhiskerPlot = new BoxAndWhiskerPlot(m_2D, units);
                            DrawBoxAndWhiskerPlotForMarker(boxAndWhiskerPlot, plotWidth, plotHeight, m_ProfileLeftView.analysis, leftMarker, minValue, maxValue,
                                 UIColor.boxAndWhiskerLineColorLeft, UIColor.boxAndWhiskerBoxColorLeft);
                            DrawBoxAndWhiskerPlotForMarker(boxAndWhiskerPlot, plotWidth, plotHeight, m_ProfileRightView.analysis, rightMarker, minValue, maxValue,
                                 UIColor.boxAndWhiskerLineColorRight, UIColor.boxAndWhiskerBoxColorRight);

                            boxAndWhiskerPlot.DrawText(m_Columns.GetColumnWidth(3), plotHeight, minValue, maxValue,
                                string.Format("Min {0} for selected frames in the 2 data sets", unitName),
                                string.Format("Max {0} for selected frames in the 2 data sets", unitName));

                            EditorGUILayout.EndHorizontal();

                            GUILayout.Space(style.lineHeight);

                            DrawComparisonSelectedStats(leftMarker, rightMarker);

                            EditorGUILayout.EndVertical();

                            m_2D.ClearClipRect();
                            GUILayout.EndScrollView();
                        }
                        else
                        {
                            EditorGUILayout.LabelField("Marker not in selection");
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No marker data selected");
                }

                EditorGUILayout.EndVertical();
            }

            if (m_ShowMarkerSummary != lastMarkerSummary)
            {
                ProfileAnalyzerAnalytics.SendUIVisibilityEvent(ProfileAnalyzerAnalytics.UIVisibility.Markers, analytic.GetDurationInSeconds(), m_ShowMarkerSummary);
            }

            EditorGUILayout.EndVertical();
        }

        void SelectTab(ActiveTab newTab)
        {
            m_NextActiveTab = newTab;
        }

        void DrawToolbar()
        {
            Profiler.BeginSample("ProfileAnalyzer.DrawToolbar");

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUILayout.LabelField("Mode:", GUILayout.Width(40));
            ActiveTab newTab = (ActiveTab)GUILayout.Toolbar((int)m_ActiveTab, new string[] { "Single", "Compare" }, EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
            if (newTab != m_ActiveTab)
            {
                SelectTab(newTab);
            }

            //GUILayout.FlexibleSpace();
            EditorGUILayout.Separator();
            bool lastEnabled = GUI.enabled;
            bool enabled = GUI.enabled;
            if (m_ProfileSingleView.IsDataValid() || (m_ProfileLeftView.IsDataValid() && m_ProfileRightView.IsDataValid()))
                GUI.enabled = true;
            else
                GUI.enabled = false;
            if (GUILayout.Button(Styles.export, EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                Vector2 windowPosition = new Vector2(Event.current.mousePosition.x, Event.current.mousePosition.y + GUI.skin.label.lineHeight);
                Vector2 screenPosition = GUIUtility.GUIToScreenPoint(windowPosition);

                ProfileAnalyzerExportWindow.Open(screenPosition.x, screenPosition.y, m_ProfileSingleView, m_ProfileLeftView, m_ProfileRightView, this);
            }

            if (GUILayout.Button(Styles.exportFunc, EditorStyles.toolbarButton, GUILayout.Width(150)))
            {
                LogFuncToCSV();
                //Vector2 windowPosition = new Vector2(Event.current.mousePosition.x, Event.current.mousePosition.y + GUI.skin.label.lineHeight);
                //Vector2 screenPosition = GUIUtility.GUIToScreenPoint(windowPosition);

                // ProfileAnalyzerExportWindow.Open(screenPosition.x, screenPosition.y, m_ProfileSingleView, m_ProfileLeftView, m_ProfileRightView);
            }
            GUI.enabled = lastEnabled;

            bool profilerOpen = IsProfilerWindowOpen();
            if (!profilerOpen)
            {
                if (GUILayout.Toggle(profilerOpen, "Open Profiler Window", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)) == true)
                {
                    var analytic = ProfileAnalyzerAnalytics.BeginAnalytic();
                    m_ProfilerWindowInterface.OpenProfilerOrUseExisting();
                    ProfileAnalyzerAnalytics.SendUIButtonEvent(ProfileAnalyzerAnalytics.UIButton.OpenProfiler, analytic);
                }
            }
            else
            {
                if (GUILayout.Toggle(profilerOpen, "Close Profiler Window", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)) == false)
                {
                    var analytic = ProfileAnalyzerAnalytics.BeginAnalytic();
                    m_ProfilerWindowInterface.CloseProfiler();
                    ProfileAnalyzerAnalytics.SendUIButtonEvent(ProfileAnalyzerAnalytics.UIButton.CloseProfiler, analytic);
                }
            }
            EditorGUILayout.Separator();

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();

            Profiler.EndSample();
        }

        void SetupStyles()
        {
            if (!m_StylesSetup)
            {
                m_StyleMiddleRight = new GUIStyle(GUI.skin.label);
                m_StyleMiddleRight.alignment = TextAnchor.MiddleRight;

                m_StyleUpperLeft = new GUIStyle(GUI.skin.label);
                m_StyleUpperLeft.alignment = TextAnchor.UpperLeft;

                m_StylesSetup = true;
            }
        }

        void Draw()
        {
            // Make sure we start enabled (in case something overrode it last frame)
            GUI.enabled = true;

            Profiler.BeginSample("ProfileAnalyzer.Draw");
            SetupStyles();

            EditorGUILayout.BeginVertical();

            DrawToolbar();

            switch (m_ActiveTab)
            {
                case ActiveTab.Summary:
                    DrawAnalysis();
                    break;
                case ActiveTab.Compare:
                    DrawComparison();
                    break;
            }

            EditorGUILayout.EndVertical();

            Profiler.EndSample();
        }

        int FindSelectionByName(List<MarkerData> markers, string name)
        {
            int index = 0;
            foreach (var marker in markers)
            {
                if (marker.name == name)
                    return index;
                index++;
            }
            return -1; // not found
        }

        /// <summary>
        /// Select marker to focus on 
        /// </summary>
        /// <param name="name">Name of the marker</param>
        public void SelectMarker(string name)
        {
            switch (m_ActiveTab)
            {
                case ActiveTab.Summary:
                    SelectMarkerByName(name);
                    break;
                case ActiveTab.Compare:
                    SelectPairingByName(name);
                    break;
            }
        }

        void UpdateSelectedMarkerName(string markerName)
        {
            m_SelectedMarkerName = markerName;
            if (m_ThreadSelection.selection != null && m_ThreadSelection.selection.Count > 0)
            {
                m_ProfilerWindowInterface.SetProfilerWindowMarkerName(markerName, m_ThreadSelection.selection);
            }
        }

        internal void SelectMarkerByIndex(int index)
        {
            if (m_ProfileSingleView == null || m_ProfileSingleView.analysis == null)
                return;


            // Check if this marker is in the 'filtered' list
            var markers = m_ProfileSingleView.analysis.GetMarkers();
            if (markers.Count <= 0)
                return;

            bool valid = true;
            if (index >= 0 && index < markers.Count)
            {
                var marker = markers[index];

                var nameFilters = GetNameFilters();
                if (nameFilters.Count > 0)
                {
                    if (!NameInFilterList(marker.name, nameFilters))
                        valid = false;
                }

                var nameExcludes = GetNameExcludes();
                if (nameExcludes.Count > 0)
                {
                    if (NameInExcludeList(marker.name, nameExcludes))
                        valid = false;
                }
            }

            m_SelectedMarker = index;

            if (m_ProfileTable != null)
            {
                List<int> selection = new List<int>();
                if (index >= 0 && valid)
                    selection.Add(index);
                m_ProfileTable.SetSelection(selection, TreeViewSelectionOptions.RevealAndFrame);
            }

            var markerName = GetMarkerName(index);
            if (markerName != null)
                UpdateSelectedMarkerName(markerName);
        }

        /// <summary>
        /// Get currently selected marker
        /// </summary>
        /// <returns>Name of currently selected marker, or null if none selected</returns>
        public string GetSelectedMarkerName()
        {
            switch (m_ActiveTab)
            {
                case ActiveTab.Summary:
                    return GetMarkerName(m_SelectedMarker);
                case ActiveTab.Compare:
                    return GetPairingName(m_SelectedPairing);
            }

            return null;
        }

        string GetMarkerName(int index)
        {
            if (m_ProfileSingleView.analysis == null)
                return null;

            var marker = m_ProfileSingleView.analysis.GetMarker(index);
            if (marker == null)
                return null;

            return marker.name;
        }

        void SelectMarkerByName(string markerName)
        {
            int index = (m_ProfileSingleView.analysis != null) ? m_ProfileSingleView.analysis.GetMarkerIndexByName(markerName) : -1;

            SelectMarkerByIndex(index);
        }

        internal void SelectPairing(int index)
        {
            if (m_Pairings == null || m_Pairings.Count == 0)
                return;

            // Check if this marker is in the 'filtered' list
            bool valid = true;
            if (index >= 0 && index < m_Pairings.Count)
            {
                var pairing = m_Pairings[index];

                var nameFilters = GetNameFilters();
                if (nameFilters.Count > 0)
                {
                    if (!NameInFilterList(pairing.name, nameFilters))
                        valid = false;
                }

                var nameExcludes = GetNameExcludes();
                if (nameExcludes.Count > 0)
                {
                    if (NameInExcludeList(pairing.name, nameExcludes))
                        valid = false;
                }
            }

            m_SelectedPairing = index;

            if (m_ComparisonTable != null)
            {
                List<int> selection = new List<int>();
                if (index >= 0 && valid)
                    selection.Add(index);
                m_ComparisonTable.SetSelection(selection, TreeViewSelectionOptions.RevealAndFrame);
            }

            var markerName = GetPairingName(index);
            if (markerName != null)
                UpdateSelectedMarkerName(markerName);
        }

        string GetPairingName(int index)
        {
            if (m_Pairings == null)
                return null;

            if (index < 0 || index >= m_Pairings.Count)
                return null;

            return m_Pairings[index].name;
        }

        void SelectPairingByName(string pairingName)
        {
            if (m_Pairings != null && pairingName != null)
            {
                for (int index = 0; index < m_Pairings.Count; index++)
                {
                    var pairing = m_Pairings[index];
                    if (pairing.name == pairingName)
                    {
                        SelectPairing(index);
                        return;
                    }
                }
            }

            SelectPairing(-1);
        }

        GUIContent GetFrameRangeText(ProfileAnalysis analysis)
        {
            var frameSummary = analysis.GetFrameSummary();

            string text;
            string tooltip;
            if (frameSummary.count == 0)
            {
                text = "";
                tooltip = "";
            }
            else if (frameSummary.first == frameSummary.last)
            {
                text = string.Format("{0}", frameSummary.first);
                tooltip = string.Format("Frame {0} selected", frameSummary.first);
            }
            else
            {
                int rangeSize = (1 + (frameSummary.last - frameSummary.first));
                if (frameSummary.count == rangeSize)
                {
                    text = string.Format("{0}-{1}", frameSummary.first, frameSummary.last);
                    tooltip = string.Format("{0} frames selected\n\n{1} first selected frame\n{2} last selected frame\n\nConsecutive Sequence", frameSummary.count, frameSummary.first, frameSummary.last);
                }
                else
                {
                    text = string.Format("{0}-{1}*", frameSummary.first, frameSummary.last);
                    tooltip = string.Format("{0} frames selected\n\n{1} first selected frame\n{2} last selected frame\n\nNot a consecutive sequence", frameSummary.count, frameSummary.first, frameSummary.last);
                }
            }

            return new GUIContent(text, tooltip);
        }

        GUIContent GetFrameCountText(ProfileAnalysis analysis)
        {
            var frameSummary = analysis.GetFrameSummary();

            string text;
            string tooltip;
            if (frameSummary.first == frameSummary.last)
            {
                text = string.Format("{0}", frameSummary.count);
                tooltip = "";
            }
            else
            {
                int rangeSize = (1 + (frameSummary.last - frameSummary.first));
                if (frameSummary.count == rangeSize)
                {
                    text = string.Format("{0}", frameSummary.count);
                    tooltip = string.Format("{0} frames selected\n\n{1} first selected frame\n{2} last selected frame\n\nConsecutive Sequence", frameSummary.count, frameSummary.first, frameSummary.last);
                }
                else
                {
                    text = string.Format("{0}*", frameSummary.count);
                    tooltip = string.Format("{0} frames selected\n\n{1} first selected frame\n{2} last selected frame\n\nNot a consecutive sequence", frameSummary.count, frameSummary.first, frameSummary.last);
                }
            }

            return new GUIContent(text, tooltip);
        }

        GUIContent GetFirstFrameText(ProfileAnalysis analysis)
        {
            var frameSummary = analysis.GetFrameSummary();

            string text;
            string tooltip;
            if (frameSummary.count == 0)
            {
                text = "";
                tooltip = "";
            }
            else if (frameSummary.first == frameSummary.last)
            {
                text = string.Format("{0}", frameSummary.first);
                tooltip = string.Format("Frame {0} selected", frameSummary.first);
            }
            else
            {
                int rangeSize = (1 + (frameSummary.last - frameSummary.first));
                if (frameSummary.count == rangeSize)
                {
                    text = string.Format("{0}", frameSummary.first);
                    tooltip = string.Format("{0} frames selected\n\n{1} first selected frame\n{2} last selected frame\n\nConsecutive Sequence", frameSummary.count, frameSummary.first, frameSummary.last);
                }
                else
                {
                    text = string.Format("{0}*", frameSummary.first);
                    tooltip = string.Format("{0} frames selected\n\n{1} first selected frame\n{2} last selected frame\n\nNot a consecutive sequence", frameSummary.count, frameSummary.first, frameSummary.last);
                }
            }

            return new GUIContent(text, tooltip);
        }

        GUIContent GetLastFrameText(ProfileAnalysis analysis)
        {
            var frameSummary = analysis.GetFrameSummary();

            string text;
            string tooltip;
            if (frameSummary.count == 0)
            {
                text = "";
                tooltip = "";
            }
            else if (frameSummary.first == frameSummary.last)
            {
                text = "";
                tooltip = string.Format("Frame {0} selected", frameSummary.first);
            }
            else
            {
                int rangeSize = (1 + (frameSummary.last - frameSummary.first));
                if (frameSummary.count == rangeSize)
                {
                    text = string.Format("{0}", frameSummary.last);
                    tooltip = string.Format("{0} frames selected\n\n{1} first selected frame\n{2} last selected frame\n\nConsecutive Sequence", frameSummary.count, frameSummary.first, frameSummary.last);
                }
                else
                {
                    text = string.Format("{0}*", frameSummary.last);
                    tooltip = string.Format("{0} frames selected\n\n{1} first selected frame\n{2} last selected frame\n\nNot a consecutive sequence", frameSummary.count, frameSummary.first, frameSummary.last);
                }
            }

            return new GUIContent(text, tooltip);
        }


        void DrawFrameSummary()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(LayoutSize.WidthRHS));

            bool lastShowFrameSummary = m_ShowFrameSummary;
            m_ShowFrameSummary = BoldFoldout(m_ShowFrameSummary, Styles.frameSummary);
            var analytic = ProfileAnalyzerAnalytics.BeginAnalytic();
            if (m_ShowFrameSummary)
            {
                EditorGUILayout.BeginVertical(); // To match indenting on the marker summary where a scroll area is present

                if (IsAnalysisValid())
                {
                    var frameSummary = m_ProfileSingleView.analysis.GetFrameSummary();

                    m_Columns.SetColumnSizes(LayoutSize.WidthColumn0, LayoutSize.WidthColumn1, LayoutSize.WidthColumn2, LayoutSize.WidthColumn3);
                    m_Columns.Draw(0, "");

                    m_Columns.Draw2(Styles.frameCount, GetFrameCountText(m_ProfileSingleView.analysis));

                    EditorGUILayout.BeginHorizontal();
                    m_Columns.Draw(0, Styles.frameStart);
                    GUIContent firstFrameTextContent = GetFirstFrameText(m_ProfileSingleView.analysis);
                    m_Columns.Draw(1, firstFrameTextContent);
                    if (firstFrameTextContent.text != "")
                        DrawFrameIndexButton(frameSummary.first);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    m_Columns.Draw(0, Styles.frameEnd);
                    GUIContent lastFrameTextContent = GetLastFrameText(m_ProfileSingleView.analysis);
                    m_Columns.Draw(1, lastFrameTextContent);
                    if (lastFrameTextContent.text != "")
                        DrawFrameIndexButton(frameSummary.last);
                    EditorGUILayout.EndHorizontal();

                    m_Columns.SetColumnSizes(LayoutSize.WidthColumn0, LayoutSize.WidthColumn1, LayoutSize.WidthColumn2, LayoutSize.WidthColumn3);
                    m_Columns.Draw(0, "");
                    m_Columns.Draw3("", GetDisplayUnits(), "Frame");

                    Draw3LabelMsFrame(Styles.max, frameSummary.msMax, frameSummary.maxFrameIndex);
                    Draw2LabelMs(Styles.upperQuartile, frameSummary.msUpperQuartile);
                    Draw3LabelMsFrame(Styles.median, frameSummary.msMedian, frameSummary.medianFrameIndex);
                    Draw2LabelMs(Styles.mean, frameSummary.msMean);
                    Draw2LabelMs(Styles.lowerQuartile, frameSummary.msLowerQuartile);
                    Draw3LabelMsFrame(Styles.min, frameSummary.msMin, frameSummary.minFrameIndex);

                    GUIStyle style = GUI.skin.label;
                    GUILayout.Space(style.lineHeight);

                    EditorGUILayout.BeginHorizontal();
                    Histogram histogram = new Histogram(m_2D, m_DisplayUnits.Units);
                    histogram.Draw(LayoutSize.HistogramWidth, 40, frameSummary.buckets, frameSummary.count, 0, frameSummary.msMax, UIColor.bar);

                    BoxAndWhiskerPlot boxAndWhiskerPlot = new BoxAndWhiskerPlot(m_2D, m_DisplayUnits.Units);

                    float plotWidth = 40 + GUI.skin.box.padding.horizontal;
                    float plotHeight = 40;
                    boxAndWhiskerPlot.Draw(plotWidth, plotHeight, frameSummary.msMin, frameSummary.msLowerQuartile, frameSummary.msMedian, frameSummary.msUpperQuartile, frameSummary.msMax, 0, frameSummary.msMax, UIColor.standardLine, UIColor.standardLine);

                    boxAndWhiskerPlot.DrawText(m_Columns.GetColumnWidth(3), plotHeight, frameSummary.msMin, frameSummary.msMax,
                        "Min frame time for selected frames",
                        "Max frame time for selected frames");

                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.LabelField("No analysis data selected");
                }

                EditorGUILayout.EndVertical();
            }

            if (m_ShowFrameSummary != lastShowFrameSummary)
            {
                ProfileAnalyzerAnalytics.SendUIVisibilityEvent(ProfileAnalyzerAnalytics.UIVisibility.Frames, analytic.GetDurationInSeconds(), m_ShowFrameSummary);
            }

            EditorGUILayout.EndVertical();
        }

        GUIContent GetThreadNameWithGroupTooltip(string threadNameWithIndex, bool singleThread)
        {
            string friendlyThreadName = GetFriendlyThreadName(threadNameWithIndex, singleThread);
            string groupName;
            friendlyThreadName = ProfileData.GetThreadNameWithoutGroup(friendlyThreadName, out groupName);

            if (groupName == "")
                return new GUIContent(friendlyThreadName, string.Format("{0}", friendlyThreadName));
            else
                return new GUIContent(friendlyThreadName, string.Format("{0}\n{1}", friendlyThreadName, groupName));
        }

        void DrawThreadSummary()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(LayoutSize.WidthRHS));

            bool lastShowThreadSummary = m_ShowThreadSummary;
            m_ShowThreadSummary = BoldFoldout(m_ShowThreadSummary, Styles.threadSummary);
            var analytic = ProfileAnalyzerAnalytics.BeginAnalytic();
            if (m_ShowThreadSummary)
            {
                EditorGUILayout.BeginVertical(); // To match indenting on the marker summary where a scroll area is present
                if (IsAnalysisValid())
                {
                    float xAxisMin = 0.0f;
                    float xAxisMax = GetThreadTimeRange(m_ProfileSingleView.analysis);

                    m_Columns.SetColumnSizes(LayoutSize.WidthColumn0, LayoutSize.WidthColumn1, LayoutSize.WidthColumn2, LayoutSize.WidthColumn3);
                    EditorGUILayout.BeginHorizontal();
                    m_Columns.Draw4("", "", "", "");
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    m_Columns.Draw(0, "Total Count : ");
                    m_Columns.Draw(1, m_ProfileSingleView.data.GetThreadCount().ToString());
                    EditorGUILayout.EndHorizontal();

                    List<string> threadSelection = GetLimitedThreadSelection(m_ThreadNames, m_ThreadSelection);
                    int selectedThreads = threadSelection.Count;

                    EditorGUILayout.BeginHorizontal();
                    m_Columns.Draw(0, "Selected : ");
                    m_Columns.Draw(1, selectedThreads.ToString());
                    EditorGUILayout.EndHorizontal();
                    ShowThreadRange();

                    m_Columns.SetColumnSizes(LayoutSize.WidthColumn0, LayoutSize.WidthColumn1, LayoutSize.WidthColumn2 + LayoutSize.WidthColumn3, 0);
                    m_Columns.Draw3("", "Median", "Thread");

                    m_ThreadScroll = EditorGUILayout.BeginScrollView(m_ThreadScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
                    Rect clipRect = new Rect(m_ThreadScroll.x, m_ThreadScroll.y, m_ThreadsAreaRect.width, m_ThreadsAreaRect.height);
                    m_2D.SetClipRect(clipRect);
                    for (int i = 0; i < m_ThreadUINames.Count; i++)
                    {
                        string threadNameWithIndex = m_ThreadNames[i];
                        if (!threadNameWithIndex.Contains(":"))
                            continue;    // Ignore 'All' 

                        bool include = ProfileAnalyzer.MatchThreadFilter(threadNameWithIndex, m_ThreadSelection.selection);
                        if (!include)
                            continue;

                        ThreadData thread = m_ProfileSingleView.analysis.GetThreadByName(threadNameWithIndex);
                        if (thread == null)    // May be the 'all' field
                            continue;

                        bool singleThread = thread.threadsInGroup > 1 ? false : true;

                        BoxAndWhiskerPlot boxAndWhiskerPlot = new BoxAndWhiskerPlot(m_2D, m_DisplayUnits.Units);
                        EditorGUILayout.BeginHorizontal();
                        boxAndWhiskerPlot.DrawHorizontal(100, GUI.skin.label.lineHeight, thread.msMin, thread.msLowerQuartile, thread.msMedian, thread.msUpperQuartile, thread.msMax, xAxisMin, xAxisMax, UIColor.bar, UIColor.barBackground, GUI.skin.label);

                        m_Columns.Draw(1, ToDisplayUnitsWithTooltips(thread.msMedian));
                        m_Columns.Draw(2, GetThreadNameWithGroupTooltip(thread.threadNameWithIndex, singleThread));
                        EditorGUILayout.EndHorizontal();
                    }
                    m_2D.ClearClipRect();
                    EditorGUILayout.EndScrollView();

                    if (Event.current.type == EventType.Repaint)
                    {
                        // This value is not valid at layout phase
                        m_ThreadsAreaRect = GUILayoutUtility.GetLastRect();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No analysis data selected");
                }
                EditorGUILayout.EndVertical();
            }

            if (m_ShowThreadSummary != lastShowThreadSummary)
            {
                ProfileAnalyzerAnalytics.SendUIVisibilityEvent(ProfileAnalyzerAnalytics.UIVisibility.Threads, analytic.GetDurationInSeconds(), m_ShowThreadSummary);
            }

            EditorGUILayout.EndVertical();
        }

        void DrawHistogramForMarker(Histogram histogram, MarkerData marker)
        {
            if (DisplayCount())
                histogram.Draw(LayoutSize.HistogramWidth, 100, marker.countBuckets, marker.presentOnFrameCount, marker.countMin, marker.countMax, UIColor.bar);
            else
                histogram.Draw(LayoutSize.HistogramWidth, 100, marker.buckets, marker.presentOnFrameCount, marker.msMin, marker.msMax, UIColor.bar);
        }

        internal bool IsProfilerWindowOpen()
        {
            return m_ProfilerWindowInterface.IsReady();
        }

        bool DataMatchesProfiler(ProfileData data, int frameIndex, out string message)
        {
            if (data == null)
            {
                message = "";
                return false;
            }

            // Don't check full range match as we may have only captured a single frame from the data
            /*
            int dataFirstFrameIndex = data.OffsetToDisplayFrame(0);
            int dataLastFrameIndex = data.OffsetToDisplayFrame(m_ProfileSingleView.data.GetFrameCount() - 1);
            int profilerFirstFrameIndex;
            int profilerLastFrameIndex;
            m_profilerWindowInterface.GetFrameRangeFromProfiler(out firstFrameIndex, out lastFrameIndex);

            if (dataFirstFrameIndex != profilerFirstFrameIndex ||
                dataLastFrameIndex != profilerLastFrameIndex)
            {
                message = string.Format("Data in profiler doesn't match data range({0}-{1}) != profiler range ({2}-{3}",
                                        dataFirstFrameIndex,
                                        dataLastFrameIndex,
                                        profilerFirstFrameIndex,
                                        profilerLastFrameIndex);
                return false;
            }
            */

            // Check check the frame we are jumping to.
            int dataFrameOffset = data.DisplayFrameToOffset(frameIndex);            // Convert from user facing index to zero based offset into analysis data
            int frames = data.GetFrameCount();
            if (dataFrameOffset < 0 || dataFrameOffset >= frames)
            {
                message = string.Format("Timing data in profiler doesn't match : Frame {0} out of range {1}-{2}",
                                        frameIndex, data.OffsetToDisplayFrame(0), data.OffsetToDisplayFrame(frames - 1));
                return false;
            }
            float msData = data.GetFrame(dataFrameOffset).msFrame;
            float msProfiler = m_ProfilerWindowInterface.GetFrameTime(frameIndex - 1); // Convert from user facing index to zero based index into profiler data
            if (msData != msProfiler)
            {
                message = string.Format("Timing data in profiler doesn't match for frame {0} : Expected {1:f2}ms but frame is {2:f2}ms",
                                        frameIndex, msData, msProfiler);
                return false;
            }

            message = "";
            return true;
        }

        internal void JumpToFrame(int frameIndex, bool reportErrors = true)
        {
            if (!m_ProfilerWindowInterface.IsReady())
                return;


            var analytic = ProfileAnalyzerAnalytics.BeginAnalytic();

            string message;
            if (!m_ProfilerWindowInterface.JumpToFrame(frameIndex))
            {
                message = string.Format("Timing data in profiler doesn't match : Frame {0} does not exist", frameIndex);
                Debug.Log(message);
            }
            else
            {
                ProfileData data = m_ProfileSingleView.data;
                bool dataMatch = DataMatchesProfiler(data, frameIndex, out message);
                if (!dataMatch && reportErrors)
                {
                    Debug.Log(message);
                }
            }
            ProfileAnalyzerAnalytics.SendUIButtonEvent(ProfileAnalyzerAnalytics.UIButton.JumpToFrame, analytic);
        }

        internal float DrawFrameIndexButton(int index)
        {
            float defaultWidth = 50f;
            if (index < 0)
                return defaultWidth;

            bool enabled = GUI.enabled;
            if (!IsProfilerWindowOpen())
                GUI.enabled = false;

            var content = new GUIContent(string.Format("{0}", index), string.Format("Jump to frame {0} in the Unity Profiler", index));
            Vector2 size = GUI.skin.button.CalcSize(content);
            //float height = size.y;
            float maxWidth = Math.Max(defaultWidth, size.x);
            if (GUILayout.Button(content, GUILayout.MinWidth(defaultWidth), GUILayout.MaxWidth(maxWidth)))
            {
                JumpToFrame(index);
            }

            GUI.enabled = enabled;

            return maxWidth;
        }

        internal void DrawFrameIndexButton(Rect rect, int index)
        {
            if (index < 0)
                return;

            bool enabled = GUI.enabled;
            if (!IsProfilerWindowOpen())
                GUI.enabled = false;

            // Clamp to max height to match other buttons
            // And centre vertically if needed
            var content = new GUIContent(string.Format("{0}", index), string.Format("Jump to frame {0} in the Unity Profiler", index));
            Vector2 size = GUI.skin.button.CalcSize(content);
            float height = size.y; // was 14
            rect.y += (rect.height - height) / 2;
            rect.height = Math.Min(rect.height, height);

            if (GUI.Button(rect, content))
            {
                JumpToFrame(index);
            }

            GUI.enabled = enabled;
        }

        GUIContent ToDisplayUnitsWithTooltips(float ms, bool showUnits = false, int frameIndex = -1)
        {
            return m_DisplayUnits.ToGUIContentWithTooltips(ms, showUnits, 5, frameIndex);
        }

        void Draw3LabelMsFrame(string col1, float ms, int frameIndex)
        {
            EditorGUILayout.BeginHorizontal();
            m_Columns.Draw(0, col1);
            m_Columns.Draw(1, ToDisplayUnitsWithTooltips(ms));
            DrawFrameIndexButton(frameIndex);
            EditorGUILayout.EndHorizontal();
        }

        void Draw3LabelMsFrame(GUIContent col1, float ms, int frameIndex)
        {
            EditorGUILayout.BeginHorizontal();
            m_Columns.Draw(0, col1);
            m_Columns.Draw(1, ToDisplayUnitsWithTooltips(ms));
            DrawFrameIndexButton(frameIndex);
            EditorGUILayout.EndHorizontal();
        }

        void Draw2LabelMs(GUIContent col1, float ms)
        {
            EditorGUILayout.BeginHorizontal();
            m_Columns.Draw(0, col1);
            m_Columns.Draw(1, ToDisplayUnitsWithTooltips(ms));
            EditorGUILayout.EndHorizontal();
        }

        void Draw4DiffMs(GUIContent col1, float msLeft, float msRight)
        {
            EditorGUILayout.BeginHorizontal();
            m_Columns.Draw(0, col1);
            m_Columns.Draw(1, ToDisplayUnitsWithTooltips(msLeft));
            m_Columns.Draw(2, ToDisplayUnitsWithTooltips(msRight));
            m_Columns.Draw(3, ToDisplayUnitsWithTooltips(msRight - msLeft));
            EditorGUILayout.EndHorizontal();
        }

        void Draw4DiffMs(GUIContent col1, float msLeft, int frameIndexLeft, float msRight, int frameIndexRight)
        {
            EditorGUILayout.BeginHorizontal();
            m_Columns.Draw(0, col1);
            m_Columns.Draw(1, ToDisplayUnitsWithTooltips(msLeft, false, frameIndexLeft));
            m_Columns.Draw(2, ToDisplayUnitsWithTooltips(msRight, false, frameIndexRight));
            m_Columns.Draw(3, ToDisplayUnitsWithTooltips(msRight - msLeft));
            EditorGUILayout.EndHorizontal();
        }

        void Draw4Ms(GUIContent col1, float value2, float value3, float value4)
        {
            EditorGUILayout.BeginHorizontal();
            m_Columns.Draw(0, col1);
            m_Columns.Draw(1, ToDisplayUnitsWithTooltips(value2));
            m_Columns.Draw(2, ToDisplayUnitsWithTooltips(value3));
            m_Columns.Draw(3, ToDisplayUnitsWithTooltips(value4));
            EditorGUILayout.EndHorizontal();
        }

        void DrawBoxAndWhiskerPlotForMarker(BoxAndWhiskerPlot boxAndWhiskerPlot, float width, float height, ProfileAnalysis analysis, MarkerData marker, float yAxisStart, float yAxisEnd, Color color, Color colorBackground)
        {
            if (marker == null)
            {
                boxAndWhiskerPlot.Draw(width, height, 0, 0, 0, 0, 0, yAxisStart, yAxisEnd, color, colorBackground);
                return;
            }

            if (DisplayCount())
                boxAndWhiskerPlot.Draw(width, height, marker.countMin, marker.countLowerQuartile, marker.countMedian, marker.countUpperQuartile, marker.countMax, yAxisStart, yAxisEnd, color, colorBackground);
            else
                boxAndWhiskerPlot.Draw(width, height, marker.msMin, marker.msLowerQuartile, marker.msMedian, marker.msUpperQuartile, marker.msMax, yAxisStart, yAxisEnd, color, colorBackground);
        }

        void DrawBoxAndWhiskerPlotHorizontalForMarker(BoxAndWhiskerPlot boxAndWhiskerPlot, float width, float height, ProfileAnalysis analysis, MarkerData marker, float yAxisStart, float yAxisEnd, Color color, Color colorBackground)
        {
            boxAndWhiskerPlot.DrawHorizontal(width, height, marker.msMin, marker.msLowerQuartile, marker.msMedian, marker.msUpperQuartile, marker.msMax, yAxisStart, yAxisEnd, color, colorBackground);
        }

        void DrawFrameRatio(MarkerData marker)
        {
            var frameSummary = m_ProfileSingleView.analysis.GetFrameSummary();

            GUIStyle style = GUI.skin.label;
            float w = LayoutSize.WidthColumn0;
            float h = style.lineHeight;
            float ySpacing = 2;
            float barHeight = h - ySpacing;

            EditorGUILayout.BeginVertical(GUILayout.Width(w + LayoutSize.WidthColumn1 + LayoutSize.WidthColumn2));

            float barMax = frameSummary.msMean;
            float barValue = marker.msMean;
            string text = "Mean frame contribution";
            Units units = m_DisplayUnits.Units;
            if (DisplayCount())
            {
                units = Units.Count;
                barMax = frameSummary.markerCountMaxMean;
                barValue = marker.countMean;
                text = "Mean count";
            }
            DisplayUnits displayUnits = new DisplayUnits(units);
            float barLength = Math.Min((w * barValue) / barMax, w);

            EditorGUILayout.LabelField(text);
            m_Columns.SetColumnSizes(LayoutSize.WidthColumn0, LayoutSize.WidthColumn1, LayoutSize.WidthColumn2, LayoutSize.WidthColumn3);

            m_Columns.Draw2("", "");
            EditorGUILayout.BeginHorizontal();

            // NOTE: This can effect the whole width of the region its inside
            // Not clear why
            if (m_2D.DrawStart(w, h, Draw2D.Origin.TopLeft, style))
            {
                m_2D.DrawFilledBox(0, ySpacing, barLength, barHeight, UIColor.bar);
                m_2D.DrawFilledBox(barLength, ySpacing, w - barLength, barHeight, UIColor.barBackground);
                m_2D.DrawEnd();

                Rect rect = GUILayoutUtility.GetLastRect();
                string tooltip = string.Format("{0}", displayUnits.ToString(barValue, true, 5));
                GUI.Label(rect, new GUIContent("", tooltip));
            }

            EditorGUILayout.LabelField(ShowPercent((100 * barValue) / barMax), GUILayout.MaxWidth(50));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        GUIContent ShowPercent(float percent)
        {
            string text;
            string tooltip;

            if (percent >= 999.95f)
            {
                text = string.Format("{0:f0}%", percent);
                tooltip = string.Format("{0:f2}%", percent);
            }
            else if (percent >= 99.995f)
            {
                text = string.Format("{0:f1}%", percent);
                tooltip = string.Format("{0:f2}%", percent);
            }
            else
            {
                text = string.Format("{0:f2}%", percent);
                tooltip = text;
            }


            return new GUIContent(text, tooltip);
        }

        void DrawComparisonFrameRatio(MarkerData leftMarker, MarkerData rightMarker)
        {
            var leftFrameSummary = m_ProfileLeftView.analysis.GetFrameSummary();
            var rightFrameSummary = m_ProfileRightView.analysis.GetFrameSummary();

            GUIStyle style = GUI.skin.label;
            float w = LayoutSize.WidthColumn0;
            float h = style.lineHeight;
            float ySpacing = 2;
            float barHeight = (h - ySpacing) / 2;

            EditorGUILayout.BeginVertical(GUILayout.Width(w + LayoutSize.WidthColumn1 + LayoutSize.WidthColumn2));

            float leftBarValue = MarkerData.GetMsMean(leftMarker);
            float rightBarValue = MarkerData.GetMsMean(rightMarker);
            float leftBarMax = leftFrameSummary.msMean;
            float rightBarMax = rightFrameSummary.msMean;

            string text = "Mean frame contribution";
            Units units = m_DisplayUnits.Units;
            if (DisplayCount())
            {
                units = Units.Count;
                leftBarValue = MarkerData.GetCountMean(leftMarker);
                rightBarValue = MarkerData.GetCountMean(rightMarker);
                leftBarMax = leftFrameSummary.markerCountMaxMean;
                rightBarMax = rightFrameSummary.markerCountMaxMean;
                text = "Mean count";
            }

            DisplayUnits displayUnits = new DisplayUnits(units);

            float leftBarLength = (leftBarMax > 0) ? (w * leftBarValue) / leftBarMax : 0f;
            leftBarLength = Math.Min(leftBarLength, w);
            float rightBarLength = (rightBarMax > 0) ? (w * rightBarValue) / rightBarMax : 0f;
            rightBarLength = Math.Min(rightBarLength, w);

            EditorGUILayout.LabelField(text);
            m_Columns.SetColumnSizes(LayoutSize.WidthColumn0, LayoutSize.WidthColumn1, LayoutSize.WidthColumn2, LayoutSize.WidthColumn3);
            m_Columns.Draw4("", "Left", "Right", "Diff");
            EditorGUILayout.BeginHorizontal();
            if (m_2D.DrawStart(w, h, Draw2D.Origin.TopLeft, style))
            {
                m_2D.DrawFilledBox(0, ySpacing, w, h - ySpacing, UIColor.barBackground);

                m_2D.DrawFilledBox(0, ySpacing, leftBarLength, barHeight, UIColor.left);
                m_2D.DrawFilledBox(0, ySpacing + barHeight, rightBarLength, barHeight, UIColor.right);
                m_2D.DrawEnd();

                Rect rect = GUILayoutUtility.GetLastRect();
                string tooltip = string.Format("Left: {0}\nRight: {1}", displayUnits.ToString(leftBarValue, true, 5), displayUnits.ToString(rightBarValue, true, 5));
                GUI.Label(rect, new GUIContent("", tooltip));
            }
            float leftPercentage = leftBarMax > 0 ? (100 * leftBarValue) / leftBarMax : 0f;
            float rightPercentage = rightBarMax > 0 ? (100 * rightBarValue) / rightBarMax : 0f;

            EditorGUILayout.LabelField(ShowPercent(leftPercentage), GUILayout.Width(LayoutSize.WidthColumn1));
            EditorGUILayout.LabelField(ShowPercent(rightPercentage), GUILayout.Width(LayoutSize.WidthColumn2));
            if (leftMarker != null && rightMarker != null)
                EditorGUILayout.LabelField(ShowPercent(rightPercentage - leftPercentage), GUILayout.Width(LayoutSize.WidthColumn3));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        void DrawTopComparison(MarkerData leftMarker, MarkerData rightMarker)
        {
            GUIStyle style = GUI.skin.label;
            float w = LayoutSize.WidthColumn0;
            float h = style.lineHeight;
            float ySpacing = 2;
            float barHeight = (h - ySpacing) / 2;

            EditorGUILayout.BeginVertical(GUILayout.Width(w + LayoutSize.WidthColumn1 + LayoutSize.WidthColumn2));
            bool showCount = DisplayCount();

            float leftMax = MarkerData.GetMsMax(leftMarker);
            float rightMax = MarkerData.GetMsMax(rightMarker);

            Units units = m_DisplayUnits.Units;
            if (showCount)
            {
                units = Units.Count;
                leftMax = MarkerData.GetCountMax(leftMarker);
                rightMax = MarkerData.GetCountMax(rightMarker);
            }
            DisplayUnits displayUnits = new DisplayUnits(units);

            TopMarkerList topMarkerList = new TopMarkerList(m_2D, units,
                LayoutSize.WidthColumn0, LayoutSize.WidthColumn1, LayoutSize.WidthColumn2, LayoutSize.WidthColumn3,
                UIColor.bar, UIColor.barBackground, DrawFrameIndexButton);
            m_TopNumber = topMarkerList.DrawTopNumber(m_TopNumber, m_TopStrings, m_TopValues);

            float barMax = Math.Max(leftMax, rightMax);

            List<FrameTime> leftFrames = leftMarker != null ? topMarkerList.GetTopN(leftMarker, m_TopNumber, showCount) : new List<FrameTime>();
            List<FrameTime> rightFrames = rightMarker != null ? topMarkerList.GetTopN(rightMarker, m_TopNumber, showCount) : new List<FrameTime>();

            FrameTime zeroFrameTime = new FrameTime(-1, 0.0f, 0,0.0f);
            for (int i = 0; i < m_TopNumber; i++)
            {
                bool leftValid = i < leftFrames.Count;
                bool rightValid = i < rightFrames.Count;
                FrameTime leftFrameTime = leftValid ? leftFrames[i] : zeroFrameTime;
                FrameTime rightFrameTime = rightValid ? rightFrames[i] : zeroFrameTime;

                float leftBarValue = showCount ? leftFrameTime.count : leftFrameTime.ms;
                float rightBarValue = showCount ? rightFrameTime.count : rightFrameTime.ms;

                float leftBarLength = Math.Min((w * leftBarValue) / barMax, w);
                float rightBarLength = Math.Min((w * rightBarValue) / barMax, w);

                EditorGUILayout.BeginHorizontal();
                if (m_2D.DrawStart(w, h, Draw2D.Origin.TopLeft, style))
                {
                    if (leftValid || rightValid)
                    {
                        m_2D.DrawFilledBox(0, ySpacing, w, h - ySpacing, UIColor.barBackground);

                        m_2D.DrawFilledBox(0, ySpacing, leftBarLength, barHeight, UIColor.left);
                        m_2D.DrawFilledBox(0, ySpacing + barHeight, rightBarLength, barHeight, UIColor.right);
                    }
                    m_2D.DrawEnd();

                    Rect rect = GUILayoutUtility.GetLastRect();
                    string leftContent = leftValid ? string.Format("{0} on frame {1}", displayUnits.ToString(leftBarValue, true, 5), leftFrameTime.frameIndex) : "None";
                    string rightContent = rightValid ? string.Format("{0} on frame {1}", displayUnits.ToString(rightBarValue, true, 5), rightFrameTime.frameIndex) : "None";
                    GUI.Label(rect, new GUIContent("", string.Format("Left:\t{0}\nRight:\t{1}", leftContent, rightContent)));
                }

                EditorGUILayout.LabelField(leftValid ? displayUnits.ToGUIContentWithTooltips(leftBarValue, frameIndex: leftFrameTime.frameIndex) : Styles.emptyString, GUILayout.Width(LayoutSize.WidthColumn1));
                EditorGUILayout.LabelField(rightValid ? displayUnits.ToGUIContentWithTooltips(rightBarValue, frameIndex: rightFrameTime.frameIndex) : Styles.emptyString, GUILayout.Width(LayoutSize.WidthColumn2));
                if (leftValid || rightValid)
                    EditorGUILayout.LabelField(displayUnits.ToGUIContentWithTooltips(rightBarValue - leftBarValue), GUILayout.Width(LayoutSize.WidthColumn3));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        void DrawSelectedStats(MarkerData marker)
        {
            GUIStyle style = GUI.skin.label;

            m_Columns.Draw3("", GetDisplayUnits(), "Frame");
            Draw3LabelMsFrame(Styles.max, marker.msMax, marker.maxFrameIndex);
            Draw2LabelMs(Styles.upperQuartile, marker.msUpperQuartile);
            Draw3LabelMsFrame(Styles.median, marker.msMedian, marker.medianFrameIndex);
            Draw2LabelMs(Styles.mean, marker.msMean);
            Draw2LabelMs(Styles.lowerQuartile, marker.msLowerQuartile);
            Draw3LabelMsFrame(Styles.min, marker.msMin, marker.minFrameIndex);

            GUILayout.Space(style.lineHeight);

            Draw3LabelMsFrame(Styles.individualMax, marker.msMaxIndividual,
                marker.maxIndividualFrameIndex);
            Draw3LabelMsFrame(Styles.individualMin, marker.msMinIndividual,
                marker.minIndividualFrameIndex);
        }

        void DrawSelected()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(LayoutSize.WidthRHS));

            bool lastMarkerSummary = m_ShowMarkerSummary;
            m_ShowMarkerSummary = BoldFoldout(m_ShowMarkerSummary, Styles.markerSummary);
            var analytic = ProfileAnalyzerAnalytics.BeginAnalytic();
            if (m_ShowMarkerSummary)
            {
                if (IsAnalysisValid())
                {
                    List<MarkerData> markers = m_ProfileSingleView.analysis.GetMarkers();
                    if (markers != null)
                    {
                        int markerAt = m_SelectedMarker;
                        if (markerAt >= 0 && markerAt < markers.Count)
                        {
                            var marker = markers[markerAt];

                            m_MarkerSummaryScroll = GUILayout.BeginScrollView(m_MarkerSummaryScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
                            Rect clipRect = new Rect(m_MarkerSummaryScroll.x, m_MarkerSummaryScroll.y, LayoutSize.WidthRHS, 500);
                            m_2D.SetClipRect(clipRect);

                            EditorGUILayout.BeginVertical();

                            EditorGUILayout.LabelField(marker.name,
                                GUILayout.MaxWidth(LayoutSize.WidthRHS -
                                                   (GUI.skin.box.padding.horizontal + GUI.skin.box.margin.horizontal)));

                            DrawFrameRatio(marker);

                            m_Columns.SetColumnSizes(LayoutSize.WidthColumn0, LayoutSize.WidthColumn1, LayoutSize.WidthColumn2, LayoutSize.WidthColumn3);

                            EditorGUILayout.BeginHorizontal();
                            m_Columns.Draw(0, Styles.firstFrame);
                            m_Columns.Draw(1, Styles.emptyString);
                            DrawFrameIndexButton(marker.firstFrameIndex);
                            EditorGUILayout.EndHorizontal();

                            GUIStyle style = GUI.skin.label;

                            float min = marker.msMin;
                            float max = marker.msMax;
                            string fieldString = "marker time";
                            Units units = m_DisplayUnits.Units;
                            if (DisplayCount())
                            {
                                min = marker.countMin;
                                max = marker.countMax;
                                fieldString = "count";
                                units = Units.Count;
                            }

                            TopMarkerList topMarkerList = new TopMarkerList(m_2D, units,
                                LayoutSize.WidthColumn0, LayoutSize.WidthColumn1, LayoutSize.WidthColumn2, LayoutSize.WidthColumn3,
                                UIColor.bar, UIColor.barBackground, DrawFrameIndexButton);
                            m_TopNumber = topMarkerList.Draw(marker, m_TopNumber, m_TopStrings, m_TopValues);

                            GUILayout.Space(style.lineHeight);

                            float plotWidth = 40 + GUI.skin.box.padding.horizontal;
                            float plotHeight = 100;

                            EditorGUILayout.BeginHorizontal();

                            Histogram histogram = new Histogram(m_2D, units);
                            DrawHistogramForMarker(histogram, marker);

                            BoxAndWhiskerPlot boxAndWhiskerPlot = new BoxAndWhiskerPlot(m_2D, units);
                            DrawBoxAndWhiskerPlotForMarker(boxAndWhiskerPlot, plotWidth, plotHeight, m_ProfileSingleView.analysis, marker,
                                                           min, max, UIColor.standardLine, UIColor.boxAndWhiskerBoxColor);

                            boxAndWhiskerPlot.DrawText(m_Columns.GetColumnWidth(3), plotHeight, min, max,
                                string.Format("Min {0} for selected frames", fieldString),
                                string.Format("Max {0} for selected frames", fieldString));
                            EditorGUILayout.EndHorizontal();

                            GUILayout.Space(style.lineHeight);

                            DrawSelectedStats(marker);

                            EditorGUILayout.EndVertical();

                            m_2D.ClearClipRect();
                            GUILayout.EndScrollView();
                        }
                        else
                        {
                            EditorGUILayout.LabelField("Marker not in selection");
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No marker data selected");
                }
            }

            if (m_ShowMarkerSummary != lastMarkerSummary)
            {
                ProfileAnalyzerAnalytics.SendUIVisibilityEvent(ProfileAnalyzerAnalytics.UIVisibility.Markers, analytic.GetDurationInSeconds(), m_ShowMarkerSummary);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 通过函数名获取数据然后导出为CSV
        /// </summary>
        /// <param name="markernames"></param>
        public void GetDataFromMarkerNames(List<string> markernames)
        {
            //@eddiezhao
            string path = "./AnalyzerDataCSV/";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            foreach (string markername in markernames)
            {
                var markername_ = markername.Replace(':', '：'); //文件名不能有冒号
                string filename = $"{path}{DateTime.Now.ToString("yyMMddHHmmss")} {markername_}.csv";
                StreamWriter sw = new StreamWriter(filename, false, Encoding.UTF8);
                sw.WriteLine("函数名,总时长(ms),每帧平均耗时(ms),单次平均耗时(ms),总调用次数,有效帧数,有效帧平均次数");
                SetAsParentMarkerFilter(markername);
                AnalyzeSync();
                var allData = m_ProfileSingleView.analysisNew.GetMarkers();
                foreach (var data in allData)
                {
                    string dataname = data.name.Replace(',', '，'); //csv有逗号就尬了
                    sw.WriteLine($"{dataname},{data.msTotal},{data.msTotal / data.presentOnFrameCount},{data.msTotal / data.count},{data.count},{data.presentOnFrameCount},{1.0f * data.count / data.presentOnFrameCount}");
                    //Debug.LogError($"{data.name} 总时长:{data.msTotal}, 每帧平均耗时:{data.msTotal / data.presentOnFrameCount} 总调用次数:{data.count} 有效帧数:{data.presentOnFrameCount} 有效帧平均次数:{1.0f * data.count / data.presentOnFrameCount} 单次耗时:{data.msTotal / data.count}");
                }
                sw.Close();
            }
        }
        
        /// <summary>
        /// 获取MeanMinMax消耗
        /// </summary>
        /// <param name="markername"></param>
        /// <returns></returns>
        public string GetDataFromMarkerName(string markername)
        {
            SetAsParentMarkerFilter(markername);
            AnalyzeSync();
            var allData = m_ProfileSingleView.analysisNew.GetMarkers();
            if (allData.Count > 0)
            {
                MarkerData data = allData[0];
                return $"{data.msMean}({data.msMin} - {data.msMax})";
            }

            return string.Empty;
        }
        
        /// <summary>
        /// 通过函数名获取数据 根据指定文本 然后导出为CSV
        /// </summary>
        /// <param name="markernames"></param>
        public void GetDataFromMarkerNamesBySpecifyPrefixName(List<string> markernames)
        {
            string specifyPrefixsFilePath = "./AnalyzerDataCSV/SpecifyPrefixs.txt";
            if (!File.Exists(specifyPrefixsFilePath))
            {
                Debug.LogError($"文件不存在, 请检查. 路径为{specifyPrefixsFilePath}");
                return;
            }
            
            string path = "./AnalyzerDataCSV/";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            List<string> list = new List<string>();
            string[] lines = File.ReadAllLines("./AnalyzerDataCSV/SpecifyPrefixs.txt");
            foreach (var line in lines)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    list.Add(line);
                }
            }
                
            foreach (string markername in markernames)
            {
                var markername_ = markername.Replace(':', '：'); //文件名不能有冒号
                string filename = $"{path}{DateTime.Now.ToString("yyMMddHHmmss")} {markername_}.csv";
                StreamWriter sw = new StreamWriter(filename, false, Encoding.UTF8);
                sw.WriteLine("函数名, Depth, Mean(ms), Min(ms), Max(ms)");
                SetAsParentMarkerFilter(markername);
                AnalyzeSync();
                var allData = m_ProfileSingleView.analysisNew.GetMarkers();
                foreach (var data in allData)
                {
                    string dataname = data.name.Replace(',', '，'); //csv有逗号就尬了

                    bool bHit = false;
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (data.name.Contains(list[i]))
                        {
                            bHit = true;
                            break;
                        }
                    }

                    if (!bHit)
                    {
                        continue;
                    }

                    sw.WriteLine($"{dataname},{data.maxDepth},{data.msMean},{data.msMin}, {data.msMax}");
                }
                sw.Close();
            }
        }
    }
}