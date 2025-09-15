using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.Database.Constant.Entities
{
    public abstract class BasicEntity : IEntity
    {
        protected BasicEntity()
        {
            Id = iml6yu.Fingerprint.GetId();
        }

        /// <summary>
        /// Key
        /// </summary>
        [SugarColumn(IsPrimaryKey = true)]
        public long Id { get; set; } 
        public DateTime CreateTime { get; set; } = DateTime.Now;
        [SugarColumn(IsNullable = true)] 
        public long? CreatorId { get; set; } = 0;
        public bool Deleted { get; set; }
        [SugarColumn(IsNullable = true)]
        public long? TenantId { get; set; }
        [SugarColumn(IsNullable = true)]
        public DateTime? UpdateTime { get; set; }
    }
}
