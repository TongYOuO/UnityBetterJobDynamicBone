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
    /// <summary>
    /// 每个挂载的Mono脚本的管理类
    /// </summary>
    public class DynamicBoneTeamManager: IDynamicBoneManager, IDynamicBoneValid
    {
        public ExNativeArray<TeamData> m_TeamDataArray;
        
        public TransformAccessArray m_TeamDataTransformAccessArray;
        
        public bool isValid;
        
        //每个挂载的Mono脚本所具有的所有属性
        public struct TeamData
        {
            //TODO:支持UpdateMode的修改
            public bool IsFixedUpdate => m_UpdateMode == DynamicBone.UpdateMode.AnimatePhysics;
            
            public bool IsUnscaled => m_UpdateMode == DynamicBone.UpdateMode.UnscaledTime;

            public DynamicBone.UpdateMode m_UpdateMode;
            
            public float m_ObjectScale;
            
            public float m_Weight;
            
            public float3 m_Force;
 
            public float3 m_Gravity;
            
            public float3 m_ObjectMove;
            
            public float3 m_ObjectPrevPosition;

            //每个Team关联的所有的碰撞器
            //TODO:用ptr减少重复的存取
            public DataChunk m_ColliderInfoChunk;
            
            public DynamicBone.FreezeAxis m_FreezeAxis;

        }
        
        public ref TeamData GetTeamDataRef(int teamId)
        {
            return ref m_TeamDataArray.GetRef(teamId);
        }
        
        internal DataChunk AddTeam(DynamicBoneProcess cprocess)
        {
            var transform = cprocess.cloth.transform;

            var team = new TeamData()
            {
                m_ObjectScale = Mathf.Abs(transform.lossyScale.x),
                m_ObjectPrevPosition = transform.position,
                m_ObjectMove = Vector3.zero,
                //权重暂时先直接读取吧,如果有需要再支持运行时Debug
                m_Weight = cprocess.cloth.SerializeData.m_BlendWeight,
                m_Gravity = cprocess.cloth.SerializeData.m_Gravity,
                m_FreezeAxis = cprocess.cloth.SerializeData.m_FreezeAxis,
                m_Force = cprocess.cloth.SerializeData.m_Force,
                m_UpdateMode = cprocess.cloth.SerializeData.m_UpdateMode,
            };
            
            var dataChunk = m_TeamDataArray.Add(team);
            DynamicBoneUtil.AddTransformAccessToArray(m_TeamDataTransformAccessArray,dataChunk.m_StartIndex, transform);
            // teamList.Add(teamID);
            return dataChunk;
        }

        // internal void RemoveTeam(int teamId)
        // {
        //     m_TeamDataArray.Remove(teamId);
        // }
        [BurstCompile]
        struct PrepareJob : IJobParallelForTransform
        {
            public NativeArray<DynamicBoneTeamManager.TeamData> m_TeamDataArray;
            public void Execute(int index, TransformAccess transform)
            {
             
                Matrix4x4 matrix = transform.localToWorldMatrix;
                Vector3 lossyScale = new Vector3(matrix.GetColumn(0).magnitude, matrix.GetColumn(1).magnitude, matrix.GetColumn(2).magnitude);
                var teamData = m_TeamDataArray[index];
                teamData.m_ObjectScale = Mathf.Abs(lossyScale.x);
            
                var position = transform.position;
                teamData.m_ObjectMove = (float3)position - teamData.m_ObjectPrevPosition;
                teamData.m_ObjectPrevPosition = position;
                
                m_TeamDataArray[index] = teamData;
            }
        }

        public JobHandle Prepare(JobHandle jobHandle)
        {
            var job = new PrepareJob()
            {
                m_TeamDataArray = m_TeamDataArray.GetNativeArray(),
            };
            jobHandle = job.ScheduleReadOnly(m_TeamDataTransformAccessArray, 4, jobHandle);
            return jobHandle;
        }

        public JobHandle ResetObjectMove(JobHandle jobHandle)
        {
            var job = new ResetObjectMoveJob()
            {
                m_TeamDataArray = m_TeamDataArray.GetNativeArray(),
            };
            jobHandle = job.Schedule(m_TeamDataArray.Count, 4, jobHandle);
            return jobHandle;
        }
        
        [BurstCompile]
        struct ResetObjectMoveJob : IJobParallelFor
        {
            public NativeArray<DynamicBoneTeamManager.TeamData> m_TeamDataArray;
            public void Execute(int index)
            {
                var teamData = m_TeamDataArray[index];
                teamData.m_ObjectMove = Vector3.zero;
                m_TeamDataArray[index] = teamData;
            }
        }
        
        public void RemoveTeam(DataChunk dataChunk)
        {
            m_TeamDataArray.RemoveAndFill(dataChunk);
        }
        
        public void Dispose()
        {
            m_TeamDataArray?.Dispose();
            m_TeamDataTransformAccessArray.Dispose();
            
            isValid = false;
        }

        public void Initialize()
        {
            const int capacity = 32;
            m_TeamDataArray = new ExNativeArray<TeamData>(capacity);
            m_TeamDataTransformAccessArray = new TransformAccessArray(capacity);
            isValid = true;
        }

        public bool IsValid()
        {
            return isValid;
        }
    }
}