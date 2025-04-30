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
    }

    public abstract class BasicService<TEntity> : BasicService where TEntity : BasicEntity, new()
    {
        public BasicService(ISqlSugarClient db, ILoggerFactory loggerFactory) : base(db, loggerFactory)
        {
        }

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
        public virtual async Task<MessageResult> DeleteAsync(params long[] ids)
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

        public virtual async Task<TEntity> FindAsync(long id)
        {
            return await Db.Queryable<TEntity>().FirstAsync(t => t.Id == id);
        }
        public virtual async Task<TEntity> FindAsync(params long[] ids)
        {
            return await Db.Queryable<TEntity>().FirstAsync(t => ids.Contains(t.Id));
        }
        public virtual async Task<List<TEntity>> FindAsync(Expression<Func<TEntity, bool>> expression)
        {
            return await Db.Queryable<TEntity>().Where(expression).ToListAsync();
        }

        public virtual async Task<(List<TEntity>, int)> FindAsync(ParSearch search)
        {
            var q = Db.Queryable<TEntity>();
            if (search.Conditionals != null)
                q = q.Where(search.Conditionals);

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
