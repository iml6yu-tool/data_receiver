using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataService.Modbus.Configs
{
    public class DataServiceStoreDefaultDataItem<TValue> where TValue : notnull
    {
        public string Address { get; set; }

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
