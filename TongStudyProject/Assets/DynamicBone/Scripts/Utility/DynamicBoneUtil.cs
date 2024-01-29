using UnityEngine;
using UnityEngine.Jobs;

namespace DynamicBone.Scripts.Utility
{
    public class DynamicBoneUtil
    {
        public static void AddTransformAccessToArray(TransformAccessArray array, int targetIndex,Transform transform)
        {
            int nowcnt = array.length;
            
            while (nowcnt < targetIndex)
            {
                array.Add(null);
                nowcnt++;
            }
            
            if (targetIndex < nowcnt)
                array[targetIndex] = transform;
            else
                array.Add(transform);
            
            // for (int i = 0; i < cnt; i++)
            // {
            //     var t = tdata.transformList[i];
            //     int index = c.startIndex + i;
            //     if (index < nowcnt)
            //         transformAccessArray[index] = t;
            //     else
            //         transformAccessArray.Add(t);
            // }
        }
    }
}