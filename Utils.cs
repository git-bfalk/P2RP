﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace PrivacyRootPatcher
{
    class Utils
    {
        public static TypeDef[] GetAllNestedTypes(TypeDef type)
        {
            List<TypeDef> result = new List<TypeDef>();
            foreach (TypeDef nsType in type.NestedTypes)
            {
                if (nsType.HasNestedTypes) { result.AddRange(GetAllNestedTypes(nsType)); }
                result.Add(nsType);
            }
            return result.ToArray();
        }

        public static TypeDef[] GetAllTypes(ModuleDefMD asm)
        {
            List<TypeDef> result = new List<TypeDef>();
            foreach (TypeDef type in asm.Types)
            {
                if (type.HasNestedTypes) { result.AddRange(GetAllNestedTypes(type)); }
                result.Add(type);
            }
            return result.ToArray();
        }

        public static bool CompareMemberRef(Instruction srcInst, Type matchType, string methodName, Type[] methodArgs = null, bool isConstructor = false)
        {
            string instNamespace = ((MemberRef)srcInst.Operand).DeclaringType.FullName + "." + ((MemberRef)srcInst.Operand).Name,
                   matchClassNamespace = matchType.FullName,
                   matchMethodName = "";
            if (methodArgs == null) { matchMethodName = matchType.GetMethod(methodName).Name; }
            else { matchMethodName = !isConstructor ? matchType.GetMethod(methodName, methodArgs).Name : matchType.GetConstructor(methodArgs).Name; }
            return instNamespace == matchClassNamespace + "." + matchMethodName;
        }
    }
}
