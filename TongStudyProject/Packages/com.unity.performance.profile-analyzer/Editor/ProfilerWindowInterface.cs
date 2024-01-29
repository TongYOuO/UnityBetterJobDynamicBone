using UnityEditorInternal;
using System.Reflection;
using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Profiling;
using UnityEditor.Profiling;

namespace UnityEditor.Performance.ProfileAnalyzer
{
    internal class ProfilerWindowInterface
    {
#if UNITY_2020_1_OR_NEWER
        bool s_UseRawIterator = true;
#endif        
        ProgressBarDisplay m_progressBar;

        Type m_ProfilerWindowType;
        EditorWindow m_ProfilerWindow;
        FieldInfo m_CurrentFrameFieldInfo;
        FieldInfo m_TimeLineGUIFieldInfo;
        FieldInfo m_SelectedEntryFieldInfo;
        FieldInfo m_SelectedNameFieldInfo;
        FieldInfo m_SelectedTimeFieldInfo;
        FieldInfo m_SelectedDurationFieldInfo;
        FieldInfo m_SelectedInstanceIdFieldInfo;
        FieldInfo m_SelectedInstanceCountFieldInfo;
        FieldInfo m_SelectedFrameIdFieldInfo;
        FieldInfo m_SelectedThreadIdFieldInfo;
        FieldInfo m_SelectedNativeIndexFieldInfo;

        MethodInfo m_GetProfilerModuleInfo;
        Type m_CPUProfilerModuleType;

