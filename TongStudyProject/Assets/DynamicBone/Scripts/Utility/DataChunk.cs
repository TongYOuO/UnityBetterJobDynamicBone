namespace DynamicBone.Scripts.Utility
{
    public struct DataChunk
    {
        public int m_StartIndex;
        
        public int m_DataLength;

        public bool IsValid => m_DataLength > 0;

        public static DataChunk Empty
        {
            get
            {
                return new DataChunk();
            }
        }

        public DataChunk(int sindex, int length)
        {
            m_StartIndex = sindex;
            m_DataLength = length;
        }

        public DataChunk(int sindex)
        {
            m_StartIndex = sindex;
            m_DataLength = 1;
        }

        public void Clear()
        {
            m_StartIndex = 0;
            m_DataLength = 0;
        }

        public override string ToString()
        {
            return $"[startIndex={m_StartIndex}, dataLength={m_DataLength}]";
        }
    }
}