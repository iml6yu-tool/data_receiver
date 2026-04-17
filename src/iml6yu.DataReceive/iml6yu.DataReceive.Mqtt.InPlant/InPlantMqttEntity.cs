using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataReceive.Mqtt.InPlant
{
    internal class InPlantMqttEntity
    {
        public List<InPlantMqttItem> RTValue { get; set; }
    }
    internal class InPlantMqttItem
    {
        public string Name { get; set; }

        public object Value { get; set; }

        public long Timestamp { get; set; }
    }
}
