using System.Collections.Generic;
using DynamicBone.Scripts.SerializeData;
using DynamicBone.Scripts.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace DynamicBone.Scripts
{
    public class DynamicBoneSimulationManager: IDynamicBoneManager, IDynamicBoneValid
    {
        //每个骨骼树特有属性
        public struct ParticleTreeInfo
        {
            public float3 m_LocalGravity;
            public float3 m_ForceAfterGravity;
            public int m_TeamIndex;
            public bool m_IsValid;
            public int GlobalParticleStartIndex;
            public int ParticleCnt;
        }
        
        //每个骨骼特有的属性
        public struct ParticleInfo
        {
            public int m_Index;
            public int m_GlobalParentIndex;
            public int m_LocalParentIndex;
            public float m_Damping ;
            public float m_Elasticity;
            public float m_Stiffness ;
            public float m_Inert;
            public float m_Friction ;
            public float m_Radius;
            public float m_BoneLength ;
            public bool m_IsCollide ;
            
            public int m_ChildCount;
            public bool m_TransformNotNull;
            
            public float3 m_PrevPosition;
            public float3 m_EndOffset;
            public float3 m_InitLocalPosition;
            public Quaternion m_InitLocalRotation ;
            
            public int m_TeamIndex;
            public int m_ParticleTreeIndex;
        }
        
        bool _isValid = false;
        
        //TODO:尽量减少模板的类型,把里面的变量全部展开抽出来,多用一些通用的类型,一方面减少泛型模板的生成减少IL2CPP代码量,一方面可以有利于重用DataChunk,还可以有效的防止Job分组越界,不过这样写起来就只能把多个Array拼成一个目标数据结构来用,就比较难读，有空再改了

        public ExNativeArray<ParticleInfo> m_ParticleInfoArray;
        //约束计算用的临时Position
        //先抽这几个出来m_ParticleTransformPosition、m_ParticleTransformLocalToWorldMatrixArray、m_ParticleTransformLocalPositionArray、m_ParticleTransformRotationArray
        public ExNativeArray<float3> m_ParticlePositionArray;
        public ExNativeArray<float3> m_ParticleTransformPositionArray;
        public ExNativeArray<float3> m_ParticleTransformLocalPositionArray;
        public ExNativeArray<Quaternion> m_ParticleTransformRotationArray;
        public ExNativeArray<Matrix4x4> m_ParticleTransformLocalToWorldMatrixArray;
        public TransformAccessArray m_ParticleTransformAccessArray;
        
        public ExNativeArray<ParticleTreeInfo> m_ParticleTreeDataArray;
        public TransformAccessArray m_ParticleTreeTransformAccessArray;

        //临时使用的变量
        private bool _addNewParticleTree;
        private bool _addNewTeam;
        private int _curParticleTreeParticleStartIndex;
        private int _curTeamParticleStartIndex;

        public void Initialize()
        {
            const int capacity = 8; 
            m_ParticleTreeDataArray = new ExNativeArray<ParticleTreeInfo>(capacity);
            m_ParticleInfoArray = new ExNativeArray<ParticleInfo>(capacity);
            m_ParticleTransformAccessArray = new TransformAccessArray(capacity);
            m_ParticleTreeTransformAccessArray = new TransformAccessArray(capacity);
            m_ParticlePositionArray = new ExNativeArray<float3>(capacity);
            m_ParticleTransformPositionArray = new ExNativeArray<float3>(capacity);
            m_ParticleTransformLocalPositionArray = new ExNativeArray<float3>(capacity);
            m_ParticleTransformRotationArray = new ExNativeArray<Quaternion>(capacity);
            m_ParticleTransformLocalToWorldMatrixArray = new ExNativeArray<Matrix4x4>(capacity);
        }

        public void Dispose()
        {
            m_ParticleTreeDataArray?.Dispose();
            m_ParticleInfoArray?.Dispose();
            m_ParticleTransformAccessArray.Dispose();
            m_ParticleTreeTransformAccessArray.Dispose();
            m_ParticlePositionArray?.Dispose();
            m_ParticleTransformPositionArray?.Dispose();
            m_ParticleTransformLocalPositionArray?.Dispose();
            m_ParticleTransformRotationArray?.Dispose();
            m_ParticleTransformLocalToWorldMatrixArray?.Dispose();
            
        }
        
        public bool IsValid()
        {
            return _isValid;
        }

        public JobHandle PrepareParticleTree(JobHandle jobHandle)
        {
            var job = new PrepareParticleTreeJob()
            {
                m_ParticleTreeArray = m_ParticleTreeDataArray.GetNativeArray(),
                m_TeamDataArray = DynamicBoneManager.Team.m_TeamDataArray.GetNativeArray(),
            };
            jobHandle = job.ScheduleReadOnly(m_ParticleTreeTransformAccessArray,4, jobHandle);
            return jobHandle;
        }
        [BurstCompile]
        struct PrepareParticleTreeJob : IJobParallelForTransform
        {
            public NativeArray<ParticleTreeInfo> m_ParticleTreeArray;
            [ReadOnly]
            public NativeArray<DynamicBoneTeamManager.TeamData> m_TeamDataArray;
            public void Execute(int index, TransformAccess transform)
            {
                if(m_ParticleTreeArray.Length <= index || m_ParticleTreeArray[index].m_IsValid == false)
                    return;
                //重力预计算,重力受每个树根的初始方向影响
                //TODO:这里看起来只用计算一次就够了,后面试试能不能优化掉
                ParticleTreeInfo particleTreeInfo = m_ParticleTreeArray[index];
                float3 localGravity = m_ParticleTreeArray[index].m_LocalGravity;
                float3 worldGravity = transform.localToWorldMatrix.MultiplyVector(localGravity);
                var teamData = m_TeamDataArray[particleTreeInfo.m_TeamIndex];
                float3 force = teamData.m_Gravity;
                //这里要特判一下0的情况,Vector3和float3的normalize对0的处理不一样直接用会有NaN的问题
                float3 fdir;
                if (math.length(force) != 0) 
                {
                    fdir = math.normalize(force);
                } 
                else 
                {
                    fdir = force; 
                }
                float3 pf = fdir * Mathf.Max(math.dot(worldGravity,fdir), 0);	// project current gravity to rest gravity
                force -= pf;	// remove projected gravity
                force = (force + teamData.m_Force) * (teamData.m_ObjectScale);//TODO:* timeVar
                
                particleTreeInfo.m_ForceAfterGravity = force;
                m_ParticleTreeArray[index] = particleTreeInfo;
            }
        }

        public JobHandle PrepareParticles()
        {
            var job = new PrepareParticleJob()
            {
                m_ParticleInfos = m_ParticleInfoArray.GetNativeArray(),
                m_ParticleTransformPositionArray = m_ParticleTransformPositionArray.GetNativeArray(),
                m_ParticleTransformRotationArray = m_ParticleTransformRotationArray.GetNativeArray(),
                m_ParticleTransformLocalPositionArray = m_ParticleTransformLocalPositionArray.GetNativeArray(),
                m_ParticleTransformLocalToWorldMatrixArray = m_ParticleTransformLocalToWorldMatrixArray.GetNativeArray(),
            };
            var jobHandle = job.ScheduleReadOnly(m_ParticleTransformAccessArray,8);
            return jobHandle;
        }
        
        [BurstCompile]
        struct PrepareParticleJob : IJobParallelForTransform
        {
            public NativeArray<ParticleInfo> m_ParticleInfos;
            [WriteOnly]
            public NativeArray<float3> m_ParticleTransformPositionArray;
            [WriteOnly]
            public NativeArray<float3> m_ParticleTransformLocalPositionArray;
            [WriteOnly]
            public NativeArray<Quaternion> m_ParticleTransformRotationArray;
            [WriteOnly]
            public NativeArray<Matrix4x4> m_ParticleTransformLocalToWorldMatrixArray;
            public void Execute(int index, TransformAccess transform)
            {
                ParticleInfo p = m_ParticleInfos[index];
                
                if (p.m_TransformNotNull)
                {
                    m_ParticleTransformPositionArray[index] = transform.position;
                    m_ParticleTransformRotationArray[index] = transform.rotation;
                    m_ParticleTransformLocalPositionArray[index] = transform.localPosition;
                    m_ParticleTransformLocalToWorldMatrixArray[index] = transform.localToWorldMatrix;
                }
                
                m_ParticleInfos[index] = p;
            }
        }

        public JobHandle UpdateParticles1(JobHandle jobHandle)
        {
            var job = new UpdateParticles1Job()
            {
                m_TeamDatas = DynamicBoneManager.Team.m_TeamDataArray.GetNativeArray(),
                m_ParticleTreeInfos = m_ParticleTreeDataArray.GetNativeArray(),
                m_ParticleInfos = m_ParticleInfoArray.GetNativeArray(),
                m_ParticlePositionArray = m_ParticlePositionArray.GetNativeArray(),
                m_ParticleTransformPositionArray = m_ParticleTransformPositionArray.GetNativeArray(),
            };
            jobHandle = job.Schedule(m_ParticleInfoArray.Count, 8, jobHandle);
            return jobHandle;
        }
        
        /// <summary>
        /// 全Particle并行,而不仅仅是ParticleTree并行
        /// </summary>
        [BurstCompile]
        private struct UpdateParticles1Job : IJobParallelFor
        {
            public NativeArray<ParticleInfo> m_ParticleInfos;
            public NativeArray<float3> m_ParticlePositionArray;
            [ReadOnly]
            public NativeArray<DynamicBoneTeamManager.TeamData> m_TeamDatas;
            [ReadOnly]
            public NativeArray<ParticleTreeInfo> m_ParticleTreeInfos;
            [ReadOnly]
            public NativeArray<float3> m_ParticleTransformPositionArray;
            public void Execute(int index)
            {
                var p = m_ParticleInfos[index];
                var m_Position = m_ParticlePositionArray[index];
                var m_TransformPosition = m_ParticleTransformPositionArray[index];
                var teamData = m_TeamDatas[p.m_TeamIndex];
                var paticleTreeInfo = m_ParticleTreeInfos[p.m_ParticleTreeIndex];
                if (p.m_LocalParentIndex >= 0)
                {
                    // verlet integration
                    float3 v = m_Position - p.m_PrevPosition;
                    float3 rmove = teamData.m_ObjectMove * p.m_Inert;
                    p.m_PrevPosition = m_Position + rmove;
                    float damping = p.m_Damping;
                    if (p.m_IsCollide)
                    {
                        damping += p.m_Friction;
                        if (damping > 1)
                        {
                            damping = 1;
                        }
                        p.m_IsCollide = false;
                    }
                    // v - dv  
                    m_Position += v * (1 - damping) + rmove + paticleTreeInfo.m_ForceAfterGravity;
                }
                else
                {
                    p.m_PrevPosition = m_Position;
                    // if (m_RootReferenceObject)
                    //     p.m_Position = m_RootReferenceObject.position;
                    // else
                    m_Position = m_TransformPosition;
                }

                m_ParticlePositionArray[index] = m_Position;
                m_ParticleInfos[index] = p;
            }
        }

        public JobHandle UpdateParticles2(JobHandle jobHandle)
        {
            var job = new UpdateParticles2Job()
            {
                m_TeamDatas = DynamicBoneManager.Team.m_TeamDataArray.GetNativeArray(),
                m_ParticleInfos = m_ParticleInfoArray.GetNativeArray(),
                m_ParticlePositionArray = m_ParticlePositionArray.GetNativeArray(),
                m_ParticleTransformLocalPositionArray = m_ParticleTransformLocalPositionArray.GetNativeArray(),
                m_ParticleTransformPositions = m_ParticleTransformPositionArray.GetNativeArray(),
                m_ParticleTransformLocalToWorldMatrixArray = m_ParticleTransformLocalToWorldMatrixArray.GetNativeArray(),
                m_ParticleTreeInfos = m_ParticleTreeDataArray.GetNativeArray(),
            };
            //粒度为每个ParticleTree
            jobHandle = job.Schedule(m_ParticleTreeDataArray.Count, 1, jobHandle);
            return jobHandle;
        }
        
        public JobHandle SkipUpdateParticles(JobHandle jobHandle)
        {
            var job = new SkipUpdateParticlesJob()
            {
                m_TeamDatas = DynamicBoneManager.Team.m_TeamDataArray.GetNativeArray(),
                m_ParticleInfos = m_ParticleInfoArray.GetNativeArray(),
                m_ParticlePositionArray = m_ParticlePositionArray.GetNativeArray(),
                m_ParticleTransformLocalPositionArray = m_ParticleTransformLocalPositionArray.GetNativeArray(),
                m_ParticleTransformPositions = m_ParticleTransformPositionArray.GetNativeArray(),
                m_ParticleTransformLocalToWorldMatrixArray = m_ParticleTransformLocalToWorldMatrixArray.GetNativeArray(),
                m_ParticleTreeInfos = m_ParticleTreeDataArray.GetNativeArray(),
            };
            //粒度为每个ParticleTree
            jobHandle = job.Schedule(m_ParticleTreeDataArray.Count, 1, jobHandle);
            return jobHandle;
        }
        [BurstCompile]
        private struct SkipUpdateParticlesJob : IJobParallelFor
        {
            //这里拆两个数组解决对同一个数据即读又写的功能,可能导致一个骨骼有多个子骨骼的时候表现异常,暂时先这么处理
            [ReadOnly] public NativeArray<ParticleInfo> m_ParticleInfos;
            [NativeDisableParallelForRestriction] public NativeArray<float3> m_ParticlePositionArray;
            [ReadOnly] public NativeArray<ParticleTreeInfo> m_ParticleTreeInfos;
            [ReadOnly] public NativeArray<float3> m_ParticleTransformLocalPositionArray;
            [ReadOnly] public NativeArray<DynamicBoneTeamManager.TeamData> m_TeamDatas;
            [ReadOnly] public NativeArray<float3> m_ParticleTransformPositions;

            [ReadOnly] public NativeArray<Matrix4x4> m_ParticleTransformLocalToWorldMatrixArray;

            //粒度为每个ParticleTree
            public void Execute(int ParticleTreeIndex)
            {
                for (int index = m_ParticleTreeInfos[ParticleTreeIndex].GlobalParticleStartIndex;
                     index < m_ParticleTreeInfos[ParticleTreeIndex].ParticleCnt +
                     m_ParticleTreeInfos[ParticleTreeIndex].GlobalParticleStartIndex;
                     index++)
                {
                    var p = m_ParticleInfos[index];
                    var m_Position = m_ParticlePositionArray[index];
                    var teamData = m_TeamDatas[p.m_TeamIndex];
                    var m_TransformPosition = m_ParticleTransformPositions[index];
                    var m_TransformLocalPosition = m_ParticleTransformLocalPositionArray[index];
                    if (p.m_LocalParentIndex >= 0)
                    {
                        p.m_PrevPosition += teamData.m_ObjectMove;
                        m_Position += teamData.m_ObjectMove;

                        var parentTransformPositon = m_ParticleTransformPositions[p.m_GlobalParentIndex];
                        var parentPosition = m_ParticlePositionArray[p.m_GlobalParentIndex];
                        var parentTransformLocalToWorldMatrix = m_ParticleTransformLocalToWorldMatrixArray[p.m_GlobalParentIndex];
                        
                        float restLen;
                        if (p.m_TransformNotNull)
                        {
                            restLen = math.length(parentTransformPositon - m_TransformPosition);
                        }
                        else
                        {
                            restLen = parentTransformLocalToWorldMatrix.MultiplyVector(p.m_EndOffset).magnitude;
                        }

                        // keep shape
                        float stiffness = Mathf.Lerp(1.0f, p.m_Stiffness, teamData.m_Weight);
                        if (stiffness > 0)
                        {
                            Matrix4x4 m0 = parentTransformLocalToWorldMatrix;
                            m0.SetColumn(3, new Vector4(parentPosition.x, parentPosition.y, parentPosition.z, 1));
                            float3 restPos;
                            if (p.m_TransformNotNull)
                            {
                                restPos = m0.MultiplyPoint3x4(m_TransformLocalPosition);
                            }
                            else
                            {
                                restPos = m0.MultiplyPoint3x4(p.m_EndOffset);
                            }
                            float3 d = restPos - m_Position;
                            float len = math.length(d);
                            float maxlen = restLen * (1 - stiffness) * 2;
                            if (len > maxlen)
                            {
                                m_Position += d * ((len - maxlen) / len);
                            }
                        }

                        // keep length
                        float3 dd = parentPosition - m_Position;
                        float leng = math.length(dd);
                        if (leng > 0)
                        {
                            m_Position += dd * ((leng - restLen) / leng);
                        }
                    }
                    else
                    {
                        p.m_PrevPosition = m_Position;
                        m_Position = m_TransformPosition;
                    }
                    m_ParticlePositionArray[index] = m_Position;
                }
            }
        }
        [BurstCompile]
        private struct UpdateParticles2Job : IJobParallelFor
        {
            //这里拆两个数组解决对同一个数据即读又写的功能,可能导致一个骨骼有多个子骨骼的时候表现异常,暂时先这么处理
            [ReadOnly]
            public NativeArray<ParticleInfo> m_ParticleInfos;
            [NativeDisableParallelForRestriction]
            public NativeArray<float3> m_ParticlePositionArray;
            [ReadOnly]
            public NativeArray<ParticleTreeInfo> m_ParticleTreeInfos;
            [ReadOnly]
            public NativeArray<float3> m_ParticleTransformLocalPositionArray;
            [ReadOnly]
            public NativeArray<DynamicBoneTeamManager.TeamData> m_TeamDatas;
            [ReadOnly]
            public NativeArray<float3> m_ParticleTransformPositions;
            [ReadOnly]
            public NativeArray<Matrix4x4> m_ParticleTransformLocalToWorldMatrixArray;
            //粒度为每个ParticleTree
            public void Execute(int ParticleTreeIndex)
            {
                for (int index = m_ParticleTreeInfos[ParticleTreeIndex].GlobalParticleStartIndex + 1;
                     index < m_ParticleTreeInfos[ParticleTreeIndex].ParticleCnt + m_ParticleTreeInfos[ParticleTreeIndex].GlobalParticleStartIndex;
                     index++)
                {
                    var transformPosition = m_ParticleTransformPositions[index];
                    
                    var p = m_ParticleInfos[index];
                    var parentTransformPosition = m_ParticleTransformPositions[p.m_GlobalParentIndex];
                    var parentPosition = m_ParticlePositionArray[p.m_GlobalParentIndex];
                    var position = m_ParticlePositionArray[index];
                    var teamData = m_TeamDatas[p.m_TeamIndex];
                    float restLen;
                    if (p.m_TransformNotNull)
                    {
                        restLen = math.distance(parentTransformPosition, transformPosition);
                    }
                    else
                    {
                        restLen = m_ParticleTransformLocalToWorldMatrixArray[p.m_GlobalParentIndex]
                            .MultiplyVector(p.m_EndOffset).magnitude;
                    }

                    // keep shape刚度约束
                    float stiffness = Mathf.Lerp(1.0f, p.m_Stiffness, teamData.m_Weight);
                    if (stiffness > 0 || p.m_Elasticity > 0)
                    {
                        Matrix4x4 m0 = m_ParticleTransformLocalToWorldMatrixArray[p.m_GlobalParentIndex];
                        m0.SetColumn(3, new Vector3(parentPosition.x, parentPosition.y, parentPosition.z));
                        float3 restPos;
                        if (p.m_TransformNotNull)
                        {
                            restPos = m0.MultiplyPoint3x4(m_ParticleTransformLocalPositionArray[index]);
                        }
                        else
                        {
                            restPos = m0.MultiplyPoint3x4(p.m_EndOffset);
                        }

                        float3 d = restPos - position;
                        position += d * (p.m_Elasticity); // * timeVar
                   

                        if (stiffness > 0)
                        {
                            //超出刚度计算的最大位置则拉回到刚度的边界
                            d = restPos - position;
                            float len = math.length(d);
                            float maxlen = restLen * (1 - stiffness) * 2;
                            if (len > maxlen)
                            {
                                position += d * ((len - maxlen) / len);
                            }
                        }
                    }

                    // //碰撞约束
                    // // collide
                    // if (m_EffectiveColliders != null)
                    // {
                    //     float particleRadius = p.m_Radius * m_ObjectScale;
                    //     for (int j = 0; j < m_EffectiveColliders.Count; ++j)
                    //     {
                    //         DynamicBoneColliderBase c = m_EffectiveColliders[j];
                    //         p.m_isCollide |= c.Collide(ref p.m_Position, particleRadius);
                    //     }
                    // }

                    //FreezeAxis约束
                    // if (teamData.m_FreezeAxis != DynamicBone.FreezeAxis.None)
                    // {
                    //     Vector3 planeNormal = p0.m_TransformLocalToWorldMatrix.GetColumn((int)teamData.m_FreezeAxis - 1).normalized;
                    //     movePlane.SetNormalAndPosition(planeNormal, p0.m_Position);
                    //     p.m_Position -= movePlane.normal * movePlane.GetDistanceToPoint(p.m_Position);
                    // }

                    //保持骨骼之间的距离为原本的距离
                    float3 dd = parentPosition - position;
                    float leng = math.length(dd);
                    if (leng > 0)
                    {
                        position += dd * ((leng - restLen) / leng);
                    }

                    m_ParticlePositionArray[index] = position;
                }

            }
        }
        
        // // [BurstCompile]
        // private struct PrepareApplyParticlesToTransformsJob : IJobParallelFor
        // {
        //     public NativeArray<Quaternion> m_ParticleTransformRotationArray;
        //     [ReadOnly]
        //     public NativeArray<ParticleInfo> m_ParticleInfoArray;
        //     [ReadOnly]
        //     public NativeArray<float3> m_ParticlePositionArray;
        //     [WriteOnly]
        //     public NativeArray<float3> m_ParticleTransformPositionArray;
        //     [ReadOnly]
        //     public NativeArray<float3> m_ParticleTransformLocalPositionArray;
        //     [ReadOnly]
        //     public NativeArray<Matrix4x4> m_ParticleTransformLocalToWorldMatrixArray;
        //     [ReadOnly]
        //     public NativeArray<ParticleTreeInfo> m_ParticleTreeInfos;
        //     public void Execute(int particleTreeIndex)
        //     {
        //         for (int index = m_ParticleTreeInfos[particleTreeIndex].GlobalParticleStartIndex + 1; index < m_ParticleTreeInfos[particleTreeIndex].GlobalParticleStartIndex + m_ParticleTreeInfos[particleTreeIndex].ParticleCnt; index++)
        //         {
        //             //跳过根节点
        //             
        //             var p = m_ParticleInfoArray[index];
        //             var p0 = m_ParticleInfoArray[p.m_GlobalParentIndex];
        //             var particlePosition = m_ParticlePositionArray[index];
        //             if (p0.m_ChildCount <= 1)		// do not modify bone orientation if has more then one child
        //             {
        //                 float3 localPos;
        //                 if (p.m_TransformNotNull)
        //                 {
        //                     localPos = m_ParticleTransformLocalPositionArray[index];
        //                 }
        //                 else
        //                 {
        //                     localPos = p.m_EndOffset;
        //                 }
        //             
        //                 float3 v0 = m_ParticleTransformLocalToWorldMatrixArray[p.m_GlobalParentIndex].MultiplyVector(localPos);
        //                 float3 v1 = particlePosition - m_ParticlePositionArray[p.m_GlobalParentIndex];
        //                 Quaternion rot = Quaternion.FromToRotation(v0, v1);
        //                 m_ParticleTransformRotationArray[p.m_GlobalParentIndex] = rot * m_ParticleTransformRotationArray[p.m_GlobalParentIndex];
        //             }
        //
        //             if (p.m_TransformNotNull)
        //             {
        //                 m_ParticleTransformPositionArray[index] = particlePosition;
        //             }
        //         }
        //     }
        // }
        public JobHandle ApplyParticlesToTransforms(JobHandle mainJob)
        {
            var job = new ApplyParticlesToTransformsJob()
            {
                m_ParticleInfoArray = m_ParticleInfoArray.GetNativeArray(),
                m_ParticlePositionArray = m_ParticlePositionArray.GetNativeArray(),
                m_ParticleTransformLocalToWorldMatrixArray = m_ParticleTransformLocalToWorldMatrixArray.GetNativeArray(),
            };
            mainJob = job.Schedule(m_ParticleTransformAccessArray, mainJob);
            return mainJob;
            
        }
        
        [BurstCompile]
        private struct ApplyParticlesToTransformsJob : IJobParallelForTransform
        {
            [ReadOnly]
            public NativeArray<ParticleInfo> m_ParticleInfoArray;
            [ReadOnly]
            public NativeArray<float3> m_ParticlePositionArray;
            [ReadOnly]
            public NativeArray<Matrix4x4> m_ParticleTransformLocalToWorldMatrixArray;
            public void Execute(int index, TransformAccess transform)
            {
                //跳过根节点
                if (m_ParticleInfoArray[index].m_LocalParentIndex == -1)
                    return;
            
                var p = m_ParticleInfoArray[index];
                var p0 = m_ParticleInfoArray[p.m_GlobalParentIndex];

                if (p0.m_ChildCount <= 1)		// do not modify bone orientation if has more then one child
                {
                    float3 localPos;
                    if (p.m_TransformNotNull)
                    {
                        localPos = transform.localPosition;
                    }
                    else
                    {
                        localPos = p.m_EndOffset;
                    }
                
                    float3 v0 = m_ParticleTransformLocalToWorldMatrixArray[p.m_GlobalParentIndex].MultiplyVector(localPos);
                    float3 v1 = m_ParticlePositionArray[index] - m_ParticlePositionArray[p.m_GlobalParentIndex];
                    Quaternion rot = Quaternion.FromToRotation(v0, v1);
                    transform.rotation = rot * transform.rotation;
                }

                if (p.m_TransformNotNull)
                {
                    transform.position = m_ParticlePositionArray[index];
                }
             
            }
            
        }
        
        // public JobHandle PrepareApplyParticlesToTransforms(JobHandle mainJob)
        // {
        //     var job = new PrepareApplyParticlesToTransformsJob()
        //     {
        //         m_ParticleInfoArray = m_ParticleInfoArray.GetNativeArray(),
        //         m_ParticleTransformPositionArray = m_ParticleTransformPositionArray.GetNativeArray(),
        //         m_ParticleTransformLocalToWorldMatrixArray = m_ParticleTransformLocalToWorldMatrixArray.GetNativeArray(),
        //         m_ParticleTransformLocalPositionArray = m_ParticleTransformLocalPositionArray.GetNativeArray(),
        //         m_ParticleTransformRotationArray = m_ParticleTransformRotationArray.GetNativeArray(),
        //         m_ParticlePositionArray = m_ParticlePositionArray.GetNativeArray(),
        //         m_ParticleTreeInfos = m_ParticleTreeDataArray.GetNativeArray(),
        //         
        //     };
        //     mainJob = job.Schedule(m_ParticleTreeDataArray.Count, 1, mainJob);
        //     return mainJob;
        // }
        //
        // public JobHandle ApplyParticlesToTransforms(JobHandle mainJob)
        // {
        //     var job = new ApplyParticlesToTransformsJob()
        //     {
        //         m_ParticleTransformPositionArray = m_ParticleTransformPositionArray.GetNativeArray(),
        //         m_ParticleTransformRotationArray = m_ParticleTransformRotationArray.GetNativeArray(),
        //     };
        //     mainJob = job.Schedule(m_ParticleTransformAccessArray, mainJob);
        //     return mainJob;
        // }
        
        // [BurstCompile]
        // private struct ApplyParticlesToTransformsJob : IJobParallelForTransform
        // {
        //     [ReadOnly]
        //     public NativeArray<float3> m_ParticleTransformPositionArray;
        //     [ReadOnly]
        //     public NativeArray<Quaternion> m_ParticleTransformRotationArray;
        //     public void Execute(int index, TransformAccess transform)
        //     {
        //         transform.position = m_ParticleTransformPositionArray[index];
        //         transform.rotation = m_ParticleTransformRotationArray[index];
        //     }
        //     
        // }

        public JobHandle PreUpdate(JobHandle jobHandle)
        {
            var job = new PreUpdateJob()
            {
                m_ParticleInfoArray = m_ParticleInfoArray.GetNativeArray(),
                m_TeamDataArray = DynamicBoneManager.Team.m_TeamDataArray.GetNativeArray(),
            };
            jobHandle = job.Schedule(m_ParticleTransformAccessArray,jobHandle);
            return jobHandle;
        }
        [BurstCompile]
        struct PreUpdateJob : IJobParallelForTransform
        {
            [ReadOnly]
            public NativeArray<ParticleInfo> m_ParticleInfoArray;
            [ReadOnly]
            public NativeArray<DynamicBoneTeamManager.TeamData> m_TeamDataArray;
            public void Execute(int index, TransformAccess transform)
            {
                var particleInfo = m_ParticleInfoArray[index];
                var teamData = m_TeamDataArray[particleInfo.m_TeamIndex];
                
                //TODO:距离判断超出指定距离时禁用
                if (teamData.m_Weight <= 0)
                {
                    return;
                }
       
                transform.localPosition = particleInfo.m_InitLocalPosition;
                transform.localRotation = particleInfo.m_InitLocalRotation;
            }
        }
        
        [BurstCompile]
        struct NormalPreUpdateJob : IJobParallelForTransform
        {
            [ReadOnly]
            public NativeArray<ParticleInfo> m_ParticleInfoArray;
            [ReadOnly]
            public NativeArray<DynamicBoneTeamManager.TeamData> m_TeamDataArray;
            public void Execute(int index, TransformAccess transform)
            {
                var particleInfo = m_ParticleInfoArray[index];
                var teamData = m_TeamDataArray[particleInfo.m_TeamIndex];
                
                //TODO:距离判断超出指定距离时禁用
                if (teamData.m_Weight <= 0)
                {
                    return;
                }
                
                if(teamData.m_UpdateMode != DynamicBone.UpdateMode.AnimatePhysics)
                {
                    transform.localPosition = particleInfo.m_InitLocalPosition;
                    transform.localRotation = particleInfo.m_InitLocalRotation;
                }
            }
        }
        
        internal (DataChunk,DataChunk) SetupParticleTrees(DynamicBoneProcess cprocess)
        {
            DynamicBoneSerializeData data = cprocess.cloth.SerializeData;
        
            //对roots进行一次去重,直接哈希表O(1)了,原DynamicBone是O(N^2)
            HashSet<Transform> roots = new HashSet<Transform>();
        
            //初始化多个ParticleTree
            //兼容原本的单个Root的模式
            if (data.m_Root != null)
            {
                roots.Add(data.m_Root);
            }
        
            //支持单个DynamicBone下多个Roots
            if (data.m_Roots != null)
            {
                for (int i = 0; i < data.m_Roots.Count; ++i)
                {
                    Transform root = data.m_Roots[i];
                    if (root == null)
                        continue;
                
                    roots.Add(root);
                }
            }
            
            //一次AddRange添加所有,减少多次Add造成的性能消耗
            var chunk = m_ParticleTreeDataArray.AddRange(roots.Count);
            int index = chunk.m_StartIndex;
            //构建所有骨骼树的数据
            _addNewTeam = true;
            _curTeamParticleStartIndex = -1;
            int curTeamCnt = 0;
            foreach (var root in roots)
            {
                AppendParticleTree(root,index,cprocess);
                
                _addNewParticleTree = true;
                //记录递归次数
                int counter = 0;
                float boneTotalLength = 0;
                //TODO:构建骨骼移到Start之后统一Jobs多线程优化
                AppendParticles(cprocess,root,index, -1, 0, ref counter,ref boneTotalLength);
                curTeamCnt += counter;
                //RunTimeRecursiveCallWatcher.OnCheck($"DynamicBone-ParticleTree{i}-AppendParticles", counter);
                var particleTreeData = m_ParticleTreeDataArray[index];
                particleTreeData.GlobalParticleStartIndex = _curParticleTreeParticleStartIndex;
                particleTreeData.ParticleCnt = counter;
                m_ParticleTreeDataArray[index] = particleTreeData;
                UpdateParticleParameters(data,boneTotalLength,counter);
                
                index++;
            }

            DataChunk particleTrunk = new DataChunk(_curTeamParticleStartIndex,curTeamCnt);
            return (chunk,particleTrunk);
        }
        
        /// <summary>
        /// 构建每一棵骨骼树的数据
        /// </summary>
        /// <param name="root"></param>
        /// <param name="teamList"></param>
        void AppendParticleTree(Transform root,int particleTreeIndex,DynamicBoneProcess cprocess)
        {
            if (root == null)
                return;

            var worldToLocalMatrix = root.worldToLocalMatrix;
            var particleTreeInfo = new ParticleTreeInfo()
            {
                m_LocalGravity = worldToLocalMatrix.MultiplyVector(cprocess.cloth.SerializeData.m_Gravity).normalized * cprocess.cloth.SerializeData.m_Gravity.magnitude,
                m_TeamIndex = cprocess.TeamId,
                m_IsValid = true
            }; 
            m_ParticleTreeDataArray[particleTreeIndex] = particleTreeInfo;
            DynamicBoneUtil.AddTransformAccessToArray(m_ParticleTreeTransformAccessArray, particleTreeIndex, root);
        }
        
        void AppendParticles(DynamicBoneProcess cprocess,Transform b,int particleTreeIndex, int parentIndex, float boneLength, ref int counter,ref float boneTotalLength)
        {
            ParticleInfo p = new ParticleInfo()
            {
                m_TransformNotNull = b != null,
                m_TeamIndex = cprocess.TeamId,
                m_LocalParentIndex = parentIndex
            };
         
            ++counter;
            
            float3 m_Position;
            
            //对每个Root下所有需要应用效果的骨骼存储初始姿势下的信息
            if (b != null)
            {
                m_Position = p.m_PrevPosition = b.position;
                p.m_InitLocalPosition = b.localPosition;
                p.m_InitLocalRotation = b.localRotation;
            }
            else 	// end bone的transform为null
            {
                //取得父骨骼的Transform
                Transform pb = m_ParticleTransformAccessArray[parentIndex];
                if (cprocess.cloth.SerializeData.m_EndLength > 0)
                {
                    Transform ppb = pb.parent;
                    //如果parent有parent则取pp指向p的方向进行延长,否则只是延长x
                    if (ppb != null)
                        p.m_EndOffset = pb.InverseTransformPoint((pb.position * 2 - ppb.position)) *cprocess.cloth.SerializeData. m_EndLength;
                    else
                        p.m_EndOffset = new Vector3(cprocess.cloth.SerializeData.m_EndLength, 0, 0);
                }
                else
                {
                    //叠加局部空间下的EndOffset
                    p.m_EndOffset = pb.InverseTransformPoint(cprocess.cloth.transform.TransformDirection(cprocess.cloth.SerializeData.m_EndOffset) + pb.position);
                }
                m_Position = p.m_PrevPosition = pb.TransformPoint(p.m_EndOffset);
                p.m_InitLocalPosition = Vector3.zero;
                p.m_InitLocalRotation = Quaternion.identity;
            }

            //如果有父骨骼则计算骨骼长度
            if (parentIndex >= 0)
            {
                boneLength += math.distance(m_ParticleTransformAccessArray[parentIndex].position, m_Position);
                p.m_BoneLength = boneLength;
                //计算最长的那一根骨骼
                boneTotalLength = Mathf.Max(boneTotalLength, boneLength);
            }
            
            //TODO:先算出骨骼数量,统一添加n个之后再具体改里面的内容,比每次Add一个性能开销更小
            var chunk = m_ParticleInfoArray.Add(p);
            m_ParticleTransformPositionArray.Add(m_Position);
            m_ParticlePositionArray.Add(m_Position);
            m_ParticleTransformLocalPositionArray.Add(p.m_InitLocalPosition);
            m_ParticleTransformRotationArray.Add(b.rotation);
            m_ParticleTransformLocalToWorldMatrixArray.Add(Matrix4x4.identity);
            
            int index = chunk.m_StartIndex;
            p.m_Index = index;
            p.m_ParticleTreeIndex = particleTreeIndex;
            p.m_TeamIndex = cprocess.TeamId;
  
            DynamicBoneUtil.AddTransformAccessToArray(m_ParticleTransformAccessArray, index, b);

            //单独加一个字段用来标记每次新增ParticleTree,方便下面UpdateParameter时定位
            if (_addNewParticleTree)
            {
                _addNewParticleTree = false;
                _curParticleTreeParticleStartIndex = chunk.m_StartIndex;
            }

            if (_addNewTeam)
            {
                _addNewTeam = false;
                _curTeamParticleStartIndex = chunk.m_StartIndex;
            }
            
            p.m_GlobalParentIndex = parentIndex + _curParticleTreeParticleStartIndex;
            if (b != null)
            {
                p.m_ChildCount = b.childCount;
            }
            m_ParticleInfoArray[index] = p;
            
            //DFS
            if (b != null)
            {
                for (int i = 0; i < b.childCount; ++i)
                {
                    Transform child = b.GetChild(i);
                    bool exclude = false;
                    //递归非m_Exclusions黑名单中的骨骼
                    if (cprocess.cloth.SerializeData.m_Exclusions != null)
                    {
                        exclude = cprocess.cloth.SerializeData.m_Exclusions.Contains(child);
                    }
                    if (!exclude)
                    {
                        AppendParticles(cprocess, child, particleTreeIndex,counter - 1, boneLength,ref counter,ref boneTotalLength);
                    }
                    else if (cprocess.cloth.SerializeData.m_EndLength > 0 || cprocess.cloth.SerializeData.m_EndOffset != Vector3.zero)
                    {
                        AppendParticles(cprocess, null, particleTreeIndex,counter - 1, boneLength,ref counter,ref boneTotalLength);
                    }
                }

                if (b.childCount == 0 && (cprocess.cloth.SerializeData.m_EndLength > 0 || cprocess.cloth.SerializeData.m_EndOffset != Vector3.zero))
                {
                    AppendParticles(cprocess, null, particleTreeIndex,counter - 1, boneLength,ref counter,ref boneTotalLength);
                }
            }
        }
        
        void UpdateParticleParameters(DynamicBoneSerializeData data,float boneTotalLength,int curParticleTreeParticleCnt)
        {
            for (int i = _curParticleTreeParticleStartIndex; i < curParticleTreeParticleCnt + _curParticleTreeParticleStartIndex; ++i)
            {
                ParticleInfo p = m_ParticleInfoArray[i];
                p.m_Damping = data.m_Damping;
                p.m_Elasticity = data.m_Elasticity;
                p.m_Stiffness = data.m_Stiffness;
                p.m_Inert = data.m_Inert;
                p.m_Friction = data.m_Friction;
                p.m_Radius = data.m_Radius;

                if (boneTotalLength > 0)
                {
                    float a = p.m_BoneLength / boneTotalLength;
                    if (data.m_DampingDistrib != null && data.m_DampingDistrib.keys.Length > 0)
                        p.m_Damping *= data.m_DampingDistrib.Evaluate(a);
                    if (data.m_ElasticityDistrib != null && data.m_ElasticityDistrib.keys.Length > 0)
                        p.m_Elasticity *= data.m_ElasticityDistrib.Evaluate(a);
                    if (data.m_StiffnessDistrib != null && data.m_StiffnessDistrib.keys.Length > 0)
                        p.m_Stiffness *= data.m_StiffnessDistrib.Evaluate(a);
                    if (data.m_InertDistrib != null && data.m_InertDistrib.keys.Length > 0)
                        p.m_Inert *= data.m_InertDistrib.Evaluate(a);
                    if (data.m_FrictionDistrib != null && data.m_FrictionDistrib.keys.Length > 0)
                        p.m_Friction *= data.m_FrictionDistrib.Evaluate(a);
                    if (data.m_RadiusDistrib != null && data.m_RadiusDistrib.keys.Length > 0)
                        p.m_Radius *= data.m_RadiusDistrib.Evaluate(a);
                }

                p.m_Damping = Mathf.Clamp01(p.m_Damping);
                p.m_Elasticity = Mathf.Clamp01(p.m_Elasticity);
                p.m_Stiffness = Mathf.Clamp01(p.m_Stiffness);
                p.m_Inert = Mathf.Clamp01(p.m_Inert);
                p.m_Friction = Mathf.Clamp01(p.m_Friction);
                p.m_Radius = Mathf.Max(p.m_Radius, 0);

                m_ParticleInfoArray[i] = p;
            }
        }

        public void RemoveParticleTrees(DataChunk particleTreeDataChunk)
        {
            m_ParticleTreeDataArray.RemoveAndFill(particleTreeDataChunk);
            
        }

        public void RemoveParticles(DataChunk particleChunk)
        {
            m_ParticleInfoArray.RemoveAndFill(particleChunk);
            m_ParticleTransformPositionArray.RemoveAndFill(particleChunk);
            m_ParticlePositionArray.RemoveAndFill(particleChunk);
            m_ParticleTransformLocalPositionArray.RemoveAndFill(particleChunk);
            m_ParticleTransformRotationArray.RemoveAndFill(particleChunk);
            m_ParticleTransformLocalToWorldMatrixArray.RemoveAndFill(particleChunk);
        }
    }
}