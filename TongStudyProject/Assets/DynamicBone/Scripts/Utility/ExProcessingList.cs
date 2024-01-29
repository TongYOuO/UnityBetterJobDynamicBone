using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace DynamicBone.Scripts.Utility
{
    public unsafe class ExProcessingList<T> : IDisposable, IDynamicBoneValid where T : struct
    {
        /// <summary>
        /// バッファの現在のデータ数をカウントするためのカウンター
        /// </summary>
        public NativeReference<int> Counter;

        /// <summary>
        /// データバッファ
        /// </summary>
        public NativeArray<T> Buffer;

        //=========================================================================================
        public void Dispose()
        {
            if (Counter.IsCreated)
                Counter.Dispose();
            if (Buffer.IsCreated)
                Buffer.Dispose();
        }

        public bool IsValid()
        {
            return Counter.IsCreated;
        }

        //=========================================================================================
        public ExProcessingList()
        {
            Counter = new NativeReference<int>(Allocator.Persistent);
        }

        //=========================================================================================
        /// <summary>
        /// キャパシティが収まるようにバッファを拡張する。
        /// すでに容量が確保できている場合は何もしない。
        /// </summary>
        /// <param name="capacity"></param>
        public void UpdateBuffer(int capacity)
        {
            if (Buffer.IsCreated == false || Buffer.Length < capacity)
            {
                if (Buffer.IsCreated)
                    Buffer.Dispose();
                Buffer = new NativeArray<T>(capacity, Allocator.Persistent);
            }
        }

        /// <summary>
        /// ジョブスケジュール用のカウントintポインターを取得する
        /// </summary>
        /// <param name="counter"></param>
        /// <returns></returns>
        public int* GetJobSchedulePtr()
        {
            return (int*)Counter.GetUnsafePtrWithoutChecks();
        }

        public override string ToString()
        {
            int counter = Counter.IsCreated ? Counter.Value : 0;
            int bufferLength = Buffer.IsCreated ? Buffer.Length : 0;
            return $"ExProcessingList BufferLength:{bufferLength} Counter:{counter}";
        }
    }
}