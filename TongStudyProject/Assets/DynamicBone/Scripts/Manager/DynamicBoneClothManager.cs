using Unity.Jobs;
using UnityEngine;

namespace DynamicBone.Scripts
{
    public class DynamicBoneClothManager: IDynamicBoneManager, IDynamicBoneValid
    {

        float m_Time = 0;
        private JobHandle _masterJob = default;
        bool isValid = false;
        void OnBeforeLateUpdate()
        {
            if (DynamicBoneManager.Time.m_UpdateLocation == DynamicBoneTimeManager.UpdateLocation.BeforeLateUpdate)
                ClothUpdate();
        }
        
        void OnAfterLateUpdate()
        {
            if (DynamicBoneManager.Time.m_UpdateLocation == DynamicBoneTimeManager.UpdateLocation.AfterLateUpdate)
                ClothUpdate();
            CompleteMasterJob();
        }
        
        void OnAfterFixedUpdate()
        {
            if (DynamicBoneManager.m_UpdateMode == DynamicBone.UpdateMode.AnimatePhysics)
            {
                ClearMasterJob();
                var simulationManager = DynamicBoneManager.Simulation;
                _masterJob = simulationManager.PreUpdate(_masterJob);
                CompleteMasterJob();
            }
        }
        
        void OnAfterUpdate()
        {
            if (DynamicBoneManager.m_UpdateMode != DynamicBone.UpdateMode.AnimatePhysics)
            {
                ClearMasterJob();
                var simulationManager = DynamicBoneManager.Simulation;
                _masterJob = simulationManager.PreUpdate(_masterJob);
                CompleteMasterJob();
            }
        }
        
        void ClearMasterJob()
        {
            _masterJob = default;
        }

        void CompleteMasterJob()
        {
            _masterJob.Complete();
        }
        
        void ClothUpdate()
        {
            if(Application.isPlaying == false)
                return;
            var teamManager = DynamicBoneManager.Team;
            var simulationManager = DynamicBoneManager.Simulation;
            var colliderManager = DynamicBoneManager.Collider;
            // if (tm.ActiveTeamCount == 0)
            // {
            //     return;
            // }
            ClearMasterJob();
            
            //TODO:支持运行时修改参数,暂不支持
            // masterJob = tm.SetRunTimeTeamsWeight(masterJob);
            
            //TODO:每个Team进行距离禁用+BlendWeight禁用,暂不支持
            
            _masterJob = teamManager.Prepare(_masterJob);
            _masterJob = simulationManager.PrepareParticleTree(_masterJob);
            var prepareParticles = simulationManager.PrepareParticles();
            
            //TODO:支持Collider功能
            // var prepareColliders = colliderManager.PrepareColliders();
            _masterJob = JobHandle.CombineDependencies(_masterJob, prepareParticles);//,prepareColliders

            UpdateParticles();
            _masterJob = simulationManager.ApplyParticlesToTransforms(_masterJob);
            
            // CompleteMasterJob();
            // _masterJob = simulationManager.PrepareApplyParticlesToTransforms(_masterJob);
            // _masterJob = simulationManager.ApplyParticlesToTransforms(_masterJob);

        }

        public void UpdateParticles()
        {
            var simulationManager = DynamicBoneManager.Simulation;
            float t = DynamicBoneManager.m_UpdateMode == DynamicBone.UpdateMode.UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            int loop = 1;
            if (DynamicBoneManager.m_UpdateRate > 0)
            {
                float dt = 1.0f / DynamicBoneManager.m_UpdateRate;
                m_Time += t;
                loop = 0;

                while (m_Time >= dt)
                {
                    m_Time -= dt;
                    if (++loop >= 3)
                    {
                        m_Time = 0;
                        break;
                    }
                }
            }
            
            if (loop > 0)
            {
                for (int i = 0; i < loop; ++i)
                {
                    //约束计算
                    _masterJob = simulationManager.UpdateParticles1(_masterJob);
                    _masterJob = simulationManager.UpdateParticles2(_masterJob);
                    _masterJob = DynamicBoneManager.Team.ResetObjectMove(_masterJob);
                }
            }
            else
            {
                _masterJob = simulationManager.SkipUpdateParticles(_masterJob);
            }
        }
        
        public void Dispose()
        {
            DynamicBoneManager.afterUpdateDelegate -= OnAfterUpdate;
            DynamicBoneManager.afterLateUpdateDelegate -= OnAfterLateUpdate;
            DynamicBoneManager.beforeLateUpdateDelegate -= OnBeforeLateUpdate;
            DynamicBoneManager.afterFixedUpdateDelegate -= OnAfterFixedUpdate;
            DynamicBoneManager.afterRenderingDelegate -= OnAfterRender;
            DynamicBoneManager.afterEarlyUpdateDelegate -= OnEarlyUpdate;
        }

        private void OnAfterRender()
        {

        }

        public void Initialize()
        {
            DynamicBoneManager.afterUpdateDelegate += OnAfterUpdate;
            DynamicBoneManager.afterLateUpdateDelegate += OnAfterLateUpdate;
            DynamicBoneManager.beforeLateUpdateDelegate += OnBeforeLateUpdate;
            DynamicBoneManager.afterFixedUpdateDelegate += OnAfterFixedUpdate;
            DynamicBoneManager.afterRenderingDelegate += OnAfterRender;
            DynamicBoneManager.afterEarlyUpdateDelegate += OnEarlyUpdate;
            isValid = true;
        }

        private void OnEarlyUpdate()
        {

        }

        public bool IsValid()
        {
            return isValid;
        }
    }
}