﻿using System.Reflection;
using System.Text;
using Vasily.Standard;

namespace Vasily.Core
{
    public class SelectTemplate : ISelect
    {
        /// <summary>
        /// 根据model信息生成 SELECT * FROM [TableName]
        /// </summary>
        /// <param name="model">载有生成信息的Model</param>
        /// <returns>查询字符串结果</returns>
        public string SelectAll(MakerModel model)
        {
            StringBuilder sql = new StringBuilder(16 + model.TableName.Length);
            sql.Append("SELECT * FROM ");
            sql.Append(model.Left);
            sql.Append(model.TableName);
            sql.Append(model.Right);
            return sql.ToString();
        }

        /// <summary>
        /// 根据model信息生成 SELECT Count(*) FROM [TableName]
        /// </summary>
        /// <param name="model">载有生成信息的Model</param>
        /// <returns>查询字符串结果</returns>
        public string SelectCount(MakerModel model)
        {
            StringBuilder sql = new StringBuilder(16 + model.TableName.Length);
            sql.Append("SELECT Count(*) FROM ");
            sql.Append(model.Left);
            sql.Append(model.TableName);
            sql.Append(model.Right);
            return sql.ToString();
        }


        /// <summary>
        /// 根据model信息生成 SELECT * FROM [TableName] WHERE
        /// </summary>
        /// <param name="model">载有生成信息的Model</param>
        /// <returns>查询字符串结果</returns>
        public string SelectAllByCondition(MakerModel model)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append(SelectAll(model));
            sql.Append(" WHERE ");
            return sql.ToString();
        }

        /// <summary>
        /// 根据model信息生成 SELECT Count(*) FROM [TableName] WHERE
        /// </summary>
        /// <param name="model">载有生成信息的Model</param>
        /// <returns>查询字符串结果</returns>
        public string SelectCountByCondition(MakerModel model)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append(SelectCount(model));
            sql.Append(" WHERE ");
            return sql.ToString();
        }

        /// <summary>
        /// 根据model信息生成 SELECT Count(*) WHERE [condition1]=@condition,[condition2]=@condition2.....
        /// </summary>
        /// <param name="model">载有生成信息的Model</param>
        /// <param name="condition_models">需要匹配的成员集合</param>
        /// <returns>查询字符串结果</returns>
        public string SelectCountWithCondition(MakerModel model, params MemberInfo[] conditions)
        {
            var select = SelectCountByCondition(model);
            StringBuilder sql = new StringBuilder(select);
            ConditionTemplate template = new ConditionTemplate();
            sql.Append(template.Condition(model, conditions));
            return sql.ToString();
        }



        /// <summary>
        /// 根据model信息生成 SELECT * FROM [TableName] WHERE [PrimaryKey] = @PrimaryKey
        /// </summary>
        /// <param name="model">载有生成信息的Model</param>
        /// <returns>查询字符串结果</returns>
        public string SelectAllByPrimary(MakerModel model)
        {

            if (model.PrimaryKey != null)
            {
                StringBuilder sql = new StringBuilder();
                sql.Append(SelectAllByCondition(model));
                sql.Append(model.Left);
                sql.Append(model.PrimaryKey);
                sql.Append(model.Right);
                sql.Append("=@");
                sql.Append(model.PrimaryKey);
                return sql.ToString();
            }
            return null;
        }
        /// <summary>
        /// 根据model信息生成 SELECT * FROM [TableName] WHERE [PrimaryKey] IN @keys
        /// </summary>
        /// <param name="model">载有生成信息的Model</param>
        /// <returns>查询字符串结果</returns>
        public string SelectAllIn(MakerModel model)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append(SelectAllByCondition(model));
            sql.Append(model.Left);
            sql.Append(model.PrimaryKey);
            sql.Append(model.Right);
            sql.Append(" IN @keys");
            return sql.ToString();
        }

        /// <summary>
        /// 根据model信息生成 SELECT [member1],[member2]... FROM [TableName]
        /// </summary>
        /// <param name="model">载有生成信息的Model</param>
        /// <returns>查询字符串结果</returns>
        public string Select(MakerModel model)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append("SELECT ");
            foreach (var item in model.Members)
            {
                sql.Append(model.Left);
                if (model.ColFunction!= null)
                {
                    sql.Append(model.ColFunction(item));
                }
                else
                {
                    sql.Append(item.Name);
                }
                sql.Append(model.Right);
                sql.Append(",");
            }
            sql.Length -= 1;
            sql.Append(" FROM ");
            sql.Append(model.Left);
            sql.Append(model.TableName);
            sql.Append(model.Right);
            return sql.ToString();
        }

        /// <summary>
        /// 根据model信息生成 SELECT [member1],[member2]... FROM [TableName] WHERE 
        /// </summary>
        /// <param name="model">载有生成信息的Model</param>
        /// <returns>查询字符串结果</returns>
        public string SelectByCondition(MakerModel model)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append(Select(model));
            sql.Append(" WHERE ");
            return sql.ToString();
        }

        /// <summary>
        /// 根据model信息生成 SELECT [member1],[member2]... FROM [TableName] WHERE [PrimaryKey]=@PrimaryKey
        /// </summary>
        /// <param name="model">载有生成信息的Model</param>
        /// <returns>查询字符串结果</returns>
        public string SelectByPrimary(MakerModel model)
        {
            if (model.PrimaryKey != null)
            {
                StringBuilder sql = new StringBuilder();
                sql.Append(SelectByCondition(model));
                sql.Append(model.Left);
                sql.Append(model.PrimaryKey);
                sql.Append(model.Right);
                sql.Append("=@");
                sql.Append(model.PrimaryKey);
                return sql.ToString();
            }
            return null;
        }

        /// <summary>
        /// 根据model信息生成 SELECT [member1],[member2]... FROM [TableName] WHERE [PrimaryKey] IN @keys
        /// </summary>
        /// <param name="model">载有生成信息的Model</param>
        /// <returns>查询字符串结果</returns>
        public string SelectIn(MakerModel model)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append(SelectByCondition(model));
            sql.Append(model.Left);
            sql.Append(model.PrimaryKey);
            sql.Append(model.Right);
            sql.Append(" IN @keys");
            return sql.ToString();
        }



        /// <summary>
        /// 根据model信息生成 SELECT [member1],[member2]... FROM [TableName] WHERE [condition1]=@condition,[condition2]=@condition2.....
        /// </summary>
        /// <param name="model">载有生成信息的Model</param>
        /// <param name="condition_models">需要匹配的成员集合</param>
        /// <returns>查询字符串结果</returns>
        public string SelectWithCondition(MakerModel model, params MemberInfo[] conditions)
        {
            var select = SelectByCondition(model);
            StringBuilder sql = new StringBuilder(select);
            ConditionTemplate template = new ConditionTemplate();
            sql.Append(template.Condition(model, conditions));
            return sql.ToString();
        }
    }
}
