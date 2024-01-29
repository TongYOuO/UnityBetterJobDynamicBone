using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
namespace DynamicBone.Scripts.SerializeData
{
    [System.Serializable]
    public class DynamicBoneSerializeData
    {
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
        
        public string ExportJson()
        {
                return JsonUtility.ToJson(this);
        }
        
        public bool ImportJson(string json)
        {
                try
                {
                        // 用TempBuffer过滤一些不需要被覆盖的数据
                        var temp = new TempBuffer(this);

                        // Import
                        JsonUtility.FromJsonOverwrite(json, this);

                        temp.Pop(this);
                        
                        //DataValidate();

                        return true;
                }
                catch (Exception e)
                {
                        Debug.LogException(e);
                        return false;
                }
        }
        class TempBuffer
        {
                
            //TODO:选择性的屏蔽一些变量
            // float blendWeight;
            // CullingSettings.CameraCullingMode cullingMode;
            // CullingSettings.CameraCullingMethod cullingMethod;
            // List<Renderer> cullingRenderers;

            internal TempBuffer(DynamicBoneSerializeData sdata)
            {
                Push(sdata);
            }

            internal void Push(DynamicBoneSerializeData sdata)
            {
                // normalAxis = sdata.normalAxis;
                // colliderList = new List<ColliderComponent>(sdata.colliderCollisionConstraint.colliderList);
                // collisionBones = new List<Transform>(sdata.colliderCollisionConstraint.collisionBones);
                // synchronization = sdata.selfCollisionConstraint.syncPartner;
                // stablizationTimeAfterReset = sdata.stablizationTimeAfterReset;
                // blendWeight = sdata.blendWeight;
                // cullingMode = sdata.cullingSettings.cameraCullingMode;
                // cullingMethod = sdata.cullingSettings.cameraCullingMethod;
                // cullingRenderers = new List<Renderer>(sdata.cullingSettings.cameraCullingRenderers);
            }

            internal void Pop(DynamicBoneSerializeData sdata)
            {
               
                // sdata.selfCollisionConstraint.syncPartner = synchronization;
                // sdata.stablizationTimeAfterReset = stablizationTimeAfterReset;
                // sdata.blendWeight = blendWeight;
                // sdata.cullingSettings.cameraCullingMode = cullingMode;
                // sdata.cullingSettings.cameraCullingMethod = cullingMethod;
                // sdata.cullingSettings.cameraCullingRenderers = cullingRenderers;
            }
        }
    }
}