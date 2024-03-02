using System.Collections.Generic;
using DynamicBone.Scripts.Collider;
using DynamicBone.Scripts.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace DynamicBone.Scripts
{
    /// <summary>
    /// 重构Collider模块,分离逻辑和参数,挂载的Collider只是用来配置,而真正的逻辑处理在ColliderManager中
    /// </summary>
    public class DynamicBoneColliderManager: IDynamicBoneManager, IDynamicBoneValid
    {
        // private TransformAccessArray _colliderTransformAccessArray;
        // private ExNativeArray<ColliderInfo> _colliderInfoList;
        // private List<DynamicBoneColliderBase> _colliderList;
        public bool isValid;

        public enum ColliderType
        {
            
        }
        
        public struct ColliderInfo
        {
            public int m_TeamIndex;
            public int m_Index;

            public ColliderType ColliderType;
            
            public bool m_IsGlobal;
            public DynamicBoneColliderBase.Bound m_Bound;
            public float m_Height;
            public float m_Radius;
            public float3 m_Center;
            public DynamicBoneColliderBase.Direction m_Direction;
            public float m_Scale;
            public float3 m_Position;
            public quaternion m_Rotation;
        }
        
        public ExNativeArray<ColliderInfo> ColliderInfoNativeArrayList;
        
        //TODO:LateUpdate时进行数据的Prepare
        // [BurstCompile]
        // private struct ColliderSetupJob : IJobParallelForTransform
        // {
        //     public NativeArray<ColliderInfo> m_ColliderArray;
        //
        //     public void Execute(int index, TransformAccess transform)
        //     {
        //         ColliderInfo colliderInfo = m_ColliderArray[index];
        //         colliderInfo.m_Position = transform.position;
        //         colliderInfo.m_Rotation = transform.rotation;
        //         // colliderInfo.Scale = transform.localScale.x;
        //         m_ColliderArray[index] = colliderInfo;
        //     }
        // }
        
        //TODO:在算完一部分约束时进行Collide处理
        
        
        /// <summary>
        /// 将每个Team关联的Collider添加到NativeArray的结构中
        /// </summary>
        /// <param name="target"></param>
        public DataChunk AddColliders(List<DynamicBoneColliderBase> colliderList)
        {
            ColliderInfo[] colliderInfos = new ColliderInfo[colliderList.Count];
            
            for (int i = 0; i < colliderList.Count; i++)
            {
                colliderInfos[i] = colliderList[i].ColliderInfo;
            }

            return ColliderInfoNativeArrayList.AddRange(colliderInfos);
        }
        
        public void Dispose()
        {
            // _colliderInfoList.Dispose();
            ColliderInfoNativeArrayList.Dispose();
            // _colliderList.Clear();
            isValid = false;
        }

        public void Initialize()
        {
            const int capacity = 32;
            ColliderInfoNativeArrayList = new ExNativeArray<ColliderInfo>(capacity);
            isValid = true;
        }

        public bool IsValid()
        {
            return isValid;
        }

        public void RemoveColliders(DataChunk mColliderInfoChunk)
        {
            ColliderInfoNativeArrayList.RemoveAndFill(mColliderInfoChunk);
        }
    }
}