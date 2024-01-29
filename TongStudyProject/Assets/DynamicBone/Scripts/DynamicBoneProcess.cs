using DynamicBone.Scripts.Utility;
using Unity.Mathematics;
using UnityEngine;

namespace DynamicBone.Scripts
{
    public class DynamicBoneProcess
    {
        public DynamicBone cloth { get; internal set; }

        public int TeamId { get; private set; } = 0;

        public DataChunk teamDataChunk;
        public DataChunk particleTreeDataChunk;
        public DataChunk particleChunk;
        /// <summary>
        /// 每个挂载的DynamicBone脚本都会在Start中自动执行初始化
        /// </summary>
        private void BuildSync()
        {
            //每个mono脚本的特有数据,TeamID作为每个Team数据段的开始下标
            //一个挂载的脚本管理多个ParticleTree,共享同一个TeamDada的配置,而每个ParticleTree因为具有不同的Transform也需要独特的ParticleTreeInfo,同时每一个Particle骨骼也需要自己的ParticleInfo
            teamDataChunk = DynamicBoneManager.Team.AddTeam(this);
            TeamId = teamDataChunk.m_StartIndex;
            var dcPair = DynamicBoneManager.Simulation.SetupParticleTrees(this);
            particleTreeDataChunk = dcPair.Item1;
            particleChunk = dcPair.Item2;
        }
        
        public void DataUpdate()
        {
            return;            
        }

        public void Init()
        {
            return;    
        }

        public void EndUse()
        {
            return;    
            //TODO:运行时卸载
            //DynamicBoneManager.Team.SetEnable(TeamId, false);
        }

        public void StartUse()
        {
            return;    
            //DynamicBoneManager.Team.SetEnable(TeamId, true);
        }

        public void AutoBuild()
        {
            
            //Start的时候Build一次
            BuildSync();
            return;    
        }

        public void Dispose()
        {
            DynamicBoneManager.Team.RemoveTeam(teamDataChunk);
            DynamicBoneManager.Simulation.RemoveParticleTrees(particleTreeDataChunk);
            DynamicBoneManager.Simulation.RemoveParticles(particleChunk);
        }
    }
}