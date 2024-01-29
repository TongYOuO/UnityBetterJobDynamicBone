using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.LowLevel;
namespace DynamicBone.Scripts
{
    /// <summary>
    /// 主要负责全局Mgr的生命周期的初始化,具体每一个ParticleTree的初始化放在每个DynamicBone的Mono behavior中,借鉴了MagicaCloth的Jobs管线,删减之后结合DynamicBone的算法
    /// </summary>
    public class DynamicBoneManager
    {
        static List<IDynamicBoneManager> managers = null;
        
        public static DynamicBoneTimeManager Time => managers?[0] as DynamicBoneTimeManager;
        
        public static DynamicBoneTeamManager Team => managers?[1] as DynamicBoneTeamManager;
        
        public static DynamicBoneSimulationManager Simulation => managers?[2] as DynamicBoneSimulationManager;
        
        public static DynamicBoneColliderManager Collider => managers?[3] as DynamicBoneColliderManager;
        
        public static DynamicBoneClothManager Cloth => managers?[4] as DynamicBoneClothManager;
        
        static volatile bool isPlaying = false;

        public static DynamicBone.UpdateMode m_UpdateMode = DynamicBone.UpdateMode.Normal;
        
        public static float m_UpdateRate = 30.0f;
        
        #region PlayerLoopDelegate

        // player loop delegate
        public delegate void UpdateMethod();

        /// <summary>
        /// 帧开始时，在所有EarlyUpdate之后，在FixedUpdate（）之前
        /// </summary>
        public static UpdateMethod afterEarlyUpdateDelegate;

        /// <summary>
        /// FixedUpdate()后
        /// </summary>
        public static UpdateMethod afterFixedUpdateDelegate;

        /// <summary>
        /// Update()后
        /// </summary>
        public static UpdateMethod afterUpdateDelegate;

        /// <summary>
        /// LateUpdate()前
        /// </summary>
        public static UpdateMethod beforeLateUpdateDelegate;

        /// <summary>
        /// LateUpdate()之后
        /// </summary>
        public static UpdateMethod afterLateUpdateDelegate;

        /// <summary>
        /// 编辑器由EditorApplication.update委托
        /// </summary>
        public static UpdateMethod defaultUpdateDelegate;
        
        /// <summary>
        /// 渲染完成后
        /// </summary>
        public static UpdateMethod afterRenderingDelegate;
        
        #endregion
        
