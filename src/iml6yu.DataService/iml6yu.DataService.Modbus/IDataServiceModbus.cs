using iml6yu.Data.Core.Models;
using iml6yu.DataService.Core;
using iml6yu.DataService.Modbus.Configs;
using iml6yu.Result;
using Microsoft.VisualBasic.FileIO;
using NModbus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.DataService.Modbus
{
    public interface IDataServiceModbus:IDataService
    {
        IModbusSlaveNetwork? Network { get; set; }
    }
}
