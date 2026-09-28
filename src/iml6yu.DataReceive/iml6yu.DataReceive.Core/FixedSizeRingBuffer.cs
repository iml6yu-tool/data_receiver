namespace iml6yu.DataReceive.Core
{
    /// <summary>
    /// 固定最多5个元素，FIFO，超过自动淘汰最旧；支持从新到旧遍历对比；线程安全
    /// </summary>
    public class FixedSizeRingBuffer<T>
    {
        private readonly T[] buffer;
        private int count;      // 当前有效元素数量
        private int writePos;   // 下一个写入位置
        private readonly object lockObj = new();

        public int MaxCapacity { get; }

        public FixedSizeRingBuffer(int maxCapacity)
        {
            MaxCapacity = maxCapacity;
            buffer = new T[maxCapacity];
        }

        /// <summary>
        /// 添加新值，满了自动移除最旧
        /// </summary>
        public void Add(T value)
        {
            lock (lockObj)
            {
                buffer[writePos] = value;
                if (count < MaxCapacity)
                    count++;
                writePos = (writePos + 1) % MaxCapacity;
            }
        }

        /// <summary>
        /// 从【最新 → 旧】依次对比
        /// 只要遇到不同返回true，diffIndex=从最新开始的下标（0=最新）
        /// 全部相同返回false，diffIndex=-1
        /// </summary>
        public bool HasAnyDifferent(T target, out int diffIndex)
        {
            lock (lockObj)
            {
                diffIndex = -1;
                if (count == 0)
                    return false;

                // 定位最新元素下标
                int idx = writePos - 1;
                if (idx < 0) idx = MaxCapacity - 1;

                for (int i = 0; i < count; i++)
                {
                    if (!Equals(buffer[idx], target))
                    {
                        diffIndex = i; // i就是从新开始的位置：0最新，1次新...
                        return true;
                    }
                    idx--;
                    if (idx < 0) idx = MaxCapacity - 1;
                }
                return false;
            }
        }

        /// <summary>
        /// 获取当前元素数量（可选）
        /// </summary>
        public int Count
        {
            get
            {
                lock (lockObj) return count;
            }
        }

        /// <summary>
        /// 清空缓存
        /// </summary>
        public void Clear()
        {
            lock (lockObj)
            {
                count = 0;
                writePos = 0;
                Array.Clear(buffer, 0, buffer.Length);
            }
        }
    } 
}
