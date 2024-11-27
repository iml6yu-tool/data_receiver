using System;

namespace iml6yu.Data.Core
{
    public class ExceptionArgs : EventArgs
    {  
        /// <summary>
        /// 错误码
        /// </summary>
        public string? Code { get; set; }
        /// <summary>
        /// 异常信息
        /// </summary>
        public Exception? Ex { get; set; }
        /// <summary>
        /// 异常描述 默认中文
        /// </summary>

        public string? Message { get; set; } 
    }
}
