using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iml6yu.Database.Constant.Entities
{
    public interface IEntity
    {
        /// <summary>
        /// Key
        /// </summary>
        [SugarColumn(IsPrimaryKey = true)]
        long Id { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        DateTime CreateTime { get; set; }
        DateTime? UpdateTime { get; set; }

        /// <summary>
        /// 创建人Id
        /// </summary>
        long? CreatorId { get; set; }

        /// <summary>
        /// 否已删除
        /// </summary>
        bool Deleted { get; set; }

        /// <summary>
        /// 租户ID
        /// </summary>
        long? TenantId { get; set; }
    }
}
