﻿using Dapper;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Vasily;
using Vasily.Core;

namespace System
{
    public class DapperWrapper
    {
        public static char WR_Char;
        public string[] Unions;
        static DapperWrapper()
        {
            WR_Char = '|';
        }
        public IDbConnection Writter;
        public IDbConnection Reader;
        public VasilyRequestType RequestType;
        public DapperWrapper(string writter, string reader)
        {
            Writter = Connector.WriteInitor(writter)();
            Reader = Connector.ReadInitor(reader)();
            RequestType = VasilyRequestType.Complete;
            Unions = new string[0];
        }

        public int Sum(IEnumerable<int> indexs)
        {
            if (indexs == null)
            {
                return 0;
            }
            int result = 0;
            foreach (var item in indexs)
            {
                result += item;
            }
            return result;
        }

        protected IDbTransaction _transcation;

        /// <summary>
        /// 事务重试机制
        /// </summary>
        /// <param name="action">事务操作委托</param>
        /// <param name="retry">重试次数</param>
        /// <param name="get_errors">获取指定次数的异常错误</param>
        /// <returns>错误集合</returns>
        public List<Exception> TransactionRetry(Action<IDbConnection, IDbConnection> action, int retry = 1, params int[] get_errors)
        {
            List<Exception> errors = new List<Exception>();
            HashSet<int> dict = new HashSet<int>(get_errors);
            for (int i = 0; i < retry; i += 1)
            {
                try
                {
                    Transaction(action);
                    return errors;
                }
                catch (Exception ex)
                {
                    if (get_errors.Length == 0)
                    {
                        errors.Add(ex);
                    }
                    else if (dict.Contains(i))
                    {
                        errors.Add(ex);
                    }
                }
            }
            return errors;
        }

