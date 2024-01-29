using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace UnityEditor.Performance.ProfileAnalyzer
{
    internal class TopMarkers
    {
        internal class RangeSettings
        {
            ProfileAnalysis m_Analysis;
            int m_DepthFilter;
            List<string> m_NameFilters;
            List<string> m_NameExcludes;

            public RangeSettings(ProfileAnalysis analysis, int depthFilter, List<string> nameFilters, List<string> nameExcludes)
            {
                m_Analysis = analysis;
                m_DepthFilter = depthFilter;
                m_NameFilters = nameFilters;
                m_NameExcludes = nameExcludes;
            }

            public override int GetHashCode()
            {
                int hash = 13;
                hash = (hash * 7) + m_Analysis.GetHashCode();
                hash = (hash * 7) + m_DepthFilter.GetHashCode();
                hash = (hash * 7) + m_NameFilters.GetHashCode();
                hash = (hash * 7) + m_NameExcludes.GetHashCode();

                return hash;
            }

            public override bool Equals(object b)
            {
                if (System.Object.ReferenceEquals(null, b))
                {
                    return false;
                }

                if (System.Object.ReferenceEquals(this, b))
                {
                    return true;
                }

                if (b.GetType() != this.GetType())
                {
                    return false;
                }

                return IsEqual((RangeSettings)b);
            }

            bool IsEqual(RangeSettings b)
            {
                if (m_Analysis != b.m_Analysis)
                    return false;
                if (m_DepthFilter != b.m_DepthFilter)
                    return false;

                if (m_NameFilters.Count != b.m_NameFilters.Count)
                    return false;
                if (m_NameExcludes.Count != b.m_NameExcludes.Count)
                    return false;

                // Want to check if contents match, not just if refeernce is the same
                for (int i = 0; i < m_NameFilters.Count; i++)
                {
                    if (m_NameFilters[i] != b.m_NameFilters[i])
                        return false;
                }
                for (int i = 0; i < m_NameExcludes.Count; i++)
                {
                    if (m_NameExcludes[i] != b.m_NameExcludes[i])
                        return false;
                }

                /*
                if (!m_NameFilters.Equals(b.m_NameFilters))
                    return false;
                if (!m_NameExcludes.Equals(b.m_NameExcludes))
                    return false;
                */

                return true;
            }

            public static bool operator ==(RangeSettings a, RangeSettings b)
            {
                // If both are null, or both are same instance, return true.
                if (System.Object.ReferenceEquals(a, b))
                {
                    return true;
                }

                // If one is null, but not both, return false.
                if (((object)a == null) || ((object)b == null))
                {
                    return false;
                }

                return a.IsEqual(b);
            }

            public static bool operator !=(RangeSettings a, RangeSettings b)
            {
                return !(a == b);
            }
        }

        internal class Settings
        {
            ProfileAnalysis m_Analysis;
            int m_BarCount;
            float m_TimeRange;
            int m_DepthFilter;
            bool m_IncludeOthers;
            bool m_IncludeUnaccounted;
            List<string> m_NameFilters;
            List<string> m_NameExcludes;

            public Settings(ProfileAnalysis analysis, int barCount, float timeRange, int depthFilter, bool includeOthers, bool includeUnaccounted, List<string> nameFilters, List<string> nameExcludes)
            {
                m_Analysis = analysis;
                m_BarCount = barCount;
                m_TimeRange = timeRange;
                m_DepthFilter = depthFilter;
                m_IncludeOthers = includeOthers;
                m_IncludeUnaccounted = includeUnaccounted;
                m_NameFilters = nameFilters;
                m_NameExcludes = nameExcludes;
            }

            public override int GetHashCode()
            {
                int hash = 13;
                hash = (hash * 7) + m_Analysis.GetHashCode();
                hash = (hash * 7) + m_BarCount.GetHashCode();
                hash = (hash * 7) + m_TimeRange.GetHashCode();
                hash = (hash * 7) + m_DepthFilter.GetHashCode();
                hash = (hash * 7) + m_IncludeOthers.GetHashCode();
                hash = (hash * 7) + m_IncludeUnaccounted.GetHashCode();
                hash = (hash * 7) + m_NameFilters.GetHashCode();
                hash = (hash * 7) + m_NameExcludes.GetHashCode();

                return hash;
            }

            public override bool Equals(object b)
            {
                if (System.Object.ReferenceEquals(null, b))
                {
                    return false;
                }

                if (System.Object.ReferenceEquals(this, b))
                {
                    return true;
                }

                if (b.GetType() != this.GetType())
                {
                    return false;
                }

                return IsEqual((Settings)b);
            }

            bool IsEqual(Settings b)
            {
                if (m_Analysis != b.m_Analysis)
                    return false;
                if (m_BarCount != b.m_BarCount)
                    return false;
                if (m_TimeRange != b.m_TimeRange)
                    return false;
                if (m_DepthFilter != b.m_DepthFilter)
                    return false;
                if (m_IncludeOthers != b.m_IncludeOthers)
                    return false;
                if (m_IncludeUnaccounted != b.m_IncludeUnaccounted)
                    return false;

                if (m_NameFilters.Count != b.m_NameFilters.Count)
                    return false;
                if (m_NameExcludes.Count != b.m_NameExcludes.Count)
                    return false;

                // Want to check if contents match, not just if refeernce is the same
                for (int i = 0; i < m_NameFilters.Count; i++)
                {
                    if (m_NameFilters[i] != b.m_NameFilters[i])
                        return false;
                }
                for (int i = 0; i < m_NameExcludes.Count; i++)
                {
                    if (m_NameExcludes[i] != b.m_NameExcludes[i])
                        return false;
                }

                /*
                if (!m_NameFilters.Equals(b.m_NameFilters))
                    return false;
                if (!m_NameExcludes.Equals(b.m_NameExcludes))
                    return false;
                */

                return true;
            }

            public static bool operator ==(Settings a, Settings b)
            {
                // If both are null, or both are same instance, return true.
                if (System.Object.ReferenceEquals(a, b))
                {
                    return true;
                }

                // If one is null, but not both, return false.
                if (((object)a == null) || ((object)b == null))
                {
                    return false;
                }

                return a.IsEqual(b);
            }

            public static bool operator !=(Settings a, Settings b)
            {
                return !(a == b);
            }
        }

        internal enum SummaryType
        {
            Marker,
            Other,
            Unaccounted
        }

        internal struct MarkerSummaryEntry
        {
            public readonly string name;
            public readonly float msAtMedian;   // At the median frame (Miliseconds)
            public readonly float msMedian;     // median value for marker over all frames (Miliseconds) on frame medianFrameIndex
            public readonly float x;
            public readonly float w;
            public readonly int medianFrameIndex;
            public readonly SummaryType summaryType;

            public MarkerSummaryEntry(string name, float msAtMedian, float msMedian, float x, float w, int medianFrameIndex, SummaryType summaryType)
            {
                this.name = name;
                this.msAtMedian = msAtMedian;
                this.msMedian = msMedian;
                this.x = x;
                this.w = w;
                this.medianFrameIndex = medianFrameIndex;
                this.summaryType = summaryType;
            }
        }

        internal class MarkerSummary
        {
            public List<MarkerSummaryEntry> entry;

            public float totalTime;

            public MarkerSummary()
            {
                entry = new List<MarkerSummaryEntry>();
                totalTime = 0f;
            }
        }

        ProfileAnalysis m_Analysis;
        List<string> m_NameFilters;
        List<string> m_NameExcludes;
        int m_DepthFilter;

        RangeSettings m_LastRangeSettings;
        Settings m_LastSettings;

        float m_TimeRange;
        MarkerSummary m_MarkerSummary;

        internal static class Styles
        {
            public static readonly GUIContent menuItemSelectFramesInAll = new GUIContent("Select Frames that contain this marker (within whole data set)", "");
            public static readonly GUIContent menuItemSelectFramesInCurrent = new GUIContent("Select Frames that contain this marker (within current selection)", "");
            //public static readonly GUIContent menuItemClearSelection = new GUIContent("Clear Selection");
            public static readonly GUIContent menuItemSelectFramesAll = new GUIContent("Select All");
            public static readonly GUIContent menuItemAddToIncludeFilter = new GUIContent("Add to Include Filter", "");
            public static readonly GUIContent menuItemAddToExcludeFilter = new GUIContent("Add to Exclude Filter", "");
            public static readonly GUIContent menuItemRemoveFromIncludeFilter = new GUIContent("Remove from Include Filter", "");
            public static readonly GUIContent menuItemRemoveFromExcludeFilter = new GUIContent("Remove from Exclude Filter", "");
            public static readonly GUIContent menuItemSetAsParentMarkerFilter = new GUIContent("Set as Parent Marker Filter", "");
            public static readonly GUIContent menuItemClearParentMarkerFilter = new GUIContent("Clear Parent Marker Filter", "");
            public static readonly GUIContent menuItemCopyToClipboard = new GUIContent("Copy to Clipboard", "");
        }

        ProfileAnalyzerWindow m_ProfileAnalyzerWindow;
        Draw2D m_2D;
        Color m_BackgroundColor;
        Color m_TextColor;

        public TopMarkers(ProfileAnalyzerWindow profileAnalyzerWindow, Draw2D draw2D, Color backgroundColor, Color textColor)
        {
            m_ProfileAnalyzerWindow = profileAnalyzerWindow;
            m_2D = draw2D;
            m_BackgroundColor = backgroundColor;
            m_TextColor = textColor;
        }

        string ToDisplayUnits(float ms, bool showUnits = false, int limitToDigits = 5)
        {
            return m_ProfileAnalyzerWindow.ToDisplayUnits(ms, showUnits, limitToDigits);
        }

        public void SetData(ProfileAnalysis analysis, int depthFilter, List<string> nameFilters, List<string> nameExcludes)
        {
            m_Analysis = analysis;
            m_DepthFilter = depthFilter;
            m_NameFilters = nameFilters;
            m_NameExcludes = nameExcludes;
        }

        float CalculateTopMarkerTimeRange(ProfileAnalysis analysis, int depthFilter, List<string> nameFilters, List<string> nameExcludes)
        {
            if (analysis == null)
                return 0.0f;

            var frameSummary = analysis.GetFrameSummary();
            if (frameSummary == null)
                return 0.0f;

            var markers = analysis.GetMarkers();

            float range = 0;
            foreach (var marker in markers)
            {
                if (depthFilter != ProfileAnalyzer.kDepthAll && marker.minDepth != depthFilter)
                {
                    continue;
                }

                if (nameFilters.Count > 0)
                {
                    if (!m_ProfileAnalyzerWindow.NameInFilterList(marker.name, nameFilters))
                        continue;
                }
                if (nameExcludes.Count > 0)
                {
                    if (m_ProfileAnalyzerWindow.NameInExcludeList(marker.name, nameExcludes))
                        continue;
                }

                range += marker.msAtMedian;
            }

            // Minimum is the frame time range
            // As we can have unaccounted markers 
            if (range < frameSummary.msMedian)
                range = frameSummary.msMedian;

            return range;
        }

        public float GetTopMarkerTimeRange()
        {
            if (m_Analysis == null)
                return 0.0f;

            var frameSummary = m_Analysis.GetFrameSummary();
            if (frameSummary == null)
                return 0.0f;

            m_Analysis.GetMarkers();

            RangeSettings currentRangeSettings = new RangeSettings(m_Analysis, m_DepthFilter, m_NameFilters, m_NameExcludes);
            if (currentRangeSettings != m_LastRangeSettings)
            {
                Profiler.BeginSample("CalculateTopMarkerTimeRange");

                m_TimeRange = CalculateTopMarkerTimeRange(m_Analysis, m_DepthFilter, m_NameFilters, m_NameExcludes);
                m_LastRangeSettings = currentRangeSettings;

                Profiler.EndSample();
            }

            return m_TimeRange;
        }

        public MarkerSummary CalculateTopMarkers(ProfileAnalysis analysis, int barCount, float timeRange, int depthFilter, bool includeOthers, bool includeUnaccounted, List<string> nameFilters, List<string> nameExcludes)
        {
            FrameSummary frameSummary = analysis.GetFrameSummary();
            if (frameSummary == null)
                return new MarkerSummary();

            var markers = analysis.GetMarkers();
            if (markers == null)
                return new MarkerSummary();

            // Show marker graph
            float x = 0;
            float width = 1.0f;

            int max = barCount;
            int at = 0;

            float other = 0.0f;

            if (timeRange <= 0.0f)
                timeRange = frameSummary.msMedian;

            float msToWidth = width / timeRange;

            float totalMarkerTime = 0;

            MarkerSummary markerSummary = new MarkerSummary();

            foreach (var marker in markers)
            {
                float msAtMedian = MarkerData.GetMsAtMedian(marker);
                totalMarkerTime += msAtMedian;

                if (depthFilter != ProfileAnalyzer.kDepthAll && marker.minDepth != depthFilter)
                {
                    continue;
                }

                if (nameFilters.Count > 0)
                {
                    if (!m_ProfileAnalyzerWindow.NameInFilterList(marker.name, nameFilters))
                        continue;
                }
                if (nameExcludes.Count > 0)
                {
                    if (m_ProfileAnalyzerWindow.NameInExcludeList(marker.name, nameExcludes))
                        continue;
                }

                if (at < max)
                {
                    float w = CaculateWidth(x, msAtMedian, msToWidth, width);
                    float msMedian = MarkerData.GetMsMedian(marker);
                    markerSummary.entry.Add(new MarkerSummaryEntry(marker.name, msAtMedian, msMedian, x, w, marker.medianFrameIndex, SummaryType.Marker));

                    x += w;
                }
                else
                {
                    other += msAtMedian;
                    if (!includeOthers)
                        break;
                }

                at++;
            }

            if (includeOthers && other > 0.0f)
            {
                float w = CaculateWidth(x, other, msToWidth, width);
                markerSummary.entry.Add(new MarkerSummaryEntry("Other", other, 0f, x, w, -1, SummaryType.Other));
                x += w;
            }
            if (includeUnaccounted && totalMarkerTime < frameSummary.msMedian)
            {
                float unaccounted = frameSummary.msMedian - totalMarkerTime;
                float w = CaculateWidth(x, unaccounted, msToWidth, width);
                markerSummary.entry.Add(new MarkerSummaryEntry("Unaccounted", unaccounted, 0f, x, w, -1, SummaryType.Unaccounted));
                x += w;
            }

            markerSummary.totalTime = totalMarkerTime;

            return markerSummary;
        }

        public void Draw(Rect rect, Color barColor, int barCount, float timeRange, Color selectedBackground, Color selectedBorder, Color selectedText, bool includeOthers, bool includeUnaccounted)
        {
            if (m_Analysis == null)
                return;

            Settings currentSettings = new Settings(m_Analysis, barCount, timeRange, m_DepthFilter, includeOthers, includeUnaccounted, m_NameFilters, m_NameExcludes);
            if (currentSettings != m_LastSettings)
            {
                Profiler.BeginSample("CalculateTopMarkers");

                m_MarkerSummary = CalculateTopMarkers(m_Analysis, barCount, timeRange, m_DepthFilter, includeOthers, includeUnaccounted, m_NameFilters, m_NameExcludes);
                m_LastSettings = currentSettings;

                Profiler.EndSample();
            }

            if (m_MarkerSummary == null || m_MarkerSummary.entry == null)
                return;

            FrameSummary frameSummary = m_Analysis.GetFrameSummary();
            if (frameSummary==null)
                return;
            if (frameSummary.count <= 0)
                return;

            var markers = m_Analysis.GetMarkers();
            if (markers==null)
                return;

            Profiler.BeginSample("DrawHeader");

            // After the marker graph we want an indication of the time range
            int rangeLabelWidth = 60;
            Rect rangeLabelRect = new Rect(rect.x + rect.width - rangeLabelWidth, rect.y, rangeLabelWidth, rect.height);
            string timeRangeString = ToDisplayUnits(timeRange, true);
            string frameTimeString = ToDisplayUnits(frameSummary.msMedian, true, 0);
            string timeRangeTooltip = string.Format("{0} median frame time", frameTimeString);
            if (frameSummary.count > 0)
                GUI.Label(rangeLabelRect, new GUIContent(timeRangeString, timeRangeTooltip) );

            // Reduce the size of the marker graph for the button/label we just added
            rect.width -= rangeLabelWidth;

            // Show marker graph
            float y = 0;
            float width = rect.width;
            float height = rect.height;

            var selectedPairingMarkerName = m_ProfileAnalyzerWindow.GetSelectedMarkerName();

            if (timeRange <= 0.0f)
                timeRange = frameSummary.msMedian;

            Profiler.EndSample();

            if (m_2D.DrawStart(rect, Draw2D.Origin.BottomLeft))
            {
                Profiler.BeginSample("DrawBars");
                
                m_2D.DrawFilledBox(0, y, width, height, m_BackgroundColor);

                foreach (MarkerSummaryEntry entry in m_MarkerSummary.entry)
                {
                    String name = entry.name;

                    float x = entry.x * width;
                    float w = entry.w * width;
                    if (entry.summaryType==SummaryType.Marker)
                    { 
                        if (name == selectedPairingMarkerName)
                        {
                            DrawBar(x, y, w, height, selectedBackground, selectedBorder, true);
                        }
                        else
                        {
                            DrawBar(x, y, w, height, barColor, selectedBorder, false);
                        }
                    }
                    else
                    {
                        // Others / Unaccounted
                        Color color = entry.summaryType==SummaryType.Unaccounted ? new Color(barColor.r * 0.5f, barColor.g * 0.5f, barColor.b * 0.5f, barColor.a) : barColor;

                        DrawBar(x, y, w, height, color, selectedBorder, false);
                    }
                }

                Profiler.EndSample();

                m_2D.DrawEnd();
            }

            GUIStyle centreAlignStyle = new GUIStyle(GUI.skin.label);
            centreAlignStyle.alignment = TextAnchor.MiddleCenter;
            centreAlignStyle.normal.textColor = m_TextColor;
            GUIStyle leftAlignStyle = new GUIStyle(GUI.skin.label);
            leftAlignStyle.alignment = TextAnchor.MiddleLeft;
            leftAlignStyle.normal.textColor = m_TextColor;
            Color contentColor = GUI.contentColor;

            Profiler.BeginSample("DrawText");
            foreach (MarkerSummaryEntry entry in m_MarkerSummary.entry)
            {
                String name = entry.name;

                float x = entry.x * width;
                float w = entry.w * width;
                float msAtMedian = entry.msAtMedian;

                if (entry.summaryType==SummaryType.Marker)
                {
                    Rect labelRect = new Rect(rect.x + x, rect.y, w, rect.height);
                    GUIStyle style = centreAlignStyle;
                    String displayName = "";
                    if (w >= 20)
                    {
                        displayName = name;
                        Vector2 size = centreAlignStyle.CalcSize(new GUIContent(name));
                        if (size.x > w)
                        {
                            var words = name.Split('.');
                            displayName = words[words.Length - 1];
                            style = leftAlignStyle;
                        }
                    }
                    float percentAtMedian = msAtMedian * 100 / timeRange;
                    string tooltip = string.Format("{0}\n{1:f2}% ({2} on median frame {3})\n\nMedian marker time (in currently selected frames)\n{4} on frame {5}",
                        name,
                        percentAtMedian, ToDisplayUnits(msAtMedian, true, 0), frameSummary.medianFrameIndex,
                        ToDisplayUnits(entry.msMedian, true, 0), entry.medianFrameIndex);
                    if (name == selectedPairingMarkerName)
                        style.normal.textColor = selectedText;
                    else
                        style.normal.textColor = m_TextColor;
                    GUI.Label(labelRect, new GUIContent(displayName, tooltip), style);

                    Event current = Event.current;
                    if (labelRect.Contains(current.mousePosition))
                    {
                        if (current.type == EventType.ContextClick)
                        {
                            GenericMenu menu = new GenericMenu();

                            menu.AddItem(Styles.menuItemSelectFramesInAll, false, () => m_ProfileAnalyzerWindow.SelectFramesContainingMarker(name, false));
                            menu.AddItem(Styles.menuItemSelectFramesInCurrent, false, () => m_ProfileAnalyzerWindow.SelectFramesContainingMarker(name, true));

                            if (m_ProfileAnalyzerWindow.AllSelected())
                                menu.AddDisabledItem(Styles.menuItemSelectFramesAll);
                            else
                                menu.AddItem(Styles.menuItemSelectFramesAll, false, () => m_ProfileAnalyzerWindow.SelectAllFrames());

                            /*
                            if (m_ProfileAnalyzerWindow.AllSelected() || m_ProfileAnalyzerWindow.HasSelection())
                                menu.AddItem(Styles.menuItemClearSelection, false, () => m_ProfileAnalyzerWindow.ClearSelection());
                            else
                                menu.AddDisabledItem(Styles.menuItemClearSelection);
                            */

                            menu.AddSeparator("");
                            if (!m_NameFilters.Contains(name))
                                menu.AddItem(Styles.menuItemAddToIncludeFilter, false, () => m_ProfileAnalyzerWindow.AddToIncludeFilter(name));
                            else
                                menu.AddItem(Styles.menuItemRemoveFromIncludeFilter, false, () => m_ProfileAnalyzerWindow.RemoveFromIncludeFilter(name));
                            if (!m_NameExcludes.Contains(name))
                                menu.AddItem(Styles.menuItemAddToExcludeFilter, false, () => m_ProfileAnalyzerWindow.AddToExcludeFilter(name));
                            else
                                menu.AddItem(Styles.menuItemRemoveFromExcludeFilter, false, () => m_ProfileAnalyzerWindow.RemoveFromExcludeFilter(name));
                            menu.AddSeparator("");
                            menu.AddItem(Styles.menuItemSetAsParentMarkerFilter, false, () => m_ProfileAnalyzerWindow.SetAsParentMarkerFilter(name));
                            menu.AddItem(Styles.menuItemClearParentMarkerFilter, false, () => m_ProfileAnalyzerWindow.SetAsParentMarkerFilter(""));
                            menu.AddSeparator("");
                            menu.AddItem(Styles.menuItemCopyToClipboard, false, () => CopyToClipboard(current, name));

                            menu.ShowAsContext();

                            current.Use();
                        }
                        if (current.type == EventType.MouseDown)
                        {
                            m_ProfileAnalyzerWindow.SelectMarker(name);
                            m_ProfileAnalyzerWindow.RequestRepaint();
                        }
                    }
                }
                else
                {
                    DrawBarText(rect, x, w, msAtMedian, name, timeRange, leftAlignStyle, frameSummary.medianFrameIndex);
                }
            }

            Profiler.EndSample();
        }

        static float CaculateWidth(float x, float msTime, float msToWidth, float width)
        {
            float w = msTime * msToWidth;
            if (x + w > width)
                w = width - x;

            return w;
        }

        float DrawBar(float x, float y, float w, float height, Color barColor, Color selectedBorder, bool withBorder)
        {
            if (withBorder)
                m_2D.DrawFilledBox(x + 1, y + 1, w, height - 2, selectedBorder);

            m_2D.DrawFilledBox(x + 2, y + 2, w - 2, height - 4, barColor);

            return w;
        }

        float DrawBarText(Rect rect, float x, float w, float msTime, string name, float timeRange, GUIStyle leftAlignStyle, int medianFrameIndex)
        {
            float width = rect.width;
            Rect labelRect = new Rect(rect.x + x, rect.y, w, rect.height);
            float percent = msTime / timeRange * 100;
            GUIStyle style = leftAlignStyle;
            string tooltip = string.Format("{0}\n{1:f2}% ({2} on median frame {3})",
                name,
                percent, 
                ToDisplayUnits(msTime, true, 0), 
                medianFrameIndex);
            GUI.Label(labelRect, new GUIContent("", tooltip), style);

            Event current = Event.current;
            if (labelRect.Contains(current.mousePosition))
            {
                if (current.type == EventType.ContextClick)
                {
                    GenericMenu menu = new GenericMenu();

                    menu.AddItem(Styles.menuItemSelectFramesAll, false, m_ProfileAnalyzerWindow.SelectAllFrames);
                    menu.ShowAsContext();

                    current.Use();
                }
                if (current.type == EventType.MouseDown)
                {
                    m_ProfileAnalyzerWindow.SelectMarker(null);
                    m_ProfileAnalyzerWindow.RequestRepaint();
                }
            }

            return w;
        }
        
        void CopyToClipboard(Event current, string text)
        {
            EditorGUIUtility.systemCopyBuffer = text;
        }
    }
}
