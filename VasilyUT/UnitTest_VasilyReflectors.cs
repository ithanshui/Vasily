﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vasily;
using Vasily.Core;
using VasilyUT.Entity;
using Xunit;

namespace VasilyUT
{
    public class UnitTest_VasilyReflectors
    {

        [Fact(DisplayName ="单成员单标签实例")]
        public void TestSaSiSm()
        {
            AttrOperator reflector = new AttrOperator(typeof(Test));
            var result = reflector.Mapping<PrimaryKeyAttribute>();
            Assert.NotNull(result.Instance);
            Assert.Equal(result.Member, typeof(Test).GetMember("Id")[0]);
        }

        [Fact(DisplayName = "单标签多成员")]
        public void TestSaMm()
        {
            AttrOperator reflector = new AttrOperator(typeof(Test));
            var result =new List<MemberInfo>(reflector.Members<IgnoreAttribute>());
            Assert.Equal(2,result.Count());
            Assert.Equal(result.Find(item=> { return item.Name == "Ignore1"; }),typeof(Test).GetMember("Ignore1")[0]);
            Assert.Equal(result.Find(item => { return item.Name == "Ignore2"; }), typeof(Test).GetMember("Ignore2")[0]);
        }

        [Fact(DisplayName = "单标签实例")]
        public void TestSaSi()
        {
            AttrOperator reflector = new AttrOperator(typeof(Test));
            var result = reflector.ClassInstance<TableAttribute>();
            Assert.Equal("Table",result.Name);
        }

        [Fact(DisplayName = "单标签实例")]
        public void TestSaSm()
        {
            AttrOperator reflector = new AttrOperator(typeof(Test));
            var result = reflector.Member<NoRepeateAttribute>();
            Assert.Equal(typeof(Test).GetMember("NoRepeate")[0], result);
        }
    }
}