        public ProfilerWindowInterface(ProgressBarDisplay progressBar)
        {
            m_progressBar = progressBar;

            Assembly assem = typeof(Editor).Assembly;
            m_ProfilerWindowType = assem.GetType("UnityEditor.ProfilerWindow");
            m_CurrentFrameFieldInfo = m_ProfilerWindowType.GetField("m_CurrentFrame", BindingFlags.NonPublic | BindingFlags.Instance);

            m_TimeLineGUIFieldInfo = m_ProfilerWindowType.GetField("m_CPUTimelineGUI", BindingFlags.NonPublic | BindingFlags.Instance);
            if (m_TimeLineGUIFieldInfo == null)
            {
                // m_CPUTimelineGUI isn't present in 2019.3.0a8 onward
                m_GetProfilerModuleInfo = m_ProfilerWindowType.GetMethod("GetProfilerModule", BindingFlags.NonPublic | BindingFlags.Instance);
                if (m_GetProfilerModuleInfo == null)
                {
                    Debug.Log("Unable to initialise link to Profiler Timeline, no GetProfilerModule found");
                }

                m_CPUProfilerModuleType = assem.GetType("UnityEditorInternal.Profiling.CPUProfilerModule");
                m_TimeLineGUIFieldInfo = m_CPUProfilerModuleType.GetField("m_TimelineGUI", BindingFlags.NonPublic | BindingFlags.Instance);
                if (m_TimeLineGUIFieldInfo == null)
                {
                    Debug.Log("Unable to initialise link to Profiler Timeline");
                }
            }

            if (m_TimeLineGUIFieldInfo != null)
                m_SelectedEntryFieldInfo = m_TimeLineGUIFieldInfo.FieldType.GetField("m_SelectedEntry", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (m_SelectedEntryFieldInfo != null)
            {
                m_SelectedNameFieldInfo = m_SelectedEntryFieldInfo.FieldType.GetField("name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                m_SelectedTimeFieldInfo = m_SelectedEntryFieldInfo.FieldType.GetField("time", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                m_SelectedDurationFieldInfo = m_SelectedEntryFieldInfo.FieldType.GetField("duration", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                m_SelectedInstanceIdFieldInfo = m_SelectedEntryFieldInfo.FieldType.GetField("instanceId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                m_SelectedInstanceCountFieldInfo = m_SelectedEntryFieldInfo.FieldType.GetField("instanceCount", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                m_SelectedFrameIdFieldInfo = m_SelectedEntryFieldInfo.FieldType.GetField("frameId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                m_SelectedThreadIdFieldInfo = m_SelectedEntryFieldInfo.FieldType.GetField("threadId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                m_SelectedNativeIndexFieldInfo = m_SelectedEntryFieldInfo.FieldType.GetField("nativeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }

        }

        /*
        public EditorWindow GetProfileWindow()
        {
            return m_profilerWindow;
        }
        */

        public bool IsReady()
        {
            if (m_ProfilerWindow != null)
                return true;

            return false;
        }

        public void GetProfilerWindowHandle()
        {
            Profiler.BeginSample("GetProfilerWindowHandle");
            UnityEngine.Object[] windows = Resources.FindObjectsOfTypeAll(m_ProfilerWindowType);
            if (windows != null && windows.Length > 0)
                m_ProfilerWindow = (EditorWindow)windows[0];
            Profiler.EndSample();
        }

        public void OpenProfilerOrUseExisting()
        {
            // Note we use existing if possible to fix a bug after domain reload
            // Where calling EditorWindow.GetWindow directly causes a second window to open
            if (!m_ProfilerWindow)
            { 
                // Create new
                m_ProfilerWindow = EditorWindow.GetWindow(m_ProfilerWindowType);
            }
        }

        public bool GetFrameRangeFromProfiler(out int first, out int last)
        {
            if (m_ProfilerWindow)
            //if (ProfilerDriver.enabled)
            {
                first = 1 + ProfilerDriver.firstFrameIndex;
                last = 1 + ProfilerDriver.lastFrameIndex;
                // Clip to the visible frames in the profile which indents 1 in from end
                if (first < last)
                    last--;
                return true;
            }

            first = 1;
            last = 1;
            return false;
        }

        public void CloseProfiler()
        {
            if (m_ProfilerWindow)
                m_ProfilerWindow.Close();
        }

        public object GetTimeLineGUI()
        {
            object timeLineGUI = null;

            if (m_CPUProfilerModuleType != null)
            {
                object[] parametersArray = new object[] { ProfilerArea.CPU };
                var getCPUProfilerModuleInfo = m_GetProfilerModuleInfo.MakeGenericMethod(m_CPUProfilerModuleType);
                var cpuModule = getCPUProfilerModuleInfo.Invoke(m_ProfilerWindow, parametersArray);

                timeLineGUI = m_TimeLineGUIFieldInfo.GetValue(cpuModule);
            }
            else if (m_TimeLineGUIFieldInfo != null)
            {
                timeLineGUI = m_TimeLineGUIFieldInfo.GetValue(m_ProfilerWindow);
            }

            return timeLineGUI;
        }

        public string GetProfilerWindowMarkerName()
        {
            if (m_ProfilerWindow!=null)
            {
                var timeLineGUI = GetTimeLineGUI();
                if (timeLineGUI != null && m_SelectedEntryFieldInfo != null)
                {
                    var selectedEntry = m_SelectedEntryFieldInfo.GetValue(timeLineGUI);
                    if (selectedEntry != null && m_SelectedNameFieldInfo != null)
                    {
                        return m_SelectedNameFieldInfo.GetValue(selectedEntry).ToString();
                    }
                }
            }

            return null;
        }
        public ProfileData PullFromProfiler(int firstFrameDisplayIndex, int lastFrameDisplayIndex)
        {
            Profiler.BeginSample("ProfilerWindowInterface.PullFromProfiler");
            
            bool recording = IsRecording();
            if (recording)
                StopRecording();

            int firstFrameIndex = firstFrameDisplayIndex - 1;
            int lastFrameIndex = lastFrameDisplayIndex - 1;
            ProfileData profileData = GetData(firstFrameIndex, lastFrameIndex);

            if (recording)
                StartRecording();

            Profiler.EndSample();
            return profileData;
        }

#if UNITY_2020_1_OR_NEWER
        ProfileData GetDataRaw(int firstFrameIndex, int lastFrameIndex)
        {
            bool firstError = true;

            var data = new ProfileData();
            data.SetFrameIndexOffset(firstFrameIndex);

            var depthStack = new Stack<int>();

            var threadNameCount = new Dictionary<string, int>();
            var threadIdMapping = new Dictionary<ulong, string>();
            var markerIdToNameIndex = new Dictionary<int, int>();
            for (int frameIndex = firstFrameIndex; frameIndex <= lastFrameIndex; ++frameIndex)
            {
                m_progressBar.AdvanceProgressBar();

                int threadIndex = 0;

                bool threadValid = true;
                threadNameCount.Clear();
                ProfileFrame frame = null;
                while (threadValid)
                {
                    using (RawFrameDataView frameData = ProfilerDriver.GetRawFrameDataView(frameIndex, threadIndex))
                    {
                        if (threadIndex == 0)
                        {
                            frame = new ProfileFrame();
                            if (frameData.valid)
                            {
                                frame.msStartTime = frameData.frameStartTimeMs;
                                frame.msFrame = frameData.frameTimeMs;
                            }
                            data.Add(frame);
                        }

                        if (!frameData.valid)
                            break;

                        string threadNameWithIndex;

                        if (threadIdMapping.ContainsKey(frameData.threadId))
                        {
                            threadNameWithIndex = threadIdMapping[frameData.threadId];
                        }
                        else
                        {
                            string threadName = frameData.threadName;
                            if (threadName.Trim() == "")
                            {
                                Debug.Log(string.Format("Warning: Unnamed thread found on frame {0}. Corrupted data suspected, ignoring frame", frameIndex));
                                threadIndex++;
                                continue;
                            }
                            var groupName = frameData.threadGroupName;
                            threadName = ProfileData.GetThreadNameWithGroup(threadName, groupName);

                            int nameCount = 0;
                            threadNameCount.TryGetValue(threadName, out nameCount);
                            threadNameCount[threadName] = nameCount + 1;

                            threadNameWithIndex = ProfileData.ThreadNameWithIndex(threadNameCount[threadName], threadName);

                            threadIdMapping[frameData.threadId] = threadNameWithIndex;
                        }

                        var thread = new ProfileThread();
                        data.AddThreadName(threadNameWithIndex, thread);

                        frame.Add(thread);

                        // The markers are in depth first order 
                        depthStack.Clear();
                        // first sample is the thread name
                        for (int i = 1; i < frameData.sampleCount; i++)
                        {
                            float durationMS = frameData.GetSampleTimeMs(i);
                            int markerId = frameData.GetSampleMarkerId(i);
                            if (durationMS < 0)
                            {
                                if (firstError)
                                {
                                    int displayIndex = data.OffsetToDisplayFrame(frameIndex);
                                    string threadName = frameData.threadName;

                                    string name = frameData.GetSampleName(i);
                                    Debug.LogFormat("Ignoring Invalid marker time found for {0} on frame {1} on thread {2} ({3} < 0)",
                                            name, displayIndex, threadName, durationMS);

                                    firstError = false;
                                }
                            }
                            else
                            {
                                int depth = 1 + depthStack.Count;
                                var markerData = ProfileMarker.Create(durationMS, depth);

                                // Use name index directly if we have already stored this named marker before
                                int nameIndex;
                                if (markerIdToNameIndex.TryGetValue(markerId, out nameIndex))
                                {
                                    markerData.nameIndex = nameIndex;
                                }
                                else
                                {
                                    string name = frameData.GetSampleName(i);
                                    data.AddMarkerName(name, markerData);
                                    markerIdToNameIndex[markerId] = markerData.nameIndex;
                                }
                                
                                thread.Add(markerData);
                            }

                            int childrenCount = frameData.GetSampleChildrenCount(i);
                            if (childrenCount > 0)
                            {
                                depthStack.Push(childrenCount);
                            }
                            else
                            {
                                while (depthStack.Count > 0)
                                {
                                    int remainingChildren = depthStack.Pop();
                                    if (remainingChildren > 1)
                                    {
                                        depthStack.Push(remainingChildren - 1);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    threadIndex++;
                }
            }

            data.Finalise();

            return data;
        }
#endif

        ProfileData GetDataOriginal(int firstFrameIndex, int lastFrameIndex)
        {
            ProfilerFrameDataIterator frameData = new ProfilerFrameDataIterator();
            bool firstError = true;

            var data = new ProfileData();
            data.SetFrameIndexOffset(firstFrameIndex);

            Dictionary<string, int> threadNameCount = new Dictionary<string, int>();
            for (int frameIndex = firstFrameIndex; frameIndex <= lastFrameIndex; ++frameIndex)
            {
                m_progressBar.AdvanceProgressBar();

                int threadCount = frameData.GetThreadCount(frameIndex);
                frameData.SetRoot(frameIndex, 0);

                var msFrame = frameData.frameTimeMS;

                /*
                if (frameIndex == lastFrameIndex)
                {
                    // Check if last frame appears to be invalid data
                    float median;
                    float mean;
                    float standardDeviation;
                    CalculateFrameTimeStats(data, out median, out mean, out standardDeviation);
                    float execessiveDeviation = (3f * standardDeviation);
                    if (msFrame > (median + execessiveDeviation))
                    {
                        Debug.LogFormat("Dropping last frame as it is significantly larger than the median of the rest of the data set {0} > {1} (median {2} + 3 * standard deviation {3})", msFrame, median + execessiveDeviation, median, standardDeviation);
                        break;
                    }
                    if (msFrame < (median - execessiveDeviation))
                    {
                        Debug.LogFormat("Dropping last frame as it is significantly smaller than the median of the rest of the data set {0} < {1} (median {2} - 3 * standard deviation {3})", msFrame, median - execessiveDeviation, median, standardDeviation);
                        break;
                    }
                }
                */

                ProfileFrame frame = new ProfileFrame();
                frame.msStartTime = 1000.0 * frameData.GetFrameStartS(frameIndex);
                frame.msFrame = msFrame;
                data.Add(frame);

                threadNameCount.Clear();
                for (int threadIndex = 0; threadIndex < threadCount; ++threadIndex)
                {
                    frameData.SetRoot(frameIndex, threadIndex);

                    var threadName = frameData.GetThreadName();
                    if (threadName.Trim() == "")
                    {
                        Debug.Log(string.Format("Warning: Unnamed thread found on frame {0}. Corrupted data suspected, ignoring frame", frameIndex));
                        continue;
                    }

                    var groupName = frameData.GetGroupName();
                    threadName = ProfileData.GetThreadNameWithGroup(threadName, groupName);

                    ProfileThread thread = new ProfileThread();
                    frame.Add(thread);

                    int nameCount = 0;
                    threadNameCount.TryGetValue(threadName, out nameCount);
                    threadNameCount[threadName] = nameCount + 1;

                    data.AddThreadName(ProfileData.ThreadNameWithIndex(threadNameCount[threadName], threadName), thread);

                    const bool enterChildren = true;
                    // The markers are in depth first order and the depth is known
                    // So we can infer a parent child relationship
                    while (frameData.Next(enterChildren))
                    {
                        if (frameData.durationMS < 0)
                        {
                            if (firstError)
                            {
                                int displayIndex = data.OffsetToDisplayFrame(frameIndex);

                                Debug.LogFormat("Ignoring Invalid marker time found for {0} on frame {1} on thread {2} ({3} < 0) : Instance id : {4}",
                                    frameData.name, displayIndex, threadName, frameData.durationMS, frameData.instanceId);

                                firstError = false;
                            }
                            continue;
                        }
                        var markerData = ProfileMarker.Create(frameData);

                        data.AddMarkerName(frameData.name, markerData);
                        thread.Add(markerData);
                    }
                }
            }

            data.Finalise();

            frameData.Dispose();
            return data;
        }


        ProfileData GetData(int firstFrameIndex, int lastFrameIndex)
        {
            //s_UseRawIterator ^= true;

            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            ProfileData data;
#if UNITY_2020_1_OR_NEWER
            if (s_UseRawIterator)
            {
                data = GetDataRaw(firstFrameIndex, lastFrameIndex);
            }
            else
#endif
            {
                data = GetDataOriginal(firstFrameIndex, lastFrameIndex);
            }

            stopwatch.Stop();
            //Debug.LogFormat("Pull time {0}ms ({1})", stopwatch.ElapsedMilliseconds, s_UseRawIterator ? "Raw" : "Standard");
           
            return data;
        }

#if UNITY_2020_1_OR_NEWER
        public float GetFrameTimeRaw(int frameIndex)
        {
            using (RawFrameDataView frameData = ProfilerDriver.GetRawFrameDataView(frameIndex, 0))
            {
                if (!frameData.valid)
                    return 0f;

                return frameData.frameTimeMs;
            }
        }
#endif


        public float GetFrameTime(int frameIndex)
        {
#if UNITY_2020_1_OR_NEWER
            if (s_UseRawIterator)
                return GetFrameTimeRaw(frameIndex);
#endif
            ProfilerFrameDataIterator frameData = new ProfilerFrameDataIterator();

            frameData.SetRoot(frameIndex, 0);
            float ms = frameData.frameTimeMS;
            frameData.Dispose();

            return ms;
        }

        bool GetMarkerInfo(string markerName, int frameIndex, List<string> threadFilters, out int outThreadIndex, out float time, out float duration, out int instanceId)
        {
            ProfilerFrameDataIterator frameData = new ProfilerFrameDataIterator();

            outThreadIndex = 0;
            time = 0.0f;
            duration = 0.0f;
            instanceId = 0;
            bool found = false;

            int threadCount = frameData.GetThreadCount(frameIndex);
            Dictionary<string, int> threadNameCount = new Dictionary<string, int>();
            for (int threadIndex = 0; threadIndex < threadCount; ++threadIndex)
            {
                frameData.SetRoot(frameIndex, threadIndex);

                var threadName = frameData.GetThreadName();
                // Name here could be "Worker Thread 1"

                var groupName = frameData.GetGroupName();
                threadName = ProfileData.GetThreadNameWithGroup(threadName, groupName);

                int nameCount = 0;
                threadNameCount.TryGetValue(threadName, out nameCount);
                threadNameCount[threadName] = nameCount + 1;

                var threadNameWithIndex = ProfileData.ThreadNameWithIndex(threadNameCount[threadName], threadName);

                // To compare on the filter we need to remove the postfix on the thread name
                // "3:Worker Thread 0" -> "1:Worker Thread"
                // The index of the thread (0) is used +1 as a prefix 
                // The preceding number (3) is the count of number of threads with this name
                // Unfortunately multiple threads can have the same name
                threadNameWithIndex = ProfileData.CorrectThreadName(threadNameWithIndex);

                if (threadFilters.Contains(threadNameWithIndex))
                {
                    const bool enterChildren = true;
                    while (frameData.Next(enterChildren))
                    {
                        if (frameData.name == markerName)
                        {
                            time = frameData.startTimeMS;
                            duration = frameData.durationMS;
                            instanceId = frameData.instanceId;
                            outThreadIndex = threadIndex;
                            found = true;
                            break;
                        }
                    }
                }

                if (found)
                    break;
            }

            frameData.Dispose();
            return found;
        }

        public void SetProfilerWindowMarkerName(string markerName, List<string> threadFilters)
        {
            if (m_ProfilerWindow == null)
                return;

            var timeLineGUI = GetTimeLineGUI();
            if (timeLineGUI==null)
                return;

            if (m_SelectedEntryFieldInfo != null)
            {
                var selectedEntry = m_SelectedEntryFieldInfo.GetValue(timeLineGUI);
                if (selectedEntry != null)
                {
                    // Read profiler data direct from profile to find time/duration
                    int currentFrameIndex = (int)m_CurrentFrameFieldInfo.GetValue(m_ProfilerWindow);
                    float time;
                    float duration;
                    int instanceId;
                    int threadIndex;
                    if (GetMarkerInfo(markerName, currentFrameIndex, threadFilters, out threadIndex, out time, out duration, out instanceId))
                    {
                        /*
                        Debug.Log(string.Format("Setting profiler to {0} on {1} at frame {2} at {3}ms for {4}ms ({5})", 
                                                markerName, currentFrameIndex, threadFilter, time, duration, instanceId));
                         */
                        
                        if (m_SelectedNameFieldInfo != null)
                            m_SelectedNameFieldInfo.SetValue(selectedEntry, markerName);
                        if (m_SelectedTimeFieldInfo != null)
                            m_SelectedTimeFieldInfo.SetValue(selectedEntry, time);
                        if (m_SelectedDurationFieldInfo != null)
                            m_SelectedDurationFieldInfo.SetValue(selectedEntry, duration);
                        if (m_SelectedInstanceIdFieldInfo != null)
                            m_SelectedInstanceIdFieldInfo.SetValue(selectedEntry, instanceId);
                        if (m_SelectedFrameIdFieldInfo != null)
                            m_SelectedFrameIdFieldInfo.SetValue(selectedEntry, currentFrameIndex);
                        if (m_SelectedThreadIdFieldInfo != null)
                            m_SelectedThreadIdFieldInfo.SetValue(selectedEntry, threadIndex);
                        
                        // TODO : Update to fill in the total and number of instances.
                        // For now we force Instance count to 1 to avoid the incorrect info showing.
                        if (m_SelectedInstanceCountFieldInfo != null)
                            m_SelectedInstanceCountFieldInfo.SetValue(selectedEntry, 1);

                        // Set other values to non negative values so selection appears
                        if (m_SelectedNativeIndexFieldInfo != null)
                            m_SelectedNativeIndexFieldInfo.SetValue(selectedEntry, currentFrameIndex);

                        m_ProfilerWindow.Repaint();
                    }
                }
            }
        }

        public bool JumpToFrame(int index)
        {
            //if (!ProfilerDriver.enabled)
            //    return;

            if (!m_ProfilerWindow)
                return false;

            if (index - 1 < ProfilerDriver.firstFrameIndex)
                return false;
            if (index - 1 > ProfilerDriver.lastFrameIndex)
                return false;

            m_CurrentFrameFieldInfo.SetValue(m_ProfilerWindow, index - 1);
            m_ProfilerWindow.Repaint();
            return true;
        }

        public bool IsRecording()
        {
#if UNITY_2017_4_OR_NEWER
            return ProfilerDriver.enabled;
#else
            return false;
#endif
        }

        public void StopRecording()
        {
#if UNITY_2017_4_OR_NEWER
            // Stop recording first
            ProfilerDriver.enabled = false;
#endif
        }

        public void StartRecording()
        {
#if UNITY_2017_4_OR_NEWER
            // Stop recording first
            ProfilerDriver.enabled = true;
#endif
        }
    }
}