        static void Dispose()
        {
            if (managers != null)
            {
                foreach (var manager in managers)
                    manager.Dispose();
                managers = null;
            }

            // clear static member.
            // OnPreSimulation = null;
            // OnPostSimulation = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Initialize()
        {
            //上来先清一下Mgr
            Dispose();
            
            managers = new List<IDynamicBoneManager>();
            managers.Add(new DynamicBoneTimeManager());//0
            managers.Add(new DynamicBoneTeamManager());//1
            managers.Add(new DynamicBoneSimulationManager());//2
            managers.Add(new DynamicBoneColliderManager());//3
            managers.Add(new DynamicBoneClothManager());//4
            
            foreach (var manager in managers)
                manager.Initialize();
            
            InitCustomGameLoop();
            
            Debug.Log("[DynamicBoneManager] Initialize");
            isPlaying = true;
        }
        
        /// <summary>
        /// 检查是否已经添加过PlayerLoop
        /// </summary>
        /// <param name="playerLoop"></param>
        /// <returns></returns>
        static bool CheckRegist(ref PlayerLoopSystem playerLoop)
        {
            var t = typeof(DynamicBoneManager);
            foreach (var subloop in playerLoop.subSystemList)
            {
                if (subloop.subSystemList != null && subloop.subSystemList.Any(x => x.type == t))
                {
                    return true;
                }
            }
            return false;
        }
        
        public static void InitCustomGameLoop()
        {
            PlayerLoopSystem playerLoop = PlayerLoop.GetCurrentPlayerLoop();

            // 如果已经加过则不去重复添加
            if (CheckRegist(ref playerLoop))
            {
                return;
            }

            // 使用PlayerLoop追加,相比DynamicBone原生LateUpdate操作更精确好用
            SetCustomGameLoop(ref playerLoop);

            PlayerLoop.SetPlayerLoop(playerLoop);
        }

        /// <summary>
        /// 追加自定义的生命周期
        /// </summary>
        /// <param name="playerLoop"></param>
        static void SetCustomGameLoop(ref PlayerLoopSystem playerLoop)
        {
            //所有EarlyUpdate之后,同时FixedUpdate之前
            PlayerLoopSystem afterEarlyUpdate = new PlayerLoopSystem()
            {
                type = typeof(DynamicBoneManager),
                updateDelegate = () => afterEarlyUpdateDelegate?.Invoke()
            };
            AddPlayerLoop(afterEarlyUpdate, ref playerLoop, "EarlyUpdate", string.Empty, last: true);
            
            //FixedUpdate之后
            PlayerLoopSystem afterFixedUpdate = new PlayerLoopSystem()
            {
                type = typeof(DynamicBoneManager),
                updateDelegate = () =>
                {
                    afterFixedUpdateDelegate?.Invoke();
                }
            };
            AddPlayerLoop(afterFixedUpdate, ref playerLoop, "FixedUpdate", "ScriptRunBehaviourFixedUpdate");
            
            //Update之后
            PlayerLoopSystem afterUpdate = new PlayerLoopSystem()
            {
                type = typeof(DynamicBoneManager),
                updateDelegate = () =>
                {
                    afterUpdateDelegate?.Invoke();

                    if (Application.isPlaying)
                    {
                        defaultUpdateDelegate?.Invoke();
                    }
                }
            };
            AddPlayerLoop(afterUpdate, ref playerLoop, "Update", "ScriptRunDelayedTasks");
            
            //LateUpdate之前
            PlayerLoopSystem beforeLateUpdate = new PlayerLoopSystem()
            {
                type = typeof(DynamicBoneManager),
                updateDelegate = () => beforeLateUpdateDelegate?.Invoke()
            };
            AddPlayerLoop(beforeLateUpdate, ref playerLoop, "PreLateUpdate", "ScriptRunBehaviourLateUpdate", before: true);
            
            //LateUpdate之后
            PlayerLoopSystem afterLateUpdate = new PlayerLoopSystem()
            {
                type = typeof(DynamicBoneManager),
                updateDelegate = () => afterLateUpdateDelegate?.Invoke()
            };
            AddPlayerLoop(afterLateUpdate, ref playerLoop, "PreLateUpdate", "ScriptRunBehaviourLateUpdate");
            
            PlayerLoopSystem afterRendering = new PlayerLoopSystem()
            {
                type = typeof(DynamicBoneManager),
                updateDelegate = () => afterRenderingDelegate?.Invoke()
            };
            AddPlayerLoop(afterRendering, ref playerLoop, "PostLateUpdate", "FinishFrameRendering");
        }
        
        static void AddPlayerLoop(PlayerLoopSystem method, ref PlayerLoopSystem playerLoop, string categoryName, string systemName, bool last = false, bool before = false)
        {
            int sysIndex = Array.FindIndex(playerLoop.subSystemList, (s) => s.type.Name == categoryName);
            PlayerLoopSystem category = playerLoop.subSystemList[sysIndex];
            var systemList = new List<PlayerLoopSystem>(category.subSystemList);

            if (last)
            {
                systemList.Add(method);
            }
            else
            {
                int index = systemList.FindIndex(h => h.type.Name.Contains(systemName));
                if (before)
                    systemList.Insert(index, method);
                else
                    systemList.Insert(index + 1, method);
            }

            category.subSystemList = systemList.ToArray();
            playerLoop.subSystemList[sysIndex] = category;
        }
        
        public static bool IsPlaying()
        {
            //return isPlaying;
            return isPlaying && Application.isPlaying;
        }
    }
}
