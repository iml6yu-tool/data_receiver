using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace iml6yu.DataService.Modbus.Configs
{
    /// <summary>
    /// 心跳类型
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum HeartType
    {
        Time,
        Number,
        OddAndEven
    }
}
