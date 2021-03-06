﻿using System;
using System.Reflection;

namespace Vasily.Extensions.ReflectionExtensions
{
    public static class MemberInfoExtension
    {
        public static Type GetCustomerType(this MemberInfo member)
        {
            if (member.MemberType == MemberTypes.Field)
            {
                return ((FieldInfo)member).FieldType;
            }
            else if (member.MemberType == MemberTypes.Property)
            {
                return ((PropertyInfo)member).PropertyType;
            }
            else if (member.MemberType == MemberTypes.Method)
            {
                return ((MethodInfo)member).ReturnType;
            }
            return null;
        }
    }
}