        /// <summary>
        /// 事务重试机制
        /// </summary>
        /// <param name="action">事务操作委托</param>
        /// <param name="retry">重试次数</param>
        /// <param name="predicate">每次异常获取的逻辑</param>
        /// <returns>错误集合</returns>
        public List<Exception> TransactionRetry(Action<IDbConnection, IDbConnection> action, int retry = 1, Predicate<int> predicate = null)
        {
            List<Exception> errors = new List<Exception>();
            if (predicate != null)
            {
                for (int i = 0; i < retry; i += 1)
                {
                    try
                    {
                        Transaction(action);
                        return errors;
                    }
                    catch (Exception ex)
                    {
                        if (predicate(i))
                        {
                            errors.Add(ex);
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < retry; i += 1)
                {
                    try
                    {
                        Transaction(action);
                        return errors;
                    }
                    catch (Exception ex)
                    {

                        errors.Add(ex);
                    }
                }
            }

            return errors;
        }

        public void Transaction(Action<IDbConnection, IDbConnection> action)
        {
            //开始事务
            using (IDbTransaction transaction = Reader.BeginTransaction())
            {
                try
                {
                    action?.Invoke(Reader, Writter);
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    //出现异常，事务Rollback
                    transaction.Rollback();
                    throw new Exception(ex.Message);
                }
            }
        }

    }
    public class DapperWrapper<T> : DapperWrapper
    {
        public static DapperWrapper<T> UseKey(string key)
        {
            return new DapperWrapper<T>(key);
        }
        public static DapperWrapper<T> UseKey(string writter, string reader)
        {
            return new DapperWrapper<T>(writter, reader);
        }
        public DapperWrapper(string key) : base(key, key) { }
        public DapperWrapper(string writter, string reader) : base(writter, reader) { }

        public DapperWrapper<T> Normal { get { RequestType = VasilyRequestType.Normal; return this; } }
        public DapperWrapper<T> Complete { get { RequestType = VasilyRequestType.Complete; return this; } }

        public int Count { get { return GetCount(); } }


        public DapperWrapper<T> UseUnion(params string[] tables)
        {
            Unions = tables;
            return this;
        }
        public DapperWrapper<T> UseTransaction()
        {
            _transcation = Reader.BeginTransaction();
            return this;
        }



        private string GetRealSqlString(SqlCondition<T> condition, string query)
        {
            if (Unions == null)
            {
                return query + condition.Full;

            }
            else
            {
                string result = SqlUnion<T>.Union(query + condition.Query, Unions) + condition.Tails;
                Unions = null;
                return result;
            }
        }
        private string GetRealSqlString(SqlCP cp, string query)
        {
            if (Unions == null)
            {
                return query + cp.Full;

            }
            else
            {
                string result = SqlUnion<T>.Union(Sql<T>.SelectAllByCondition + cp.Query, Unions) + cp.Tails;
                Unions = null;
                return result;
            }
        }

        /// <summary>
        /// 根据条件查询单个实体类
        /// </summary>
        /// <param name="condition">条件查询</param>
        /// <param name="instance">条件参数化实例</param>
        /// <returns></returns>
        public T Get(SqlCondition<T> condition, object instance)
        {
            string sql = null;

            if (RequestType == VasilyRequestType.Complete)
            {
                sql = GetRealSqlString(condition, Sql<T>.SelectAllByCondition);
            }
            else
            {
                sql = GetRealSqlString(condition, Sql<T>.SelectByCondition);
                
            }
            return Reader.QueryFirst<T>(sql, instance);

        }
        public T Get(SqlCP cp)
        {
            string sql = null;

            if (RequestType == VasilyRequestType.Complete)
            {
                sql = GetRealSqlString(cp, Sql<T>.SelectAllByCondition);
            }
            else
            {
                sql = GetRealSqlString(cp, Sql<T>.SelectByCondition);

            }
            return Reader.QueryFirst<T>(sql, cp);
        }


        /// <summary>
        /// 根据条件更新实体
        /// </summary>
        /// <param name="condition">条件查询</param>
        /// <param name="instance">更新参数化实例</param>
        /// <returns></returns>
        public int Modify(SqlCondition<T> condition, object instance)
        {
            string sql = null;

            if (RequestType == VasilyRequestType.Complete)
            {
                sql = GetRealSqlString(condition, Sql<T>.UpdateAllByCondition);
            }
            else
            {
                sql = GetRealSqlString(condition, Sql<T>.UpdateByCondition);
            }

            var result = Writter.Execute(sql, instance, transaction: _transcation);
            _transcation = null;
            return result;
        }
        public int Modify(SqlCP condition)
        {
            string sql = null;

            if (RequestType == VasilyRequestType.Complete)
            {
                sql = GetRealSqlString(condition, Sql<T>.UpdateAllByCondition);
            }
            else
            {
                sql = GetRealSqlString(condition, Sql<T>.UpdateByCondition);
            }

            var result = Writter.Execute(sql, condition.Instance, transaction: _transcation);
            _transcation = null;
            return result;
        }
        /// <summary>
        /// 根据条件删除实体
        /// </summary>
        /// <param name="condition">条件查询</param>
        /// <param name="instance">删除参数化实例</param>
        /// <returns></returns>
        public int Delete(SqlCondition<T> condition, object instance, ForceDelete flag = ForceDelete.No)
        {
            int result = 0;
            if (flag == ForceDelete.No)
            {
                result = Writter.Execute(Sql<T>.DeleteByCondition + condition.Full, instance, transaction: _transcation);
            }
            else
            {
                result =  Writter.Execute(SqlUnion<T>.Union(Sql<T>.DeleteByCondition + condition.SqlPages.ToString(), Unions) + condition.Tails, instance, transaction: _transcation);
            }
            _transcation = null;
            return result;
        }
        public int Delete(SqlCP cp, ForceDelete flag = ForceDelete.No)
        {
            int result = 0;
            if (flag == ForceDelete.No)
            {
                result= Writter.Execute(Sql<T>.DeleteByCondition + cp.Full, cp.Instance, transaction: _transcation);
            }
            else
            {
                result =  Writter.Execute(SqlUnion<T>.Union(Sql<T>.DeleteByCondition + cp.Query, Unions) + cp.Tails, cp.Instance, transaction: _transcation);
            }
            _transcation = null;
            return result;
        }
        /// <summary>
        /// 根据条件批量查询
        /// </summary>
        /// <param name="condition">条件查询</param>
        /// <param name="instance">查询参数化实例</param>
        /// <returns></returns>
        public IEnumerable<T> Gets(SqlCondition<T> condition, object instance)
        {
            string sql = null;

            if (RequestType == VasilyRequestType.Complete)
            {
                sql = GetRealSqlString(condition, Sql<T>.SelectAllByCondition);
            }
            else
            {
                sql = GetRealSqlString(condition, Sql<T>.SelectByCondition);
            }

            return Reader.Query<T>(sql, instance);
        }
        public IEnumerable<T> Gets(SqlCP condition)
        {

            string sql = null;

            if (RequestType == VasilyRequestType.Complete)
            {
                sql = GetRealSqlString(condition, Sql<T>.SelectAllByCondition);
            }
            else
            {
                sql = GetRealSqlString(condition, Sql<T>.SelectByCondition);
            }

            return Reader.Query<T>(sql, condition.Instance);

        }
        /// <summary>
        /// 根据条件批量查询数量
        /// </summary>
        /// <param name="condition">条件查询</param>
        /// <param name="instance">查询参数化实例</param>
        /// <returns></returns>
     
        public int CountWithCondition(SqlCondition<T> condition, object instance)
        {
            if (Unions==null)
            {
                return Reader.ExecuteScalar<int>(Sql<T>.SelectCountByCondition + condition.Full, instance);
            }
            else {
                var temp = Reader.Query<int>(SqlUnion<T>.Union(Sql<T>.SelectCountByCondition + condition.Query, Unions), instance);
                Unions = null;
                return Sum(temp);
            }
        }
        public int CountWithCondition(SqlCP condition)
        {
            if (Unions == null)
            {
                return Reader.ExecuteScalar<int>(Sql<T>.SelectCountByCondition + condition.Full, condition.Instance);
            }
            else
            {
                var temp = Reader.Query<int>(SqlUnion<T>.Union(Sql<T>.SelectCountByCondition + condition.Query, Unions), condition.Instance);
                Unions = null;
                return Sum(temp);
            }
        }

        /// <summary>
        /// 返回当前表总数
        /// </summary>
        /// <returns></returns>
        public int GetCount()
        {

            if (Unions == null)
            {
                return Reader.ExecuteScalar<int>(Sql<T>.SelectCount);
            }
            else
            {
                var temp = Reader.Query<int>(SqlUnion<T>.Union(Sql<T>.SelectCount, Unions));
                Unions = null;
                return Sum(temp);
            }
        }

        #region 把下面的Complate和Normal方法都封装一下
        /// <summary>
        /// 使用where id in (1,2,3)的方式，根据主键来获取对象集合，有Normal和Complete区分
        /// </summary>
        /// <param name="range">主键数组</param>
        /// <returns></returns>
        public IEnumerable<T> GetsIn(params int[] range)
        {
            if (RequestType == VasilyRequestType.Complete)
            {
                return Complete_GetByIn(range);
            }
            else
            {
                return Normal_GetByIn(range);
            }
        }
        /// <summary>
        /// 使用where id in (1,2,3)的方式，根据主键来获取对象集合，有Normal和Complete区分
        /// </summary>
        /// <param name="range">主键数组</param>
        /// <returns></returns>
        public IEnumerable<T> GetsIn(IEnumerable<int> range)
        {
            if (RequestType == VasilyRequestType.Complete)
            {
                return Complete_GetByIn(range.ToArray());
            }
            else
            {
                return Normal_GetByIn(range.ToArray());
            }
        }
        /// <summary>
        /// 使用where id in (1,2,3)的方式，根据主键来获取一个对象，有Normal和Complete区分
        /// </summary>
        /// <param name="range">主键</param>
        /// <returns></returns>
        public T GetIn(int range)
        {
            if (RequestType == VasilyRequestType.Complete)
            {
                return Complete_GetByPrimary(range);
            }
            else
            {
                return Normal_GetByPrimary(range);
            }
        }
        public T GetIn(IEnumerable<int> range)
        {
            if (RequestType == VasilyRequestType.Complete)
            {
                return Complete_GetByPrimary(range.ToArray()[0]);
            }
            else
            {
                return Normal_GetByPrimary(range.ToArray()[0]);
            }
        }
        /// <summary>
        /// 获取无条件，整个对象的在数据库的所有数据，有Normal和Complete区分
        /// </summary>
        /// <returns></returns>
        public IEnumerable<T> GetAll()
        {
            if (RequestType == VasilyRequestType.Complete)
            {
                return Complete_GetAll();
            }
            else
            {
                return Normal_GetAll();
            }
        }
        /// <summary>
        /// 通过主键获取单个实体，有Normal和Complete区分
        /// </summary>
        /// <param name="primary">主键</param>
        /// <returns></returns>
        public T GetByPrimary(object primary)
        {
            if (RequestType == VasilyRequestType.Complete)
            {
                return Complete_GetByPrimary(primary);
            }
            else
            {
                return Normal_GetByPrimary(primary);
            }
        }
        /// <summary>
        /// 更新实体或者实体的集合，有Normal和Complete区分
        /// </summary>
        /// <param name="instances">实体类</param>
        /// <returns></returns>
        public bool ModifyByPrimary(params T[] instances)
        {
            if (RequestType == VasilyRequestType.Complete)
            {
                return Complete_UpdateByPrimary(instances);
            }
            else
            {
                return Normal_UpdateByPrimary(instances);
            }
        }
        public bool ModifyByPrimary(IEnumerable<T> instances)
        {
            if (RequestType == VasilyRequestType.Complete)
            {
                return Complete_UpdateByPrimary(instances);
            }
            else
            {
                return Normal_UpdateByPrimary(instances);
            }
        }
        /// <summary>
        /// 插入实体或者实体的集合，有Normal和Complete区分
        /// </summary>
        /// <param name="instances">实体类</param>
        /// <returns></returns>
        public int Add(params T[] instances)
        {
            if (RequestType == VasilyRequestType.Complete)
            {
                return Complate_Insert(instances);
            }
            else
            {
                return Normal_Insert(instances);
            }
        }
        public int Add(IEnumerable<T> instances)
        {
            if (RequestType == VasilyRequestType.Complete)
            {
                return Complate_Insert(instances);
            }
            else
            {
                return Normal_Insert(instances);
            }
        }


        #endregion


        #region 完整实体类的SELECT函数
        /// <summary>
        /// 获取表中所有的完整实体类
        /// </summary>
        /// <returns>结果集</returns>
        internal IEnumerable<T> Complete_GetAll()
        {

            string sql = null;
            if (Unions==null)
            {
                sql = Sql<T>.SelectAll;
            }
            else
            {
                sql = SqlUnion<T>.Union(Sql<T>.SelectAll, Unions);
                Unions = null;
            }
            return Reader.Query<T>(sql);
        }
        /// <summary>
        /// 根据主键来获取完整的实体类
        /// </summary>
        /// <param name="primary">主键ID</param>
        /// <returns>实体类</returns>
        internal T Complete_GetByPrimary(object primary)
        {
            var dynamicParams = new DynamicParameters();
            dynamicParams.Add(Sql<T>.Primary, primary);
            return Reader.QuerySingle<T>(Sql<T>.SelectAllByPrimary, dynamicParams);
        }
        /// <summary>
        /// 获取指定范围主键的完整实体类
        /// </summary>
        /// <param name="range">主键范围</param>
        /// <returns>结果集</returns>
        internal IEnumerable<T> Complete_GetByIn<S>(params S[] range)
        {
            var dynamicParams = new DynamicParameters();
            dynamicParams.Add("keys", range);
            return Reader.Query<T>(Sql<T>.SelectAllIn, dynamicParams);
        }


        #endregion

        #region 业务相关实体类的SELECT函数
        /// <summary>
        /// 获取表中所有的业务相关的实体类(带有select_ignore标签的会被排除)
        /// </summary>
        /// <returns></returns>
        internal IEnumerable<T> Normal_GetAll()
        {
            string sql = null;
            if (Unions == null)
            {
                sql = Sql<T>.Select;
            }
            else
            {
                sql = SqlUnion<T>.Union(Sql<T>.Select, Unions);
                Unions = null;
            }
            return Reader.Query<T>(sql);
        }
        /// <summary>
        /// 根据主键来获取业务相关的实体类(带有select_ignore标签的会被排除)
        /// </summary>
        /// <param name="primary">主键ID</param>
        /// <returns>实体类</returns>
        internal T Normal_GetByPrimary(object primary)
        {
            var dynamicParams = new DynamicParameters();
            dynamicParams.Add(Sql<T>.Primary, primary);
            return Reader.QuerySingle<T>(Sql<T>.SelectByPrimary, dynamicParams);
        }
        /// <summary>
        /// 获取指定范围主键的普通实体类
        /// </summary>
        /// <param name="range">主键范围</param>
        /// <returns>结果集</returns>
        internal IEnumerable<T> Normal_GetByIn<S>(params S[] range)
        {
            var dynamicParams = new DynamicParameters();
            dynamicParams.Add("keys", range);
            return Reader.Query<T>(Sql<T>.SelectIn, dynamicParams);
        }
        #endregion


        #region 完整实体类的UPDATE函数
        /// <summary>
        /// 根据主键更新
        /// </summary>
        /// <param name="instance">需要更新的实体类</param>
        /// <returns>更新结果</returns>
        internal bool Complete_UpdateByPrimary(params T[] instances)
        {
            bool result = false;
            string sql = null;
            if (Unions == null)
            {
                sql = Sql<T>.UpdateAllByPrimary;
            }
            else
            {
                sql = SqlUnion<T>.Union(Sql<T>.UpdateAllByPrimary, Unions);
                Unions = null;
            }
            result = Writter.Execute(sql, instances, transaction: _transcation) == instances.Length;
            _transcation = null;
            return result;
           }
        internal bool Complete_UpdateByPrimary(IEnumerable<T> instances)
        {
            bool result = false;
            string sql = null;
            if (Unions == null)
            {
                sql = Sql<T>.UpdateAllByPrimary;
            }
            else
            {
                sql = SqlUnion<T>.Union(Sql<T>.UpdateAllByPrimary, Unions);
                Unions = null;
            }
            result = Writter.Execute(sql, instances, transaction: _transcation) == instances.Count();
            _transcation = null;
            return result;
        }
        #endregion

        #region 业务相关实体类的UPDATE函数
        /// <summary>
        /// 根据主键更新
        /// </summary>
        /// <param name="instance">需要更新的实体类</param>
        /// <returns>更新结果</returns>
        internal bool Normal_UpdateByPrimary(params T[] instances)
        {
            bool result = false;
            string sql = null;
            if (Unions == null)
            {
                sql = Sql<T>.UpdateByPrimary;
            }
            else
            {
                sql = SqlUnion<T>.Union(Sql<T>.UpdateByPrimary, Unions);
                Unions = null;
            }
            result = Writter.Execute(sql, instances, transaction: _transcation) == instances.Length;
            _transcation = null;
            return result;
        }
        internal bool Normal_UpdateByPrimary(IEnumerable<T> instances)
        {
            bool result = false;
            string sql = null;
            if (Unions == null)
            {
                sql = Sql<T>.UpdateByPrimary;
            }
            else
            {
                sql = SqlUnion<T>.Union(Sql<T>.UpdateByPrimary, Unions);
                Unions = null;
            }
            result = Writter.Execute(sql, instances, transaction: _transcation) == instances.Count();
            _transcation = null;
            return result;
        }
        #endregion


        #region 完整实体类的INSERT函数
        /// <summary>
        /// 插入新节点
        /// </summary>
        /// <param name="instances">实体类</param>
        /// <returns>返回结果</returns>
        internal int Complate_Insert(params T[] instances)
        {
            int result = 0;
            result = Writter.Execute(Sql<T>.InsertAll, instances, transaction: _transcation);
            _transcation = null;
            return result;
        }
        internal int Complate_Insert(IEnumerable<T> instances)
        {
            int result = 0;
            result = Writter.Execute(Sql<T>.InsertAll, instances, transaction: _transcation);
            _transcation = null;
            return result;
        }
        #endregion

        #region 业务相关实体类的INSERT函数
        /// <summary>
        /// 插入新节点
        /// </summary>
        /// <param name="instances">实体类</param>
        /// <returns>返回结果</returns>
        internal int Normal_Insert(params T[] instances)
        {
            int result = 0;
            result = Writter.Execute(Sql<T>.Insert, instances, transaction: _transcation);
            _transcation = null;
            return result;
        }
        internal int Normal_Insert(IEnumerable<T> instances)
        {
            int result = 0;
            result = Writter.Execute(Sql<T>.Insert, instances, transaction: _transcation);
            _transcation = null;
            return result;
        }
        #endregion


        #region 查重函数
        /// <summary>
        /// 节点查重
        /// </summary>
        /// <param name="instance">实体类条件</param>
        /// <returns>返回结果</returns>
        public bool IsRepeat(T instance)
        {
            if (Unions == null)
            {
                return Reader.ExecuteScalar<int>(Sql<T>.RepeateCount)>0;
            }
            else
            {
                IEnumerable<int> temp = Reader.Query<int>(SqlUnion<T>.Union(Sql<T>.RepeateCount, Unions), instance);
                Unions = null;
                return Sum(temp) > 0;
            }
        }
        /// <summary>
        /// 通过实体类获取跟其相同唯一约束的集合
        /// </summary>
        /// <param name="instance">实体类</param>
        /// <returns>结果集</returns>
        public IEnumerable<T> GetRepeates(T instance)
        {
            if (Unions == null)
            {
                return Reader.Query<T>(Sql<T>.RepeateEntities);
            }
            else
            {
                var result = Reader.Query<T>(SqlUnion<T>.Union(Sql<T>.RepeateEntities, Unions));
                Unions = null;
                return result;
            }
        }
        /// <summary>
        /// 通过实体类获取当前实体的主键，注：只有实体类有NoRepeate条件才能用
        /// </summary>
        /// <typeparam name="S">主键类型</typeparam>
        /// <param name="instance">实体类</param>
        /// <returns>主键</returns>
        public S GetNoRepeateId<S>(T instance)
        {
            if (Unions == null)
            {
                return Reader.ExecuteScalar<S>(Sql<T>.RepeateId);
            }
            else
            {
                IEnumerable<S> temp = Reader.Query<S>(SqlUnion<T>.Union(Sql<T>.RepeateId, Unions), instance);
                Unions = null;
                foreach (var item in temp)
                {
                    if (item != null)
                    {
                        return item;
                    }
                }
                return default(S);
            }
        }
        /// <summary>
        /// 通过查重条件进行不重复插入，有Normal和Complete区分
        /// </summary>
        /// <param name="instance">实体类</param>
        /// <returns></returns>
        public bool NoRepeateAdd(T instance)
        {
            if (!IsRepeat(instance))
            {
                if (RequestType == VasilyRequestType.Complete)
                {
                    return Complate_Insert(instance) > 0;
                }
                else
                {
                    return Normal_Insert(instance) > 0;
                }
            }
            return false;
        }
        /// <summary>
        /// 先查重，如果没有则插入，再根据插入的实体类通过唯一约束找到主键赋值给实体类，有Normal和Complete区分
        /// </summary>
        /// <typeparam name="S">主键类型</typeparam>
        /// <param name="instance">实体类</param>
        /// <returns></returns>
        public bool SafeAdd(T instance)
        {
            bool result = false;

            if (NoRepeateAdd(instance))
            {
                result = true;
            }

            object obj = Reader.ExecuteScalar<object>(Sql<T>.RepeateId, instance);
            Sql<T>.SetPrimary(instance, obj);

            return result;
        }
        #endregion

        #region 实体类的DELETE函数
        /// <summary>
        /// 根据主键删除
        /// </summary>
        /// <param name="primary">主键ID</param>
        /// <returns>更新结果</returns>
        public bool SingleDeleteByPrimary(object primary)
        {
            var dynamicParams = new DynamicParameters();
            dynamicParams.Add(Sql<T>.Primary, primary);
            return Writter.Execute(Sql<T>.DeleteByPrimary, dynamicParams) == 1;
        }
        /// <summary>
        /// 根据主键删除
        /// </summary>
        /// <param name="primary">主键ID</param>
        /// <returns>更新结果</returns>
        public bool EntitiesDeleteByPrimary(params T[] instances)
        {
            bool result = false;
            result = Writter.Execute(Sql<T>.DeleteByPrimary, instances, transaction: _transcation) == instances.Length;
            _transcation = null;
            return result;
        }

        public bool EntitiesDeleteByPrimary(IEnumerable<T> instances)
        {
            bool result = false;
            result = Writter.Execute(Sql<T>.DeleteByPrimary, instances, transaction: _transcation) == instances.Count();
            _transcation = null;
            return result;
        }
        #endregion

        public static implicit operator DapperWrapper<T>(string key)
        {
            if (key.Contains(WR_Char))
            {
                string[] result = key.Split(WR_Char);
                return new DapperWrapper<T>(result[0].Trim(), result[1].Trim());
            }
            return new DapperWrapper<T>(key);
        }
    }
    public abstract class RelationWrapper<T> : DapperWrapper<T>
    {
        internal MemberGetter[] _emits;
        internal string[] _sources;
        internal string[] _tables;
        public RelationWrapper(string key) : this(key, key)
        {

        }
        public RelationWrapper(string writter, string reader) : base(writter, reader)
        {

        }


        #region 内部函数封装
        internal int TableExecute(string sql, params object[] parameters)
        {
            var dynamicParams = new DynamicParameters();
            for (int i = 0; i < _tables.Length; i += 1)
            {
                dynamicParams.Add(_tables[i], parameters[i]);
            }
            return Reader.Execute(sql, dynamicParams);
        }
        internal int TableAftExecute(string sql, params object[] parameters)
        {
            var dynamicParams = new DynamicParameters();
            for (int i = 0; i < _tables.Length - 1; i += 1)
            {
                dynamicParams.Add(_tables[i + 1], parameters[i + 1]);
            }
            return Reader.Execute(sql, dynamicParams);
        }
        internal int SourceExecute(string sql, params object[] parameters)
        {
            var dynamicParams = new DynamicParameters();
            for (int i = 0; i < _sources.Length; i += 1)
            {
                dynamicParams.Add(_sources[i], _emits[i](parameters[i]));
            }
            return Reader.Execute(sql, dynamicParams);
        }
        internal int SourceAftExecute(string sql, params object[] parameters)
        {
            var dynamicParams = new DynamicParameters();
            for (int i = 0; i < _sources.Length - 1; i += 1)
            {
                dynamicParams.Add(_sources[i + 1], _emits[i + 1](parameters[i]));
            }
            return Reader.Execute(sql, dynamicParams);
        }

        /// <summary>
        /// 直接查询到实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第三个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1 </param>
        /// <returns></returns>
        internal IEnumerable<T> SourceGets_Wrapper(string sql, params object[] parameters)
        {
            var dynamicParams = new DynamicParameters();
            for (int i = 0; i < _sources.Length - 1; i += 1)
            {
                dynamicParams.Add(_sources[i + 1], _emits[i + 1](parameters[i]));
            }
            var range = Reader.Query<int>(sql, dynamicParams);
            return GetsIn(range);
        }

        /// <summary>
        /// 获取关系
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        internal T SourceGet_Wrapper(string sql, params object[] parameters)
        {
            var dynamicParams = new DynamicParameters();
            for (int i = 0; i < _sources.Length - 1; i += 1)
            {
                dynamicParams.Add(_sources[i + 1], _emits[i + 1](parameters[i]));
            }
            var range = Reader.Query<int>(sql, dynamicParams);
            return GetIn(range);
        }

        /// <summary>
        /// 直接查询到实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第三个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1 </param>
        /// <returns></returns>
        internal IEnumerable<T> TableGets_Wrapper(string sql, params object[] parameters)
        {
            var dynamicParams = new DynamicParameters();
            for (int i = 0; i < _tables.Length - 1; i += 1)
            {
                dynamicParams.Add(_tables[i + 1], parameters[i]);
            }
            var range = Reader.Query<int>(sql, dynamicParams);
            return GetsIn(range);
        }

        /// <summary>
        /// 获取关系
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        internal T TableGet_Wrapper(string sql, params object[] parameters)
        {
            var dynamicParams = new DynamicParameters();
            for (int i = 0; i < _tables.Length - 1; i += 1)
            {
                dynamicParams.Add(_tables[i + 1], parameters[i]);
            }
            var range = Reader.Query<int>(sql, dynamicParams);
            return GetIn(range);
        }
        #endregion

        #region 用实体类进行查询
        /// <summary>
        /// 获取集合-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public virtual IEnumerable<T> SourceGets(params object[] parameters)
        {
            return null;
        }

        /// <summary>
        /// 获取集合数量-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public virtual int SourceCount(params object[] parameters)
        {
            return 0;
        }
        /// <summary>
        /// 更新操作-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public virtual int SourceModify(params object[] parameters)
        {
            return 0;
        }
        /// <summary>
        /// 删除关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where t=@t</param>
        /// <returns></returns>
        public virtual int SourcePreDelete(params object[] parameters)
        {
            return 0;
        }
        /// <summary>
        /// 删除关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public virtual int SourceAftDelete(params object[] parameters)
        {
            return 0;
        }
        /// <summary>
        /// 增加关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型</param>
        /// <returns></returns>
        public virtual int SourceAdd(params object[] parameters)
        {
            return 0;
        }
        /// <summary>
        /// 获取关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public virtual T SourceGet(params object[] parameters)
        {
            return default(T);
        }
        #endregion

        #region 不知道实体类信息
        /// <summary>
        /// 查询到实体类-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第三个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1 </param>
        /// <returns></returns>
        public virtual IEnumerable<T> TableGets(params object[] parameters)
        {
            return null;
        }
        /// <summary>
        /// 获取集合数量-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public virtual int TableCount(params object[] parameters)
        {
            return 0;
        }
        /// <summary>
        /// 更新操作-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public virtual int TableModify(params object[] parameters)
        {
            return 0;
        }
        /// <summary>
        /// 删除关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where t=@t</param>
        /// <returns></returns>
        public virtual int TablePreDelete(params object[] parameters)
        {
            return 0;
        }
        /// <summary>
        /// 删除关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public virtual int TableAftDelete(params object[] parameters)
        {
            return 0;
        }
        /// <summary>
        /// 增加关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型</param>
        /// <returns></returns>
        public virtual int TableAdd(params object[] parameters)
        {
            return 0;
        }
        /// <summary>
        /// 获取关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public virtual T TableGet(params object[] parameters)
        {
            return default(T);
        }
        #endregion
    }

    public class DapperWrapper<T, R, C1> : RelationWrapper<T>
    {
        public static implicit operator DapperWrapper<T, R, C1>(string key)
        {
            if (key.Contains(WR_Char))
            {
                string[] result = key.Split(WR_Char);
                return new DapperWrapper<T, R, C1>(result[0].Trim(), result[1].Trim());
            }
            return new DapperWrapper<T, R, C1>(key);
        }
        public new static RelationWrapper<T> UseKey(string key)
        {
            return new DapperWrapper<T, R, C1>(key);
        }
        public new static RelationWrapper<T> UseKey(string writter, string reader)
        {
            return new DapperWrapper<T, R, C1>(writter, reader);
        }
        public DapperWrapper(string key) : this(key, key)
        {

        }
        public DapperWrapper(string writter, string reader) : base(writter, reader)
        {
            _tables = RelationSql<T, R, C1>.TableConditions;
            _sources = RelationSql<T, R, C1>.SourceConditions;
            _emits = RelationSql<T, R, C1>.Getters;
        }

        #region 用实体类进行查询
        /// <summary>
        /// 获取集合-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override IEnumerable<T> SourceGets(params object[] parameters)
        {
            return SourceGets_Wrapper(RelationSql<T, R, C1>.GetFromSource, parameters);
        }
        /// <summary>
        /// 获取集合数量-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override int SourceCount(params object[] parameters)
        {
            return SourceAftExecute(RelationSql<T, R, C1>.CountFromSource, parameters);
        }
        /// <summary>
        /// 更新操作-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override int SourceModify(params object[] parameters)
        {
            return SourceExecute(RelationSql<T, R, C1>.ModifyFromSource, parameters);
        }
        /// <summary>
        /// 删除关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where t=@t</param>
        /// <returns></returns>
        public override int SourcePreDelete(params object[] parameters)
        {
            return SourceExecute(RelationSql<T, R, C1>.DeletePreFromSource, parameters);
        }
        /// <summary>
        /// 删除关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override int SourceAftDelete(params object[] parameters)
        {
            return SourceAftExecute(RelationSql<T, R, C1>.DeleteAftFromSource, parameters);
        }
        /// <summary>
        /// 增加关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型</param>
        /// <returns></returns>
        public override int SourceAdd(params object[] parameters)
        {
            return SourceExecute(RelationSql<T, R, C1>.AddFromSource, 0, parameters);
        }
        /// <summary>
        /// 获取关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override T SourceGet(params object[] parameters)
        {
            return SourceGet_Wrapper(RelationSql<T, R, C1>.GetFromSource, parameters);
        }
        #endregion

        #region 不知道实体类信息
        /// <summary>
        /// 查询到实体类-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第三个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1 </param>
        /// <returns></returns>
        public override IEnumerable<T> TableGets(params object[] parameters)
        {
            return TableGets_Wrapper(RelationSql<T, R, C1>.GetFromTable, parameters);
        }
        /// <summary>
        /// 获取集合数量-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override int TableCount(params object[] parameters)
        {
            return SourceAftExecute(RelationSql<T, R, C1>.CountFromTable, parameters);
        }
        /// <summary>
        /// 更新操作-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override int TableModify(params object[] parameters)
        {
            return TableExecute(RelationSql<T, R, C1>.ModifyFromTable, parameters);
        }
        /// <summary>
        /// 删除关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where t=@t</param>
        /// <returns></returns>
        public override int TablePreDelete(params object[] parameters)
        {
            return TableExecute(RelationSql<T, R, C1>.DeletePreFromTable, parameters);
        }
        /// <summary>
        /// 删除关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override int TableAftDelete(params object[] parameters)
        {
            return TableAftExecute(RelationSql<T, R, C1>.DeleteAftFromTable, parameters);
        }
        /// <summary>
        /// 增加关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型</param>
        /// <returns></returns>
        public override int TableAdd(params object[] parameters)
        {
            return TableExecute(RelationSql<T, R, C1>.AddFromTable, parameters);
        }


        /// <summary>
        /// 获取关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override T TableGet(params object[] parameters)
        {
            return TableGet_Wrapper(RelationSql<T, R, C1>.GetFromTable, parameters);
        }
        #endregion


    }

    public class DapperWrapper<T, R, C1, C2> : RelationWrapper<T>
    {
        public static implicit operator DapperWrapper<T, R, C1, C2>(string key)
        {
            if (key.Contains(WR_Char))
            {
                string[] result = key.Split(WR_Char);
                return new DapperWrapper<T, R, C1, C2>(result[0].Trim(), result[1].Trim());
            }
            return new DapperWrapper<T, R, C1, C2>(key);
        }
        public new static RelationWrapper<T> UseKey(string key)
        {
            return new DapperWrapper<T, R, C1, C2>(key);
        }
        public new static RelationWrapper<T> UseKey(string writter, string reader)
        {
            return new DapperWrapper<T, R, C1, C2>(writter, reader);
        }
        public DapperWrapper(string key) : this(key, key)
        {

        }
        public DapperWrapper(string writter, string reader) : base(writter, reader)
        {
            _tables = RelationSql<T, R, C1, C2>.TableConditions;
            _sources = RelationSql<T, R, C1, C2>.SourceConditions;
            _emits = RelationSql<T, R, C1, C2>.Getters;
        }

        #region 用实体类进行查询
        /// <summary>
        /// 获取集合-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override IEnumerable<T> SourceGets(params object[] parameters)
        {
            return SourceGets_Wrapper(RelationSql<T, R, C1, C2>.GetFromSource, parameters);
        }
        /// <summary>
        /// 获取集合数量-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override int SourceCount(params object[] parameters)
        {
            return SourceAftExecute(RelationSql<T, R, C1, C2>.CountFromSource, parameters);
        }
        /// <summary>
        /// 更新操作-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override int SourceModify(params object[] parameters)
        {
            return SourceExecute(RelationSql<T, R, C1, C2>.ModifyFromSource, parameters);
        }
        /// <summary>
        /// 删除关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where t=@t</param>
        /// <returns></returns>
        public override int SourcePreDelete(params object[] parameters)
        {
            return SourceExecute(RelationSql<T, R, C1, C2>.DeletePreFromSource, parameters);
        }
        /// <summary>
        /// 删除关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override int SourceAftDelete(params object[] parameters)
        {
            return SourceAftExecute(RelationSql<T, R, C1, C2>.DeleteAftFromSource, parameters);
        }
        /// <summary>
        /// 增加关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型</param>
        /// <returns></returns>
        public override int SourceAdd(params object[] parameters)
        {
            return SourceExecute(RelationSql<T, R, C1, C2>.AddFromSource, 0, parameters);
        }
        /// <summary>
        /// 获取关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override T SourceGet(params object[] parameters)
        {
            return SourceGet_Wrapper(RelationSql<T, R, C1, C2>.GetFromSource, parameters);
        }
        #endregion

        #region 不知道实体类信息
        /// <summary>
        /// 查询到实体类-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第三个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1 </param>
        /// <returns></returns>
        public override IEnumerable<T> TableGets(params object[] parameters)
        {
            return TableGets_Wrapper(RelationSql<T, R, C1, C2>.GetFromTable, parameters);
        }
        /// <summary>
        /// 获取集合数量-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override int TableCount(params object[] parameters)
        {
            return SourceAftExecute(RelationSql<T, R, C1, C2>.CountFromTable, parameters);
        }
        /// <summary>
        /// 更新操作-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override int TableModify(params object[] parameters)
        {
            return TableExecute(RelationSql<T, R, C1, C2>.ModifyFromTable, parameters);
        }
        /// <summary>
        /// 删除关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where t=@t</param>
        /// <returns></returns>
        public override int TablePreDelete(params object[] parameters)
        {
            return TableExecute(RelationSql<T, R, C1, C2>.DeletePreFromTable, parameters);
        }
        /// <summary>
        /// 删除关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override int TableAftDelete(params object[] parameters)
        {
            return TableAftExecute(RelationSql<T, R, C1, C2>.DeleteAftFromTable, parameters);
        }
        /// <summary>
        /// 增加关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型</param>
        /// <returns></returns>
        public override int TableAdd(params object[] parameters)
        {
            return TableExecute(RelationSql<T, R, C1, C2>.AddFromTable, parameters);
        }
        /// <summary>
        /// 获取关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override T TableGet(params object[] parameters)
        {
            return TableGet_Wrapper(RelationSql<T, R, C1, C2>.GetFromTable, parameters);
        }
        #endregion


    }

    public class DapperWrapper<T, R, C1, C2, C3> : RelationWrapper<T>
    {
        public static implicit operator DapperWrapper<T, R, C1, C2, C3>(string key)
        {
            if (key.Contains(WR_Char))
            {
                string[] result = key.Split(WR_Char);
                return new DapperWrapper<T, R, C1, C2, C3>(result[0].Trim(), result[1].Trim());
            }
            return new DapperWrapper<T, R, C1, C2, C3>(key);
        }
        public new static RelationWrapper<T> UseKey(string key)
        {
            return new DapperWrapper<T, R, C1, C2, C3>(key);
        }
        public new static RelationWrapper<T> UseKey(string writter, string reader)
        {
            return new DapperWrapper<T, R, C1, C2, C3>(writter, reader);
        }
        public DapperWrapper(string key) : this(key, key)
        {

        }
        public DapperWrapper(string writter, string reader) : base(writter, reader)
        {
            _tables = RelationSql<T, R, C1, C2, C3>.TableConditions;
            _sources = RelationSql<T, R, C1, C2, C3>.SourceConditions;
            _emits = RelationSql<T, R, C1, C2, C3>.Getters;
        }

        #region 用实体类进行查询
        /// <summary>
        /// 获取集合-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override IEnumerable<T> SourceGets(params object[] parameters)
        {
            return SourceGets_Wrapper(RelationSql<T, R, C1, C2, C3>.GetFromSource, parameters);
        }

        /// <summary>
        /// 获取集合数量-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override int SourceCount(params object[] parameters)
        {
            return SourceAftExecute(RelationSql<T, R, C1, C2, C3>.CountFromSource, parameters);
        }

        /// <summary>
        /// 更新操作-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override int SourceModify(params object[] parameters)
        {
            return SourceExecute(RelationSql<T, R, C1, C2, C3>.ModifyFromSource, parameters);
        }
        /// <summary>
        /// 删除关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where t=@t</param>
        /// <returns></returns>
        public override int SourcePreDelete(params object[] parameters)
        {
            return SourceExecute(RelationSql<T, R, C1, C2, C3>.DeletePreFromSource, parameters);
        }
        /// <summary>
        /// 删除关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override int SourceAftDelete(params object[] parameters)
        {
            return SourceAftExecute(RelationSql<T, R, C1, C2, C3>.DeleteAftFromSource, parameters);
        }
        /// <summary>
        /// 增加关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型</param>
        /// <returns></returns>
        public override int SourceAdd(params object[] parameters)
        {
            return SourceExecute(RelationSql<T, R, C1, C2, C3>.AddFromSource, 0, parameters);
        }
        /// <summary>
        /// 获取关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override T SourceGet(params object[] parameters)
        {
            return SourceGet_Wrapper(RelationSql<T, R, C1, C2, C3>.GetFromSource, parameters);
        }
        #endregion

        #region 不知道实体类信息
        /// <summary>
        /// 查询到实体类-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第三个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1 </param>
        /// <returns></returns>
        public override IEnumerable<T> TableGets(params object[] parameters)
        {
            return TableGets_Wrapper(RelationSql<T, R, C1, C2, C3>.GetFromTable, parameters);
        }
        /// <summary>
        /// 获取集合数量-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override int TableCount(params object[] parameters)
        {
            return SourceAftExecute(RelationSql<T, R, C1, C2, C3>.CountFromTable, parameters);
        }
        /// <summary>
        /// 更新操作-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override int TableModify(params object[] parameters)
        {
            return TableExecute(RelationSql<T, R, C1, C2, C3>.ModifyFromTable, parameters);
        }
        /// <summary>
        /// 删除关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where t=@t</param>
        /// <returns></returns>
        public override int TablePreDelete(params object[] parameters)
        {
            return TableExecute(RelationSql<T, R, C1, C2, C3>.DeletePreFromTable, parameters);
        }
        /// <summary>
        /// 删除关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override int TableAftDelete(params object[] parameters)
        {
            return TableAftExecute(RelationSql<T, R, C1, C2, C3>.DeleteAftFromTable, parameters);
        }
        /// <summary>
        /// 增加关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型</param>
        /// <returns></returns>
        public override int TableAdd(params object[] parameters)
        {
            return TableExecute(RelationSql<T, R, C1, C2, C3>.AddFromTable, parameters);
        }
        /// <summary>
        /// 获取关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override T TableGet(params object[] parameters)
        {
            return TableGet_Wrapper(RelationSql<T, R, C1, C2, C3>.GetFromTable, parameters);
        }
        #endregion


    }

    public class DapperWrapper<T, R, C1, C2, C3, C4> : RelationWrapper<T>
    {
        public static implicit operator DapperWrapper<T, R, C1, C2, C3, C4>(string key)
        {
            if (key.Contains(WR_Char))
            {
                string[] result = key.Split(WR_Char);
                return new DapperWrapper<T, R, C1, C2, C3, C4>(result[0].Trim(), result[1].Trim());
            }
            return new DapperWrapper<T, R, C1, C2, C3, C4>(key);
        }
        public new static RelationWrapper<T> UseKey(string key)
        {
            return new DapperWrapper<T, R, C1, C2, C3, C4>(key);
        }
        public new static RelationWrapper<T> UseKey(string writter, string reader)
        {
            return new DapperWrapper<T, R, C1, C2, C3, C4>(writter, reader);
        }
        public DapperWrapper(string key) : this(key, key)
        {

        }
        public DapperWrapper(string writter, string reader) : base(writter, reader)
        {
            _tables = RelationSql<T, R, C1, C2, C3, C4>.TableConditions;
            _sources = RelationSql<T, R, C1, C2, C3, C4>.SourceConditions;
            _emits = RelationSql<T, R, C1, C2, C3, C4>.Getters;
        }

        #region 用实体类进行
        /// <summary>
        /// 获取集合-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override IEnumerable<T> SourceGets(params object[] parameters)
        {
            return SourceGets_Wrapper(RelationSql<T, R, C1, C2, C3, C4>.GetFromSource, parameters);
        }
        /// <summary>
        /// 获取集合数量-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override int SourceCount(params object[] parameters)
        {
            return SourceAftExecute(RelationSql<T, R, C1, C2, C3, C4>.CountFromSource, parameters);
        }
        /// <summary>
        /// 更新操作-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override int SourceModify(params object[] parameters)
        {
            return SourceExecute(RelationSql<T, R, C1, C2, C3, C4>.ModifyFromSource, parameters);
        }
        /// <summary>
        /// 删除关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where t=@t</param>
        /// <returns></returns>
        public override int SourcePreDelete(params object[] parameters)
        {
            return SourceExecute(RelationSql<T, R, C1, C2, C3, C4>.DeletePreFromSource, parameters);
        }
        /// <summary>
        /// 删除关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override int SourceAftDelete(params object[] parameters)
        {
            return SourceAftExecute(RelationSql<T, R, C1, C2, C3, C4>.DeleteAftFromSource, parameters);
        }
        /// <summary>
        /// 增加关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型</param>
        /// <returns></returns>
        public override int SourceAdd(params object[] parameters)
        {
            return SourceExecute(RelationSql<T, R, C1, C2, C3, C4>.AddFromSource, 0, parameters);
        }
        /// <summary>
        /// 获取关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override T SourceGet(params object[] parameters)
        {
            return SourceGet_Wrapper(RelationSql<T, R, C1, C2, C3, C4>.GetFromSource, parameters);
        }
        #endregion

        #region 不知道实体类信息
        /// <summary>
        /// 查询到实体类-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第三个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1 </param>
        /// <returns></returns>
        public override IEnumerable<T> TableGets(params object[] parameters)
        {
            return TableGets_Wrapper(RelationSql<T, R, C1, C2, C3, C4>.GetFromTable, parameters);
        }
        /// <summary>
        /// 获取集合数量-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override int TableCount(params object[] parameters)
        {
            return SourceAftExecute(RelationSql<T, R, C1, C2, C3, C4>.CountFromTable, parameters);
        }
        /// <summary>
        /// 更新操作-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override int TableModify(params object[] parameters)
        {
            return TableExecute(RelationSql<T, R, C1, C2, C3, C4>.ModifyFromTable, parameters);
        }
        /// <summary>
        /// 删除关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where t=@t</param>
        /// <returns></returns>
        public override int TablePreDelete(params object[] parameters)
        {
            return TableExecute(RelationSql<T, R, C1, C2, C3, C4>.DeletePreFromTable, parameters);
        }
        /// <summary>
        /// 删除关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override int TableAftDelete(params object[] parameters)
        {
            return TableAftExecute(RelationSql<T, R, C1, C2, C3, C4>.DeleteAftFromTable, parameters);
        }
        /// <summary>
        /// 增加关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型</param>
        /// <returns></returns>
        public override int TableAdd(params object[] parameters)
        {
            return TableExecute(RelationSql<T, R, C1, C2, C3, C4>.AddFromTable, parameters);
        }
        /// <summary>
        /// 获取关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override T TableGet(params object[] parameters)
        {
            return TableGet_Wrapper(RelationSql<T, R, C1, C2, C3, C4>.GetFromTable, parameters);
        }
        #endregion


    }

    public class DapperWrapper<T, R, C1, C2, C3, C4, C5> : RelationWrapper<T>
    {
        public static implicit operator DapperWrapper<T, R, C1, C2, C3, C4, C5>(string key)
        {
            if (key.Contains(WR_Char))
            {
                string[] result = key.Split(WR_Char);
                return new DapperWrapper<T, R, C1, C2, C3, C4, C5>(result[0].Trim(), result[1].Trim());
            }
            return new DapperWrapper<T, R, C1, C2, C3, C4, C5>(key);
        }
        public new static RelationWrapper<T> UseKey(string key)
        {
            return new DapperWrapper<T, R, C1, C2, C3, C4, C5>(key);
        }
        public new static RelationWrapper<T> UseKey(string writter, string reader)
        {
            return new DapperWrapper<T, R, C1, C2, C3, C4, C5>(writter, reader);
        }
        public DapperWrapper(string key) : this(key, key)
        {

        }
        public DapperWrapper(string writter, string reader) : base(writter, reader)
        {
            _tables = RelationSql<T, R, C1, C2, C3, C4, C5>.TableConditions;
            _sources = RelationSql<T, R, C1, C2, C3, C4, C5>.SourceConditions;
            _emits = RelationSql<T, R, C1, C2, C3, C4, C5>.Getters;
        }

        #region 用实体类进行查询
        /// <summary>
        /// 获取集合-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override IEnumerable<T> SourceGets(params object[] parameters)
        {
            return SourceGets_Wrapper(RelationSql<T, R, C1, C2, C3, C4, C5>.GetFromSource, parameters);
        }
        /// <summary>
        /// 获取集合数量-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override int SourceCount(params object[] parameters)
        {
            return SourceAftExecute(RelationSql<T, R, C1, C2, C3, C4, C5>.CountFromSource, parameters);
        }
        /// <summary>
        /// 更新操作-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override int SourceModify(params object[] parameters)
        {
            return SourceExecute(RelationSql<T, R, C1, C2, C3, C4, C5>.ModifyFromSource, parameters);
        }
        /// <summary>
        /// 删除关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where t=@t</param>
        /// <returns></returns>
        public override int SourcePreDelete(params object[] parameters)
        {
            return SourceExecute(RelationSql<T, R, C1, C2, C3, C4, C5>.DeletePreFromSource, parameters);
        }
        /// <summary>
        /// 删除关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override int SourceAftDelete(params object[] parameters)
        {
            return SourceAftExecute(RelationSql<T, R, C1, C2, C3, C4, C5>.DeleteAftFromSource, parameters);
        }
        /// <summary>
        /// 增加关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型</param>
        /// <returns></returns>
        public override int SourceAdd(params object[] parameters)
        {
            return SourceExecute(RelationSql<T, R, C1, C2, C3, C4, C5>.AddFromSource, 0, parameters);
        }
        /// <summary>
        /// 获取关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override T SourceGet(params object[] parameters)
        {
            return SourceGet_Wrapper(RelationSql<T, R, C1, C2, C3, C4, C5>.GetFromSource, parameters);
        }
        #endregion

        #region 不知道实体类信息
        /// <summary>
        /// 查询到实体类-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第三个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1 </param>
        /// <returns></returns>
        public override IEnumerable<T> TableGets(params object[] parameters)
        {
            return TableGets_Wrapper(RelationSql<T, R, C1, C2, C3, C4, C5>.GetFromTable, parameters);
        }
        /// <summary>
        /// 获取集合数量-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override int TableCount(params object[] parameters)
        {
            return SourceAftExecute(RelationSql<T, R, C1, C2, C3, C4, C5>.CountFromTable, parameters);
        }
        /// <summary>
        /// 更新操作-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override int TableModify(params object[] parameters)
        {
            return TableExecute(RelationSql<T, R, C1, C2, C3, C4, C5>.ModifyFromTable, parameters);
        }
        /// <summary>
        /// 删除关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where t=@t</param>
        /// <returns></returns>
        public override int TablePreDelete(params object[] parameters)
        {
            return TableExecute(RelationSql<T, R, C1, C2, C3, C4, C5>.DeletePreFromTable, parameters);
        }
        /// <summary>
        /// 删除关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override int TableAftDelete(params object[] parameters)
        {
            return TableAftExecute(RelationSql<T, R, C1, C2, C3, C4, C5>.DeleteAftFromTable, parameters);
        }
        /// <summary>
        /// 增加关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型</param>
        /// <returns></returns>
        public override int TableAdd(params object[] parameters)
        {
            return TableExecute(RelationSql<T, R, C1, C2, C3, C4, C5>.AddFromTable, parameters);
        }
        /// <summary>
        /// 获取关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override T TableGet(params object[] parameters)
        {
            return TableGet_Wrapper(RelationSql<T, R, C1, C2, C3, C4, C5>.GetFromTable, parameters);
        }
        #endregion


    }

    public class DapperWrapper<T, R, C1, C2, C3, C4, C5, C6> : RelationWrapper<T>
    {
        public static implicit operator DapperWrapper<T, R, C1, C2, C3, C4, C5, C6>(string key)
        {
            if (key.Contains(WR_Char))
            {
                string[] result = key.Split(WR_Char);
                return new DapperWrapper<T, R, C1, C2, C3, C4, C5, C6>(result[0].Trim(), result[1].Trim());
            }
            return new DapperWrapper<T, R, C1, C2, C3, C4, C5, C6>(key);
        }
        public new static RelationWrapper<T> UseKey(string key)
        {
            return new DapperWrapper<T, R, C1, C2, C3, C4, C5, C6>(key);
        }
        public new static RelationWrapper<T> UseKey(string writter, string reader)
        {
            return new DapperWrapper<T, R, C1, C2, C3, C4, C5, C6>(writter, reader);
        }
        public DapperWrapper(string key) : this(key, key)
        {

        }
        public DapperWrapper(string writter, string reader) : base(writter, reader)
        {
            _tables = RelationSql<T, R, C1, C2, C3, C4, C5, C6>.TableConditions;
            _sources = RelationSql<T, R, C1, C2, C3, C4, C5, C6>.SourceConditions;
            _emits = RelationSql<T, R, C1, C2, C3, C4, C5, C6>.Getters;
        }

        #region 用实体类进行查询
        /// <summary>
        /// 获取集合-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override IEnumerable<T> SourceGets(params object[] parameters)
        {
            return SourceGets_Wrapper(RelationSql<T, R, C1, C2, C3, C4, C5, C6>.GetFromSource, parameters);
        }
        /// <summary>
        /// 获取集合数量-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override int SourceCount(params object[] parameters)
        {
            return SourceAftExecute(RelationSql<T, R, C1, C2, C3, C4, C5, C6>.CountFromSource, parameters);
        }
        /// <summary>
        /// 更新操作-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override int SourceModify(params object[] parameters)
        {
            return SourceExecute(RelationSql<T, R, C1, C2, C3, C4, C5, C6>.ModifyFromSource, parameters);
        }
        /// <summary>
        /// 删除关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where t=@t</param>
        /// <returns></returns>
        public override int SourcePreDelete(params object[] parameters)
        {
            return SourceExecute(RelationSql<T, R, C1, C2, C3, C4, C5, C6>.DeletePreFromSource, parameters);
        }
        /// <summary>
        /// 删除关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override int SourceAftDelete(params object[] parameters)
        {
            return SourceAftExecute(RelationSql<T, R, C1, C2, C3, C4, C5, C6>.DeleteAftFromSource, parameters);
        }
        /// <summary>
        /// 增加关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型</param>
        /// <returns></returns>
        public override int SourceAdd(params object[] parameters)
        {
            return SourceExecute(RelationSql<T, R, C1, C2, C3, C4, C5, C6>.AddFromSource, 0, parameters);
        }
        /// <summary>
        /// 获取关系-直接传实体类
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override T SourceGet(params object[] parameters)
        {
            return SourceGet_Wrapper(RelationSql<T, R, C1, C2, C3, C4, C5, C6>.GetFromSource, parameters);
        }
        #endregion

        #region 不知道实体类信息
        /// <summary>
        /// 查询到实体类-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第三个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1 </param>
        /// <returns></returns>
        public override IEnumerable<T> TableGets(params object[] parameters)
        {
            return TableGets_Wrapper(RelationSql<T, R, C1, C2, C3, C4, C5, C6>.GetFromTable, parameters);
        }
        /// <summary>
        /// 获取集合数量-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override int TableCount(params object[] parameters)
        {
            return SourceAftExecute(RelationSql<T, R, C1, C2, C3, C4, C5, C6>.CountFromTable, parameters);
        }
        /// <summary>
        /// 更新操作-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型），set t=@t where c1=@c1</param>
        /// <returns></returns>
        public override int TableModify(params object[] parameters)
        {
            return TableExecute(RelationSql<T, R, C1, C2, C3, C4, C5, C6>.ModifyFromTable, parameters);
        }
        /// <summary>
        /// 删除关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where t=@t</param>
        /// <returns></returns>
        public override int TablePreDelete(params object[] parameters)
        {
            return TableExecute(RelationSql<T, R, C1, C2, C3, C4, C5, C6>.DeletePreFromTable, parameters);
        }
        /// <summary>
        /// 删除关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override int TableAftDelete(params object[] parameters)
        {
            return TableAftExecute(RelationSql<T, R, C1, C2, C3, C4, C5, C6>.DeleteAftFromTable, parameters);
        }
        /// <summary>
        /// 增加关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第1个类型起<T,R,C1>的T,C1,详见F12泛型类型</param>
        /// <returns></returns>
        public override int TableAdd(params object[] parameters)
        {
            return TableExecute(RelationSql<T, R, C1, C2, C3, C4, C5, C6>.AddFromTable, parameters);
        }
        /// <summary>
        /// 获取关系-直接传值
        /// </summary>
        /// <param name="parameters">参数顺序（泛型类型参数从第3个类型起<T,R,C1>的C1,详见F12泛型类型），where c1=@c1</param>
        /// <returns></returns>
        public override T TableGet(params object[] parameters)
        {
            return TableGet_Wrapper(RelationSql<T, R, C1, C2, C3, C4, C5, C6>.GetFromTable, parameters);
        }
        #endregion


    }
    public enum VasilyRequestType
    {
        Complete = 0,
        Normal = 1
    }
}
