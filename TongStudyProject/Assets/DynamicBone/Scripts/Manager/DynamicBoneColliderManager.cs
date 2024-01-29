using System.Collections.Generic;
using DynamicBone.Scripts.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace DynamicBone.Scripts
{
    public class DynamicBoneColliderManager: IDynamicBoneManager, IDynamicBoneValid
    {
        // private TransformAccessArray _colliderTransformAccessArray;
        // private ExNativeArray<ColliderInfo> _colliderInfoList;
        // private List<DynamicBoneColliderBase> _colliderList;
        public bool isValid;
        public struct ColliderInfo
        {
            public int m_Index;
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
        //
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
        
        /// <summary>
        /// 将目标骨骼添加到结构中
        /// </summary>
        /// <param name="target"></param>
        // public void AddCollider(DynamicBoneColliderBase target)
        // {
        //     int index = _colliderList.IndexOf(target);
        //
        //     if (index != -1) return; //防止重复添加
        //
        //     _colliderList.Add(target);
        //
        //     int colliderIndex = _colliderInfoList.Length;
        //     target.ColliderInfo.m_Index = colliderIndex;
        //
        //     _colliderInfoList.Add(target.ColliderInfo);
        //     _colliderTransformAccessArray.Add(target.transform);
        //     
        // }
        public void Dispose()
        {
            // _colliderInfoList.Dispose();
            // _colliderTransformAccessArray.Dispose();
            // _colliderList.Clear();
            // isValid = false;
        }

        public void Initialize()
        {
            // const int capacity = 32;
            // _colliderTransformAccessArray = new TransformAccessArray(capacity);
            // _colliderInfoList = new ExNativeArray<ColliderInfo>(capacity);
            // _colliderList = new List<DynamicBoneColliderBase>(capacity);
            // isValid = true;
        }

        public bool IsValid()
        {
            return isValid;
        }
    }
}