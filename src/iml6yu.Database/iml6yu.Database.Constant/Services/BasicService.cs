using iml6yu.Database.Constant.Entities;
using iml6yu.Database.Constant.Paras;
using iml6yu.Result;
using Microsoft.Extensions.Logging;
using SqlSugar;
using System.Linq.Expressions;

namespace iml6yu.Database.Constant.Services
{
    public abstract class BasicService
    {
        public BasicService(ISqlSugarClient db, ILoggerFactory loggerFactory)
        {
            Db = db;
            Logger = loggerFactory.CreateLogger(this.GetType());
        }

        public ISqlSugarClient Db { get; }

        public ILogger Logger { get; }

        /// <summary>
        /// 初始化数据库
        /// </summary>
        /// <returns></returns>
        public MessageResult InitDb()
        {
            try
            {
                if (Db.CurrentConnectionConfig.DbType == DbType.Sqlite)
                {
                    var fileName = Db.CurrentConnectionConfig.ConnectionString.Replace("Data Source=", "").Trim();
                    if (File.Exists(fileName))
                    {
                        return MessageResult.Success();
                    }
                    var dir = Path.GetDirectoryName(fileName);
                    if (dir != null && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                }
                Db.DbMaintenance.CreateDatabase();
                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError("初始化数据库发生异常！异常信息如下：\r\n{0}", ex.Message);
                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }

        }
        /// <summary>
        /// 初始化表
        /// </summary>
        /// <param name="tables"></param>
        /// <returns></returns>
        public MessageResult InitTables(params Type[] tables)
        {
            try
            {
                Db.CodeFirst.InitTables(tables);
                return MessageResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError("初始化数据表发生异常！异常信息如下：\r\n{0}", ex.Message);
                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
        }
    }

    public abstract class BasicService<TEntity> : BasicService where TEntity : BasicEntity, new()
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="db"></param>
        /// <param name="loggerFactory"></param>
        public BasicService(ISqlSugarClient db, ILoggerFactory loggerFactory) : base(db, loggerFactory)
        {
        }
        /// <summary>
        /// 通过id获取实体
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public virtual async Task<DataResult<TEntity>> GetAsync(long id)
        {
            try
            {
                var result = await FindAsync(id);
                return DataResult<TEntity>.Success(result);
            }
            catch (Exception ex)
            {

                return DataResult<TEntity>.Failed(ResultType.Failed, ex.Message);
            }
        }
        /// <summary>
        /// 通过查询条件获取实体集合
        /// </summary>
        /// <param name="search">查询条件</param>
        /// <returns></returns>
        public virtual async Task<CollectionResult<TEntity>> GetAsync(ParSearch search)
        {
            try
            {
                var result = await FindAsync(search);
                return CollectionResult<TEntity>.Success(search.PageIndex, result.Item2, result.Item1, result.Item2);
            }
            catch (Exception ex)
            {

                return CollectionResult<TEntity>.Failed(ResultType.Failed, ex.Message, ex);
            }

        }
        /// <summary>
        /// 获取所有实体集合
        /// </summary>
        /// <returns></returns>
        public virtual async Task<CollectionResult<TEntity>> GetAsync()
        {
            try
            {
                var datas = await Db.Queryable<TEntity>().Where(t => !t.Deleted).ToListAsync();
                return CollectionResult<TEntity>.Success(1, datas.Count(), datas, datas.Count());
            }
            catch (Exception ex)
            {
                return CollectionResult<TEntity>.Failed(ResultType.Failed, ex.Message, ex);
            }
        }

        /// <summary>
        /// 插入一条数据信息
        /// </summary>
        /// <param name="entity">数据</param>
        /// <returns></returns>
        public virtual async Task<MessageResult> SaveAsync(TEntity entity)
        {
            try
            {
                await Db.Insertable(entity).ExecuteCommandAsync();

                return MessageResult.Success();
            }
            catch (Exception ex)
            {

                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
        }

        /// <summary>
        /// 插入多条数据信息
        /// </summary>
        /// <param name="entities">数据</param>
        /// <returns></returns>
        public virtual async Task<MessageResult> SaveAsync(List<TEntity> entities)
        {
            try
            {
                await Db.Insertable(entities).ExecuteCommandAsync();

                return MessageResult.Success();
            }
            catch (Exception ex)
            {

                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
        }

        /// <summary>
        /// 更新一条数据信息
        /// </summary>
        /// <param name="entity">数据</param>
        /// <returns></returns>
        public virtual async Task<MessageResult> UpdateAsync(TEntity entity)
        {
            try
            {
                await Db.Updateable(entity).ExecuteCommandAsync();

                return MessageResult.Success();
            }
            catch (Exception ex)
            {

                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
        }

        /// <summary>
        /// 批量更新数据信息
        /// </summary>
        /// <param name="entities"></param>
        /// <returns></returns>
        public virtual async Task<MessageResult> UpdateAsync(List<TEntity> entities)
        {
            try
            {
                await Db.Updateable(entities).ExecuteCommandAsync();

                return MessageResult.Success();
            }
            catch (Exception ex)
            {

                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
        }

        /// <summary>
        /// 根据id列表删除数据
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public virtual async Task<MessageResult> DeleteAsync(long[] ids)
        {
            try
            {
                var entity = await FindAsync(ids);
                return await DeleteAsync(entity);
            }
            catch (Exception ex)
            {

                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
        }

        /// <summary>
        /// 根据id删除数据
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public virtual async Task<MessageResult> DeleteAsync(long id)
        {
            try
            {
                var entity = await FindAsync(id);
                return await DeleteAsync(entity);
            }
            catch (Exception ex)
            {

                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
        }

        /// <summary>
        /// 根基实体删除数据
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public virtual async Task<MessageResult> DeleteAsync(TEntity entity)
        {
            try
            {
                await Db.Deleteable(entity).ExecuteCommandAsync();

                return MessageResult.Success();
            }
            catch (Exception ex)
            {

                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
        }
        /// <summary>
        /// 根据实体集合删除数据
        /// </summary>
        /// <param name="entities"></param>
        /// <returns></returns>
        public virtual async Task<MessageResult> DeleteAsync(List<TEntity> entities)
        {
            try
            {
                await Db.Deleteable(entities).ExecuteCommandAsync();

                return MessageResult.Success();
            }
            catch (Exception ex)
            {

                return MessageResult.Failed(ResultType.Failed, ex.Message, ex);
            }
        }

        /// <summary>
        /// 根据id查找实体
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public virtual async Task<TEntity> FindAsync(long id)
        {
            return await Db.Queryable<TEntity>().FirstAsync(t => t.Id == id);
        }

        /// <summary>
        /// 根据ids查找实体
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public virtual async Task<TEntity> FindAsync(long[] ids)
        {
            return await Db.Queryable<TEntity>().FirstAsync(t => ids.Contains(t.Id));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public virtual async Task<List<TEntity>> FindAsync(Expression<Func<TEntity, bool>> expression)
        {
            return await Db.Queryable<TEntity>().Where(expression).ToListAsync();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="search"></param>
        /// <returns></returns>
        public virtual async Task<(List<TEntity>, int)> FindAsync(ParSearch search)
        {
            var q = Db.Queryable<TEntity>();
            if (search.Conditionals != null)
                q = q.Where(search.Conditionals.Select(t => (IConditionalModel)t).ToList());

            var total = await q.CountAsync();

            if (search.OrderByArray != null)
            {
                foreach (var item in search.OrderByArray.Keys)
                {
                    q = q.OrderByPropertyName(item, search.OrderByArray[item] == "asc" ? OrderByType.Asc : OrderByType.Desc);
                }
            }

            var data = await q.ToPageListAsync(search.PageIndex, search.PageSize);

            return (data, total);
        }

        /// <summary>
        /// 表是否存在,判断表存不存在 
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public virtual bool ExistTable(string tableName)
        {
            return Db.DbMaintenance.IsAnyTable(tableName, false);
        }
    }
}
