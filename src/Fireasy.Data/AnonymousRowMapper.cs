﻿// -----------------------------------------------------------------------
// <copyright company="Fireasy"
//      email="faib920@126.com"
//      qq="55570729">
//   (c) Copyright Fireasy. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Fireasy.Common;
using Fireasy.Common.Extensions;
using Fireasy.Data.RecordWrapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Fireasy.Data
{
    /// <summary>
    /// 一个用于将数据行转换为匿名类型对象的映射器。无法继承此类。
    /// </summary>
    /// <typeparam name="T">要构造的匿名类型。</typeparam>
    public class AnonymousRowMapper<T> : IDataRowMapper<T>
    {
        private Func<IDataReader, T> funcDataRecd;
        private Func<DataRow, T> funcDataRow;

        /// <summary>
        /// 将一个 <see cref="IDataReader"/> 转换为一个 <typeparamref name="T"/> 的对象。
        /// </summary>
        /// <param name="reader">一个 <see cref="IDataReader"/> 对象。</param>
        /// <returns>由当前 <see cref="IDataReader"/> 对象中的数据转换成的 <typeparamref name="T"/> 对象实例。</returns>
        public T Map(IDataReader reader)
        {
            if (funcDataRecd == null)
            {
                funcDataRecd = BuildExpressionForDataReader().Compile();
            }

            return funcDataRecd(reader);
        }

        /// <summary>
        /// 将一个 <see cref="DataRow"/> 转换为一个 <typeparamref name="T"/> 的对象。
        /// </summary>
        /// <param name="row">一个 <see cref="DataRow"/> 对象。</param>
        /// <returns>由 <see cref="DataRow"/> 中数据转换成的 <typeparamref name="T"/> 对象实例。</returns>
        public T Map(DataRow row)
        {
            if (funcDataRow == null)
            {
                funcDataRow = BuildExpressionForDataRow().Compile();
            }

            return funcDataRow(row);
        }

        /// <summary>
        /// 获取或设置 <see cref="IRecordWrapper"/>。
        /// </summary>
        public IRecordWrapper RecordWrapper { get; set; }

        /// <summary>
        /// 获取或设置对象的初始化器。
        /// </summary>
        public Action<object> Initializer { get; set; }

        object IDataRowMapper.Map(IDataReader reader)
        {
            return Map(reader);
        }

        object IDataRowMapper.Map(DataRow row)
        {
            return Map(row);
        }

        private IEnumerable<ParameterInfo> GetParameters(ConstructorInfo conInfo)
        {
            return conInfo.GetParameters().Where(s => Extensions.DataExtension.IsDbTypeSupported(s.ParameterType));
        }

        protected virtual Expression<Func<IDataReader, T>> BuildExpressionForDataReader()
        {
            var conInfo = typeof(T).GetConstructors().FirstOrDefault();
            Guard.NullReference(conInfo);
            var parExp = Expression.Parameter(typeof(IDataReader), "s");
            var convertMethod = typeof(GenericExtension).GetMethod("ToType");
            var itemGetMethod = typeof(IRecordWrapper).GetMethod("GetValue", new[] { typeof(IDataReader), typeof(string) });
            var parameters =
                GetParameters(conInfo).Select(s => (Expression)Expression.Convert(
                            Expression.Call(convertMethod, new Expression[] 
                                    { 
                                        Expression.Call(Expression.Constant(RecordWrapper), itemGetMethod, new Expression[] { parExp, Expression.Constant(s.Name) }),
                                        Expression.Constant(s.ParameterType),
                                        Expression.Constant(null)
                                    }
                            ), s.ParameterType));

            var newExp = Expression.New(conInfo, parameters);

            return Expression.Lambda<Func<IDataReader, T>>(
                    Expression.MemberInit(newExp), parExp);
        }

        protected virtual Expression<Func<DataRow, T>> BuildExpressionForDataRow()
        {
            var conInfo = typeof(T).GetConstructors().FirstOrDefault();
            Guard.NullReference(conInfo);
            var parExp = Expression.Parameter(typeof(DataRow), "s");
            var convertMethod = typeof(GenericExtension).GetMethod("ToType");
#if NET35
            var itemGetMethod = typeof(DataRow).GetMethod("get_Item", new[] { typeof(string) });
#else
            var itemProperty = typeof(DataRow).GetProperty("Item", new[] { typeof(string) });
#endif
            var parameters =
                GetParameters(conInfo).Select(s => (Expression)Expression.Convert(
                            Expression.Call(convertMethod, new Expression[] 
                                    { 
#if NET35
                                        Expression.Call(parExp, itemGetMethod, Expression.Constant(s.Name)),
#else
                                        Expression.MakeIndex(parExp, itemProperty, new List<Expression> { Expression.Constant(s.Name) }),
#endif
                                        Expression.Constant(s.ParameterType),
                                        Expression.Constant(null)
                                    }
                            ), s.ParameterType));

            var newExp = Expression.New(conInfo, parameters);

            return Expression.Lambda<Func<DataRow, T>>(
                    Expression.MemberInit(newExp), parExp);
        }
    }
}
