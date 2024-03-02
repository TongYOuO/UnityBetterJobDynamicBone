#if UNITY_WEBGL
// No multithread
#else
#define ENABLE_MULTITHREAD
#endif

using System.Collections.Generic;
using System.Threading;
using DynamicBone.Scripts.Collider;
using DynamicBone.Scripts.SerializeData;
using UnityEngine;
using UnityEngine.Serialization;

namespace DynamicBone.Scripts
{
    [AddComponentMenu("Dynamic Bone/Dynamic Bone")]
    public class DynamicBone : MonoBehaviour
    {
        /// <summary>
        /// General processing.
        /// 分离配置和逻辑,process专门处理逻辑
        /// </summary>
        private DynamicBoneProcess process = new DynamicBoneProcess();

        public DynamicBoneProcess Process
        {
            get
            {
                process.cloth = this;
                return process;
            }
        }

        /// <summary>
        /// 单独拆出来一个实际配置时候使用的序列化数据,可以运行时修改,支持模板导入导出
        /// </summary>
        [SerializeField]
        private DynamicBoneSerializeData serializeData = new DynamicBoneSerializeData();

        public DynamicBoneSerializeData SerializeData => serializeData;
    
        public enum UpdateMode
        {
            Normal,
            AnimatePhysics,
            UnscaledTime,
            Default
        }
        
        public enum FreezeAxis
        {
            None, X, Y, Z
        }
        
        // public bool IsValid()
        // {
        //     return DynamicBoneManager.IsPlaying() && Process.IsValid() && Process.TeamId > 0;
        // }
        
        //分离逻辑和数据
        //=====================================================
        private void OnValidate()
        {
            Process.DataUpdate();
        }

        private void Awake()
        {
            Process.Init();
        }

        private void OnEnable()
        {
            Process.StartUse();
        }

        private void OnDisable()
        {
            Process.EndUse();
        }

        void Start()
        {
            Process.AutoBuild();
        }

        private void OnDestroy()
        {
            Process.Dispose();
        }

        #region  兼容原本的序列化配置
        
        public bool isConverted = false;
        //TODO:后续统一用工具刷一遍
        public DynamicBone.UpdateMode m_UpdateMode = DynamicBone.UpdateMode.AnimatePhysics;
        
#if UNITY_5_3_OR_NEWER
        [Tooltip("The roots of the transform hierarchy to apply physics.")]
#endif
        public Transform m_Root = null;
        public List<Transform> m_Roots = null;
        
#if UNITY_5_3_OR_NEWER
        [Tooltip("Internal physics simulation rate.")]
#endif
        public float m_UpdateRate = 60.0f;
        
#if UNITY_5_3_OR_NEWER
        [Tooltip("How much the bones slowed down.")]
#endif
        [Range(0, 1)]
        public float m_Damping = 0.1f;
        public AnimationCurve m_DampingDistrib = null;

#if UNITY_5_3_OR_NEWER
        [Tooltip("How much the force applied to return each bone to original orientation.")]
#endif
        [Range(0, 1)]
        public float m_Elasticity = 0.1f;
        public AnimationCurve m_ElasticityDistrib = null;

#if UNITY_5_3_OR_NEWER
        [Tooltip("How much bone's original orientation are preserved.")]
#endif
        [Range(0, 1)]
        public float m_Stiffness = 0.1f;
        public AnimationCurve m_StiffnessDistrib = null;

#if UNITY_5_3_OR_NEWER
        [Tooltip("How much character's position change is ignored in physics simulation.")]
#endif
        [Range(0, 1)]
        public float m_Inert = 0;
        public AnimationCurve m_InertDistrib = null;

#if UNITY_5_3_OR_NEWER
        [Tooltip("How much the bones slowed down when collide.")]
#endif
        public float m_Friction = 0;
        public AnimationCurve m_FrictionDistrib = null;

#if UNITY_5_3_OR_NEWER
        [Tooltip("Each bone can be a sphere to collide with colliders. Radius describe sphere's size.")]
#endif
        public float m_Radius = 0;
        public AnimationCurve m_RadiusDistrib = null;

#if UNITY_5_3_OR_NEWER
        [Tooltip("If End Length is not zero, an extra bone is generated at the end of transform hierarchy.")]
#endif
        public float m_EndLength = 0;

#if UNITY_5_3_OR_NEWER
        [Tooltip("If End Offset is not zero, an extra bone is generated at the end of transform hierarchy.")]
#endif
        public Vector3 m_EndOffset = Vector3.zero;

#if UNITY_5_3_OR_NEWER
        [Tooltip("The force apply to bones. Partial force apply to character's initial pose is cancelled out.")]
#endif
        public Vector3 m_Gravity = Vector3.zero;

#if UNITY_5_3_OR_NEWER
        [Tooltip("The force apply to bones.")]
#endif
        public Vector3 m_Force = Vector3.zero;

#if UNITY_5_3_OR_NEWER
        [Tooltip("Control how physics blends with existing animation.")]
#endif
        [Range(0, 1)]
        public float m_BlendWeight = 1.0f;

#if UNITY_5_3_OR_NEWER
        [Tooltip("Collider objects interact with the bones.")]
#endif
        public List<DynamicBoneColliderBase> m_Colliders = null;

#if UNITY_5_3_OR_NEWER
        [Tooltip("Bones exclude from physics simulation.")]
#endif
        public List<Transform> m_Exclusions = null;
        
#if UNITY_5_3_OR_NEWER
        [Tooltip("Constrain bones to move on specified plane.")]
#endif	
        public DynamicBone.FreezeAxis m_FreezeAxis = DynamicBone.FreezeAxis.None;

#if UNITY_5_3_OR_NEWER
        [Tooltip("Disable physics simulation automatically if character is far from camera or player.")]
#endif
        public bool m_DistantDisable = false;
        public Transform m_ReferenceObject = null;
        public float m_DistanceToObject = 20;
        

        #endregion
    }
}
