using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataService.Modbus.Configs
{
    public class DataServiceStoreDefaultDataItem<TValue> where TValue : notnull
    {
        /// <summary>
        /// 地址
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// 默认值
        /// </summary>
        public required TValue DefaultValue { get; set; }
    }

    public class DataServiceStoreDefaultObjectItem : DataServiceStoreDefaultDataItem<object>
    {

    }
    public class DataServiceStoreDefaultBoolItem : DataServiceStoreDefaultDataItem<bool>
    {

    }
    public class DataServiceStoreDefaultUShortItem : DataServiceStoreDefaultDataItem<ushort>
    {

    }
}
