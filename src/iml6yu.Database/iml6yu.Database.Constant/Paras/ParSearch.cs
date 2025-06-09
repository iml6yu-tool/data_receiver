using SqlSugar;

namespace iml6yu.Database.Constant.Paras
{
    public class ParSearch
    {
        public int PageIndex { get; set; }

        public int PageSize { get; set; }

        public List<ConditionalModel> Conditionals { get; set; }

        public Dictionary<string, string> OrderByArray { get; set; }
    }
}
