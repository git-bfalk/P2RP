using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Runtime.CompilerServices;

namespace PrivacyRootPatcher
{
    class StringResolver
    {
        private ModuleDefMD asm;
        private byte[] strData = null;

        public StringResolver(ModuleDefMD mod)
        {
            asm = mod;
            foreach (TypeDef type in Utils.GetAllTypes(asm))
            {
                foreach (MethodDef method in type.Methods)
                {
                    if (!method.HasBody || method.Body.Instructions.Count == 0) { continue; }
                    //if (method.MDToken.ToInt32() == 100663324) { int.Parse("0"); }
                    for (int x = 0; x < method.Body.Instructions.Count; x++)
                    {
                        Instruction inst = method.Body.Instructions[x];

                        if (inst.OpCode == OpCodes.Call && inst.Operand is MethodDef)
                        {
                            MethodDef tempMethod = inst.Operand as MethodDef;
                            if (!tempMethod.HasBody || tempMethod.Body.Instructions.Count == 0) { continue; }

                            if (tempMethod.Body.Instructions[0].OpCode == OpCodes.Ldsfld &&
                                tempMethod.Body.Instructions[0].Operand is FieldDef &&
                                (tempMethod.Body.Instructions[0].Operand as FieldDef).FieldType.FullName == typeof(string[]).FullName &&
                                tempMethod.Body.Instructions[1].IsLdcI4() &&
                                tempMethod.Body.Instructions[2].OpCode == OpCodes.Ldelem_Ref &&
                                (tempMethod.Body.Instructions[4].IsBrtrue() || tempMethod.Body.Instructions[4].IsBrfalse()) &&
                                tempMethod.Body.Instructions[6].IsLdcI4() &&
                                tempMethod.Body.Instructions[7].IsLdcI4() &&
                                tempMethod.Body.Instructions[8].IsLdcI4() &&
                                tempMethod.Body.Instructions[9].OpCode == OpCodes.Call &&
                                tempMethod.Body.Instructions[9].Operand is MethodDef)
                            {
                                if (strData == null) { GetStringData(tempMethod.Body.Instructions[9].Operand as MethodDef); }
                                method.Body.Instructions[x].OpCode = OpCodes.Ldstr;
                                method.Body.Instructions[x].Operand = Encoding.UTF8.GetString(strData,
                                        tempMethod.Body.Instructions[7].GetLdcI4Value(),
                                        tempMethod.Body.Instructions[8].GetLdcI4Value());
                            }
                        }
                    }
                }
            }
        }

        public Dictionary<MethodDef, int> GetStringInitializer(FieldDef field)
        {
            Dictionary<MethodDef, int> result = new Dictionary<MethodDef, int>();
            foreach (TypeDef type in Utils.GetAllTypes(asm))
            {
                foreach (MethodDef method in type.Methods)
                {
                    if (!method.HasBody || method.Body.Instructions.Count == 0) { continue; }

                    for (int x = 0; x < method.Body.Instructions.Count; x++)
                    {
                        Instruction inst = method.Body.Instructions[x];

                        switch (inst.OpCode.Code)
                        {
                            case Code.Stfld:
                            case Code.Stsfld:
                                if (inst.Operand is FieldDef && inst.Operand as FieldDef == field)
                                {
                                    result.Add(method, x);
                                    continue;
                                }
                                break;
                        }
                    }
                }
            }
            return result;
        }

        public void GetString(int index)
        {
        }

        public void GetStringData(MethodDef method)
        {
            if (method.Body.Instructions[0].OpCode == OpCodes.Call &&
                method.Body.Instructions[0].Operand is MemberRef &&
                (method.Body.Instructions[0].Operand as MemberRef).DeclaringType.FullName == typeof(Encoding).FullName &&
                method.Body.Instructions[1].OpCode == OpCodes.Ldsfld &&
                method.Body.Instructions[1].Operand is FieldDef &&
                (method.Body.Instructions[1].Operand as FieldDef).FieldType.FullName == typeof(byte[]).FullName &&
                method.Body.Instructions[2].IsLdarg() &&
                method.Body.Instructions[3].IsLdarg() &&
                method.Body.Instructions[6].OpCode == OpCodes.Ldsfld &&
                method.Body.Instructions[6].Operand is FieldDef &&
                method.Body.Instructions[9].OpCode == OpCodes.Stelem_Ref)
            {
                Dictionary<MethodDef, int> methodArr = GetStringInitializer(method.Body.Instructions[1].Operand as FieldDef);
                foreach (MethodDef str in methodArr.Keys)
                {
                    int x = methodArr[str];
                    if (x - 1 >= 0 &&
                        str.Body.Instructions[x - 1].OpCode == OpCodes.Call &&
                        str.Body.Instructions[x - 1].Operand is MemberRef &&
                        Utils.CompareMemberRef(str.Body.Instructions[x - 1], typeof(RuntimeHelpers), "InitializeArray", new Type[] { typeof(Array), typeof(RuntimeFieldHandle) }, false) &&
                        str.Body.Instructions[x-2].OpCode == OpCodes.Ldtoken &&
                        str.Body.Instructions[x-2].Operand is FieldDef)
                        //(str.Body.Instructions[x-2].Operand as FieldDef).FieldType.FullName == typeof(byte[]).FullName)
                    {
                        byte[] encData = (str.Body.Instructions[x - 2].Operand as FieldDef).InitialValue;
                        strData = new byte[encData.Length];
                        for (int i = 0; i < encData.Length; i++)
                        {
                            strData[i] = (byte)(encData[i] ^ i ^ str.Body.Instructions[x + 11].GetLdcI4Value());
                        }
                        return;
                    }
                }
            }
        }
    }
}
