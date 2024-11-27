using System;

namespace iml6yu.Data.Core
{
    public class WarnArgs : EventArgs
    {  
        /// <summary>
        /// 报警码
        /// </summary>
        public string? Code { get; set; }
        
        /// <summary>
        /// 报警描述 默认中文
        /// </summary>

        public string? Message { get; set; }
       
    }
}
