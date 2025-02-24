using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.Database.Constant.Paras
{
    public class ParSearch
    {
        public int PageIndex { get; set; }

        public int PageSize { get; set; }

        public List<IConditionalModel> Conditionals { get; set; }

        public Dictionary<string, string> OrderByArray { get; set; }
    }
}
