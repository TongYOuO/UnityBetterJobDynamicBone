using System.Collections.Generic;
using DynamicBone.Scripts.Collider;
using DynamicBone.Scripts.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
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

        public enum Direction
        {
            X, Y, Z
        }
        
        public enum ColliderType
        {
            //归属于Collider子类的部分
            OutsideSphere = 0,
            InsideSphere = 1,
            OutsideCapsule = 2,
            InsideCapsule = 3,
            OutsideCapsule2 = 4,
            InsideCapsule2 = 5,
            //属于PlaneCollider子类
            PlaneCollider = 6,
        }
        
        public struct ColliderInfo
        {
            //存储每个Collider对应的哪个Team,只对指定Team中的骨骼生效
            public int m_TeamIndex;
            public int m_Index;
            
            public ColliderType ColliderType;
            public Plane m_Plane;
            
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
        
        //存储每个生效的Collider的信息
        public ExNativeArray<ColliderInfo> ColliderInfoNativeArrayList;
        
        [BurstCompile]
        private struct ColliderSetupJob : IJobParallelForTransform
        {
            public NativeArray<ColliderInfo> m_ColliderArray;
        
            public void Execute(int index, TransformAccess transform)
            {
                ColliderInfo colliderInfo = m_ColliderArray[index];
                
                //归属于PlaneCollider的这个子类
                if(colliderInfo.ColliderType == ColliderType.PlaneCollider)
                {
                    Vector3 normal = Vector3.up;
                    switch (colliderInfo.m_Direction)
                    {
                        case DynamicBoneColliderBase.Direction.X:
                            normal = transform.rotation * Vector3.right;
                            break;
                        case DynamicBoneColliderBase.Direction.Y:
                            normal = transform.rotation * Vector3.up;
                            break;
                        case DynamicBoneColliderBase.Direction.Z:
                            normal = transform.rotation * Vector3.forward;
                            break;
                    }

                    // Vector3 p = transform.TransformPoint(colliderInfo.m_Center);
                    // colliderInfo.m_Plane.SetNormalAndPosition(normal, p);
                }
                //归属于Collider的这个子类
                else
                {
                    
                }
                m_ColliderArray[index] = colliderInfo;
            }
        }
        
        // //TODO:在算完一部分约束时进行Collide处理
        // public void Collide()
        // {
        //     
        // }
        
        /// <summary>
        /// 将每个Team关联的Collider添加到NativeArray的结构中,在每个Mono刚挂上的时候进行添加
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