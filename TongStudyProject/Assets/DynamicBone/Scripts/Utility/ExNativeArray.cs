using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
namespace DynamicBone.Scripts.Utility
{
    /// <summary>
    /// 可扩展的NativeArray类
    /// 当区域不足时，它会自动扩展
    /// 数据由ChankData管理，包括起始索引和长度
    /// 数据可以删除，删除的区域会被管理并重复使用
    /// 请注意，由于需要管理区域，所以与ExSimpleNativeArray相比，这个类稍重
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ExNativeArray<T> : IDisposable where T : unmanaged
    {
        NativeArray<T> nativeArray;

        List<DataChunk> emptyChunks = new List<DataChunk>();

        int useCount;

        public void Dispose()
        {
            if (nativeArray.IsCreated)
            {
                nativeArray.Dispose();
            }
            emptyChunks.Clear();
            useCount = 0;
        }

        public bool IsValid => nativeArray.IsCreated;

        /// <summary>
        /// NativeArray的区域大小
        /// 注意，这与实际使用的大小不同！
        /// </summary>
        public int Length => nativeArray.IsCreated ? nativeArray.Length : 0;

        /// <summary>
        /// 实际使用的数据数量（最后一个块的末尾+1）
        /// </summary>
        public int Count => useCount;

        //=========================================================================================
        public ExNativeArray()
        {
        }

        public ExNativeArray(int emptyLength, bool create = false) : this()
        {
            if (emptyLength > 0)
            {
                nativeArray = new NativeArray<T>(emptyLength, Allocator.Persistent);
                var chunk = new DataChunk(0, emptyLength);
                emptyChunks.Add(chunk);

                if (create)
                {
                    // 保留区域
                    AddRange(emptyLength);
                }
            }
            else if (create)
            {
                // 仅以0创建Native数组（主要用于处理作业中的错误）
                nativeArray = new NativeArray<T>(0, Allocator.Persistent);
            }
        }

        public ExNativeArray(int emptyLength, T fillData) : this(emptyLength)
        {
            if (emptyLength > 0)
                Fill(fillData);
        }

        public ExNativeArray(NativeArray<T> dataArray) : this()
        {
            AddRange(dataArray);
        }

        public ExNativeArray(T[] dataArray) : this()
        {
            AddRange(dataArray);
        }

        //=========================================================================================
#if false
        /// <summary>
        /// 设置使用数组计数
        /// 重写有效数量，将所有数据作为一个块使用
        /// 这是一个非常强大的功能，所以要小心使用！
        /// </summary>
        /// <param name="count"></param>
        public void SetUseCount(int count)
        {
            useCount = count;
            emptyChunks.Clear();
            if (useCount > Length)
            {
                // 将未使用的区域注册为一个空块
                var chunk = new DataChunk(useCount, Length - useCount);
                emptyChunks.Add(chunk);
            }
        }
#endif
        ///<summary>
        /// 添加指定大小的区域，并返回该区块
        /// </summary>
        ///<param name="dataLength"></param>
        ///<returns></returns>
        public DataChunk AddRange(int dataLength)
        {
            // 支持大小为0的区域
            if (dataLength == 0)
            {
                // 仅为区域分配0
                if (nativeArray.IsCreated == false)
                    nativeArray = new NativeArray<T>(0, Allocator.Persistent);

                return DataChunk.Empty;
            }

            var chunk = GetEmptyChunk(dataLength);

            if (chunk.IsValid == false)
            {
                // 增加空闲空间
                int nowLength = Length;
                int nextLength = Length + math.max(dataLength, nowLength);
                if (nowLength == 0)
                {
                    // 新建
                    if (nativeArray.IsCreated)
                        nativeArray.Dispose();
                    nativeArray = new NativeArray<T>(nextLength, Allocator.Persistent);
                    chunk.m_DataLength = dataLength;
                }
                else
                {
                    // 扩展
                    var newNativeArray = new NativeArray<T>(nextLength, Allocator.Persistent);

                    // 复制
                    NativeArray<T>.Copy(nativeArray, newNativeArray, nowLength);
                    nativeArray.Dispose();
                    nativeArray = newNativeArray;

                    // 数据区块
                    chunk.m_StartIndex = nowLength;
                    chunk.m_DataLength = dataLength;

                    int last = nowLength + dataLength;
                    if (last< nextLength)
                    {
                        var emptyChunk = new DataChunk(last, nextLength - last);
                        AddEmptyChunk(emptyChunk);
                    }
                }
            }

            // 使用量
            useCount = math.max(useCount, chunk.m_StartIndex + chunk.m_DataLength);

            return chunk;
        }

        public DataChunk AddRange(int dataLength, T fillData = default(T))
        {
            var chunk = AddRange(dataLength);
            Fill(chunk, fillData);
            return chunk;
        }

        public DataChunk AddRange(T[] array)
        {
            if (array == null || array.Length == 0)
                return DataChunk.Empty;

            int dataLength = array.Length;
            var chunk = AddRange(dataLength);

            // copy
            NativeArray<T>.Copy(array, 0, nativeArray, chunk.m_StartIndex, dataLength);

            return chunk;
        }

        public DataChunk AddRange(NativeArray<T> narray, int length = 0)
        {
            if (narray.IsCreated == false || narray.Length == 0)
                return DataChunk.Empty;

            int dataLength = length > 0 ? length : narray.Length;
            var chunk = AddRange(dataLength);
            // copy
            NativeArray<T>.Copy(narray, 0, nativeArray, chunk.m_StartIndex, dataLength);

            return chunk;
        }

        public DataChunk AddRange(ExNativeArray<T> exarray)
        {
            return AddRange(exarray.GetNativeArray(), exarray.Count);
        }

        // public DataChunk AddRange(ExSimpleNativeArray<T> exarray)
        // {
        //     return AddRange(exarray.GetNativeArray(), exarray.Count);
        // }

        ///<summary>
        /// 添加具有相同大小但不同类型的数组。例如，Vector3 -> float3 等。
        /// </summary>
        /// <typeparam name="U"></typeparam>
        ///<param name="array"></param>
        ///<returns></returns>
        public unsafe DataChunk AddRange<U>(U[] array) where U : struct
        {
            if (array == null || array.Length == 0)
                return DataChunk.Empty;

            int dstSize = UnsafeUtility.SizeOf<T>();
            int dataLength = array.Length;
            var chunk = AddRange(dataLength);

            ulong src_gcHandle;
            void* src_p = UnsafeUtility.PinGCArrayAndGetDataAddress(array, out src_gcHandle);
            byte* dst_p = (byte*)nativeArray.GetUnsafePtr();

            UnsafeUtility.MemCpy(dst_p + chunk.m_StartIndex * dstSize, src_p, dataLength * dstSize);
            UnsafeUtility.ReleaseGCObject(src_gcHandle);

            return chunk;
        }

        ///<summary>
        /// 添加具有相同大小但不同类型的 NativeArray。例如，Vector3 -> float3 等。
        /// </summary>
        /// <typeparam name="U"></typeparam>
        ///<param name="udata"></param>
        ///<returns></returns>
        public DataChunk AddRange<U>(NativeArray<U> udata) where U : struct
        {
            if (udata.IsCreated == false || udata.Length == 0)
                return DataChunk.Empty;

            int dataLength = udata.Length;
            var chunk = AddRange(dataLength);

            // copy
            NativeArray<T>.Copy(udata.Reinterpret<T>(), 0, nativeArray, chunk.m_StartIndex, dataLength);

            return chunk;
        }

        ///<summary>
        /// 添加具有不同类型和大小的数组。例如，int[] -> int3[] 等。
        /// 数据会直接内存复制。例如，从 int[] 添加到 int3[]，结果如下：
        /// int[]{1, 2, 3, 4, 5, 6} => int3[]{{1, 2, 3}, {4, 5, 6}}
        /// </summary>
        /// <typeparam name="U"></typeparam>
        ///<param name="array"></param>
        ///<returns></returns>
        public unsafe DataChunk AddRangeTypeChange<U>(U[] array) where U : struct
        {
            if (array == null || array.Length == 0)
                return DataChunk.Empty;

            int srcSize = UnsafeUtility.SizeOf<U>();
            int dstSize = UnsafeUtility.SizeOf<T>();

            int dataLength = (array.Length * srcSize) / dstSize;
            var chunk = AddRange(dataLength);

            ulong src_gcHandle;
            void* src_p = UnsafeUtility.PinGCArrayAndGetDataAddress(array, out src_gcHandle);
            byte* dst_p = (byte*)nativeArray.GetUnsafePtr();

            UnsafeUtility.MemCpy(dst_p + chunk.m_StartIndex * dstSize, src_p, dataLength * dstSize);
            UnsafeUtility.ReleaseGCObject(src_gcHandle);

            return chunk;
        }

        ///<summary>
        /// 部分复制不同类型的数组。例如：Vector4[] -> float3 等。
        /// </summary>
        ///<typeparam name="U"></typeparam>
        ///<param name="array"></param>
        ///<returns></returns>
        public unsafe DataChunk AddRangeStride<U>(U[] array) where U : struct
        {
            if (array == null || array.Length == 0)
                return DataChunk.Empty;

            int srcSize = UnsafeUtility.SizeOf<U>();
            int dstSize = UnsafeUtility.SizeOf<T>();
            int dataLength = array.Length;
            var chunk = AddRange(dataLength);

            ulong src_gcHandle;
            void* src_p = UnsafeUtility.PinGCArrayAndGetDataAddress(array, out src_gcHandle);
            byte* dst_p = (byte*)nativeArray.GetUnsafePtr();
            int elementSize = math.min(srcSize, dstSize);

            UnsafeUtility.MemCpyStride(dst_p + chunk.m_StartIndex * dstSize, dstSize, src_p, srcSize, elementSize, dataLength);
            UnsafeUtility.ReleaseGCObject(src_gcHandle);

            return chunk;
        }

        public DataChunk Add(T data)
        {
            var chunk = AddRange(1);
            nativeArray[chunk.m_StartIndex] = data;
            return chunk;
        }

        ///<summary>
        /// 扩展指定块的数据长度并返回新的块
        /// 旧块的数据将被复制到新的块中
        /// </summary>
        ///<param name="c"></param>
        ///<param name="newDataLength"></param>
        ///<returns></returns>
        public DataChunk Expand(DataChunk c, int newDataLength)
        {
            if (!c.IsValid)
                return c;
            if (newDataLength <= c.m_DataLength)
                return c;

            // 分配新的内存区域
            var nc = AddRange(newDataLength);

            // 复制旧区域的数据
            NativeArray<T>.Copy(nativeArray, c.m_StartIndex, nativeArray, nc.m_StartIndex, c.m_DataLength);

            // 释放旧区域
            Remove(c);

            return nc;
        }

        ///<summary>
        /// 扩展指定块的数据长度并返回新的块
        /// 旧块的数据将被复制到新的块中
        /// </summary>
        ///<param name="c"></param>
        ///<param name="newDataLength"></param>
        ///<returns></returns>
        public DataChunk ExpandAndFill(DataChunk c, int newDataLength, T fillData = default(T), T clearData = default(T))
        {
            if (!c.IsValid)
                return c;
            if (newDataLength <= c.m_DataLength)
                return c;

            // 分配新的内存区域
            var nc = AddRange(newDataLength, fillData);

            // 复制旧区域的数据
            NativeArray<T>.Copy(nativeArray, c.m_StartIndex, nativeArray, nc.m_StartIndex, c.m_DataLength);

            // 释放旧区域并填充
            RemoveAndFill(c, clearData);

            return nc;
        }

        public T[] ToArray()
        {
            return nativeArray.ToArray();
        }

        public void CopyTo(T[] array)
        {
            NativeArray<T>.Copy(nativeArray, array);
        }

        public void CopyTo<U>(U[] array) where U : struct
        {
            NativeArray<U>.Copy(nativeArray.Reinterpret<U>(), array);
        }

        public void CopyFrom(NativeArray<T> array)
        {
            NativeArray<T>.Copy(array, nativeArray);
        }

        public void CopyFrom<U>(NativeArray<U> array) where U : struct
        {
            NativeArray<T>.Copy(array.Reinterpret<T>(), nativeArray);
        }

        ///<summary>
        /// 将数据复制到不同类型和大小的数组中。
        /// 例如：int3 -> int[] 等。
        /// </summary>
        ///<typeparam name="U"></typeparam>
        ///<param name="array"></param>
        public unsafe void CopyTypeChange<U>(U[] array) where U : struct
        {
            int srcSize = UnsafeUtility.SizeOf<T>();
            int dstSize = UnsafeUtility.SizeOf<U>();
            int dataLength = (Length * srcSize) / dstSize;

            byte* src_p = (byte*)nativeArray.GetUnsafePtr();
            ulong dst_gcHandle;
            void* dst_p = UnsafeUtility.PinGCArrayAndGetDataAddress(array, out dst_gcHandle);

            UnsafeUtility.MemCpy(dst_p, src_p, dataLength * dstSize);
            UnsafeUtility.ReleaseGCObject(dst_gcHandle);
        }

        ///<summary>
        /// 将数据分段复制到不同类型和大小的数组中。
        /// 例如：float3 -> Vector4[] 等。这种情况下，只有 Vector4 的 xyz 部分会被写入。
        /// </summary>
        ///<typeparam name="U"></typeparam>
        ///<param name="array"></param>
        public unsafe void CopyTypeChangeStride<U>(U[] array) where U : struct
        {
            int srcSize = UnsafeUtility.SizeOf<T>();
            int dstSize = UnsafeUtility.SizeOf<U>();
            int dataLength = Length;

            byte* src_p = (byte*)nativeArray.GetUnsafePtr();
            ulong dst_gcHandle;
            void* dst_p = UnsafeUtility.PinGCArrayAndGetDataAddress(array, out dst_gcHandle);

            int elementSize = srcSize;

            UnsafeUtility.MemCpyStride(dst_p, dstSize, src_p, srcSize, elementSize, dataLength);
            UnsafeUtility.ReleaseGCObject(dst_gcHandle);
        }

        ///<summary>
        /// 仅添加可立即使用的空闲区域。
        /// </summary>
        ///<param name="dataLength"></param>
        public void AddEmpty(int dataLength)
        {
            var chunk = AddRange(dataLength);
            Remove(chunk);
        }

        public void Remove(DataChunk chunk)
        {
            if (chunk.IsValid == false)
                return;

            AddEmptyChunk(chunk);

            // 重新计算使用量
            if ((chunk.m_StartIndex + chunk.m_DataLength) == useCount)
            {
                useCount = 0;
                foreach (var echunk in emptyChunks)
                {
                    useCount = math.max(useCount, echunk.m_StartIndex);
                }
            }
        }

        public void RemoveAndFill(DataChunk chunk, T clearData = default(T))
        {
            Remove(chunk);

            // 清除数据
            // C#
            //Parallel.For(0, chunk.dataLength, i =>
            //{
            //    nativeArray[chunk.startIndex + i] = clearData;
            //});
            //FillInternal(chunk.startIndex, chunk.dataLength, clearData);
            Fill(chunk, clearData);
        }

        public void Fill(T fillData = default(T))
        {
            if (IsValid == false)
                return;

            // C#
            //Parallel.For(0, nativeArray.Length, i =>
            //{
            //    nativeArray[i] = fillData;
            //});
            FillInternal(0, nativeArray.Length, fillData);
        }

        public void Fill(DataChunk chunk, T fillData = default(T))
        {
            if (IsValid == false || chunk.IsValid == false)
                return;

            // C#
            //Parallel.For(0, chunk.dataLength, i =>
            //{
            //    nativeArray[chunk.startIndex + i] = fillData;
            //});
            FillInternal(chunk.m_StartIndex, chunk.m_DataLength, fillData);
        }

        unsafe void FillInternal(int start, int size, T fillData = default(T))
        {
            //byte* dst_p = (byte*)nativeArray.GetUnsafePtr();
            void* dst_p = nativeArray.GetUnsafePtr();
            int index = start;
            for (int i = 0; i < size; i++, index++)
            {
                UnsafeUtility.WriteArrayElement<T>(dst_p, index, fillData);
            }
        }


        public void Clear()
        {
            emptyChunks.Clear();
            useCount = 0;

            // empty chunk
            if (IsValid && Length > 0)
            {
                var chunk = new DataChunk(0, Length);
                emptyChunks.Add(chunk);
            }
        }

        public T this[int index]
        {
            get
            {
                return nativeArray[index];
            }
            set
            {
                nativeArray[index] = value;
            }
        }

        public unsafe ref T GetRef(int index)
        {
            T* p = (T*)nativeArray.GetUnsafePtr();
            return ref *(p + index);
        }

        //public unsafe ref T GetRef(int index)
        //{
        //    var span = new Span<T>(nativeArray.GetUnsafePtr(), nativeArray.Length);
        //    return ref span[index];
        //}

        ///<summary>
        /// 如果在Job中使用，请使用此函数将其转换为NativeArray并传递
        /// </summary>
        ///<returns></returns>
        public NativeArray<T> GetNativeArray()
        {
            return nativeArray;
        }

        ///<summary>
        /// 如果在Job中使用，请使用此函数将其转换为NativeArray并传递（带类型转换）
        /// </summary>
        /// <typeparam name="U"></typeparam>
        ///<returns></returns>
        public NativeArray<U> GetNativeArray<U>() where U : struct
        {
            return nativeArray.Reinterpret<U>();
        }

        //=========================================================================================
        DataChunk GetEmptyChunk(int dataLength)
        {
            if (dataLength <= 0)
                return new DataChunk();

            for (int i = 0; i < emptyChunks.Count; i++)
            {
                var c = emptyChunks[i];
                if (dataLength == c.m_DataLength)
                {
                    //如果有相同大小的DataChunk就直接利用
                    emptyChunks.RemoveAtSwapBack(i);
                    return c;
                }
                else if (dataLength < c.m_DataLength)
                {
                    //如果有大的DataChunk就分割出來用一部分
                    var chunk = new DataChunk();
                    chunk.m_StartIndex = c.m_StartIndex;
                    chunk.m_DataLength = dataLength;
                    c.m_StartIndex += dataLength;
                    c.m_DataLength -= dataLength;
                    emptyChunks[i] = c;
                    return chunk;
                }
            }

            // 如果大于就new个新的
            return new DataChunk();
        }

        void AddEmptyChunk(DataChunk chunk)
        {
            if (chunk.IsValid == false)
                return;

            // 寻找可以连接到后面的位置
            for (int i = 0; i< emptyChunks.Count; i++)
            {
                var c = emptyChunks[i];
                if ((c.m_StartIndex + c.m_DataLength) == chunk.m_StartIndex)
                {
                    // 在这里连接
                    c.m_DataLength += chunk.m_DataLength;
                    chunk = c;

                    // 删除c
                    emptyChunks.RemoveAtSwapBack(i);
                    break;
                }
            }

            // 寻找可以连接到前面的位置
            for (int i = 0; i< emptyChunks.Count; i++)
            {
                var c = emptyChunks[i];
                if (c.m_StartIndex == (chunk.m_StartIndex + chunk.m_DataLength))
                {
                    // 在这里连接
                    chunk.m_DataLength += c.m_DataLength;

                    // 删除c
                    emptyChunks.RemoveAtSwapBack(i);
                    break;
                }
            }

            // 添加chunk
            emptyChunks.Add(chunk);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"ExNativeArray Length:{Length} Count:{Count} IsValid:{IsValid}");
            sb.AppendLine("---- Datas[100] ----");
            if (IsValid)
            {
                for (int i = 0; i < Length && i < 100; i++)
                {
                    sb.AppendLine(nativeArray[i].ToString());
                }
            }

            sb.AppendLine("---- Empty Chunks ----");
            foreach (var c in emptyChunks)
            {
                sb.AppendLine(c.ToString());
            }
            sb.AppendLine();

            return sb.ToString();
        }

        public string ToSummary()
        {
            return $"ExNativeArray Length:{Length} Count:{Count} IsValid:{IsValid}";
        }
    }
}