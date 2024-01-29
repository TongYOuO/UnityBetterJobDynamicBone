using System.Text;
using UnityEngine;

namespace DynamicBone.Scripts
{
    public class DynamicBoneTimeManager : IDynamicBoneManager, IDynamicBoneValid
    {
        bool _isValid = false;
        
        internal int FixedUpdateCount { get; private set; }
        
        float _deltaTime;
        
        public enum UpdateLocation
        {
            AfterLateUpdate = 0,
            BeforeLateUpdate = 1,
        }
        internal UpdateLocation m_UpdateLocation = UpdateLocation.AfterLateUpdate;
        
        public bool IsValid()
        {
            return _isValid;
        }
        
        void AfterFixedUpdate()
        {
            FixedUpdateCount++;
        }
        
        void AfterRenderring()
        {
            FixedUpdateCount = 0;
        }
        
        public void Initialize()
        {
            FixedUpdateCount = 0;
            // GlobalTimeScale = 1.0f;
            // SimulationPower = 1.0f;

            DynamicBoneManager.afterFixedUpdateDelegate += AfterFixedUpdate;

            _isValid = true;
        }
        
        public void Dispose()
        {
            _isValid = true;

            FixedUpdateCount = 0;
            // GlobalTimeScale = 1.0f;
            // SimulationPower = 1.0f;
            
            DynamicBoneManager.afterFixedUpdateDelegate -= AfterFixedUpdate;
        }
        
        // public void InformationLog(StringBuilder allsb)
        // {
        //     StringBuilder sb = new StringBuilder();
        //
        //     sb.AppendLine($"========== Time Manager ==========");
        //     if (IsValid() == false)
        //     {
        //         sb.AppendLine($"Time Manager. Invalid");
        //     }
        //     else
        //     {
        //         // sb.AppendLine($"SimulationFrequency:{simulationFrequency}");
        //         // sb.AppendLine($"MaxSimulationCountPerFrame:{maxSimulationCountPerFrame}");
        //         // sb.AppendLine($"GlobalTimeScale:{GlobalTimeScale}");
        //         // sb.AppendLine($"SimulationDeltaTime:{SimulationDeltaTime}");
        //         // sb.AppendLine($"MaxDeltaTime:{MaxDeltaTime}");
        //         // sb.AppendLine($"SimulationPower:{SimulationPower}");
        //     }
        //     sb.AppendLine();
        //
        //     Debug.Log(sb.ToString());
        //     allsb.Append(sb);
        // }
    }
}