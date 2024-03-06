using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Jobs;
using Object = UnityEngine.Object;

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
        
        #if UNITY_EDITOR
        /// <summary>
        /// 替换单个DynamicBone的SerializeField到新版的JobifyDynamicBone
        /// </summary>
        public static void ReplaceSingleDynamicBoneSerializeField()
        {
            
        }

        /// <summary>
        /// 基于模板把一个组件转换为另一个组件并且调用的ReplaceComponentCopyData,使用模板是因为要用于Debug时快速切换不同实现的DynamicBone测试性能
        /// </summary>
        /// <param name="targetTransform"></param>
        /// <param name="onBeforeDestroyFrom"></param>之前的组件销毁的回调
        /// <param name="needReplaceData"></param>如果为True的话才会开启数据替换
        /// <param name="useJson"></param>选择以Json还是反射的方式进行数据替换
        /// <typeparam name="TFrom"></typeparam>
        /// <typeparam name="TO"></typeparam>
        public static void ReplaceComponentRecursivelyAndCopyData<TFrom,TO>(Transform targetTransform,Action<TFrom,TO> onBeforeDestroyFrom = null,bool needReplaceData = true ,bool useJson = true) where TO : Component where TFrom : Component
        {
             if (targetTransform.TryGetComponent<TFrom>(out TFrom from))
             {
                 TO to = targetTransform.gameObject.AddComponent<TO>();
                 
                 if (needReplaceData)
                 {
                     if (useJson)
                     {
                         ReplaceComponentCopyDataUseJson(from, to);
                     }
                     else
                     {
                         ReplaceComponentCopyData(from, to);
                     }
                 }
                 
                 onBeforeDestroyFrom?.Invoke(from,to);
                 Object.DestroyImmediate(from,true);
             }

             for (int i = 0; i < targetTransform.childCount; i++)
             {
                 ReplaceComponentRecursivelyAndCopyData<TFrom,TO>(targetTransform.GetChild(i),onBeforeDestroyFrom);
             }

        }

        /// <summary>
        /// 支持递归的删除指定组件
        /// </summary>
        /// <param name="targetTransform"></param>
        /// <typeparam name="TFrom"></typeparam>
        public static void DestroyComponentRecursively<TFrom>(Transform targetTransform) where TFrom : Component
        {
             if (targetTransform.TryGetComponent<TFrom>(out TFrom from))
             {
                 Object.DestroyImmediate(from,true);
             }

             for (int i = 0; i < targetTransform.childCount; i++)
             {
                 DestroyComponentRecursively<TFrom>(targetTransform.GetChild(i));
             }

        }

        public static void ReplaceComponentCopyDataUseJson<TFrom, TO>(TFrom tFrom, TO to)
        {
            var from = JsonUtility.ToJson(tFrom);
            JsonUtility.FromJsonOverwrite(from, to);
        }

        /// <summary>
        /// 通过反射读取全部的序列化字段
        /// </summary>
        /// <param name="tFrom"></param>
        /// <param name="to"></param>
        /// <typeparam name="TFrom"></typeparam>
        /// <typeparam name="TO"></typeparam>
        public static void ReplaceComponentCopyData<TFrom,TO>(TFrom tFrom ,TO to)
        {
             System.Type typeSource = tFrom.GetType();
             System.Type typeTarget = to.GetType();

             // 获取源类和目标类的所有公共和非公共字段
             FieldInfo[] fieldsSource = typeSource.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
             FieldInfo[] fieldsTarget = typeTarget.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

             foreach (var fieldSource in fieldsSource)
             {
                 // 检查字段是否有[Serializefield]属性或者是public
                 if (fieldSource.IsPublic || System.Attribute.IsDefined(fieldSource, typeof(SerializeField)))
                 {
                     foreach (var fieldTarget in fieldsTarget)
                     {
                         // 检查目标类是否有相同名称和类型的字段
                         if (fieldTarget.Name == fieldSource.Name && fieldTarget.FieldType == fieldSource.FieldType)
                         {
                             // 如果有，将源类的字段值复制到目标类
                             fieldTarget.SetValue(to, fieldSource.GetValue(tFrom));
                             break;
                         }
                     }
                 }
             }
        }
        #endif
    }
}