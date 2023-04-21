using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.IO;
using System.Windows.Forms;

namespace PrivacyRootPatcher
{
    class Program
    {
        static readonly string uactivatedFieldName = "UActivated";

        class AppIdentity
        {
            public string Version { get; set; }
            public TargetType Type { get; set; }
        }

        static List<ModuleDefMD> asms = new List<ModuleDefMD>();
        static List<string> log = new List<string>();
        static List<AppIdentity> appIds = new List<AppIdentity>();
        static List<string> paths = new List<string>();

        static void Main(string[] args)
        {
            Console.CursorVisible = false;
            Console.Title = "PrivacyRootPatcher v2.0 - @rc_bfalk - Runcrime";
            foreach (string arg in args)
            {
                try
                {
                    if (File.Exists(arg))
                    {
                        ModuleDefMD temp = ModuleDefMD.Load(arg);
                        asms.Add(temp);
                        paths.Add(arg);
                    }
                }
                catch { }
            }
            ChangeForeColor(ConsoleColor.White);
            Console.Write("PrivacyRootPatcher v2.0 - ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("https://github.com/git-bfalk/P2RP");
            ChangeForeColor(ConsoleColor.White);
            Console.Write(" - ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Runcrime");
            Console.WriteLine();
            ChangeForeColor(ConsoleColor.White);
            Console.WriteLine("Detected:");
            foreach (ModuleDefMD asm in asms)
            {
                Console.Write(" - ");
                AppIdentity appId = Identify(asm);
                ChangeColorFromType(appId.Type);
                Console.Write("'" + Enum.GetName(typeof(TargetType), appId.Type) + "'");
                Console.CursorLeft = 35;
                ChangeForeColor(ConsoleColor.Magenta);
                Console.WriteLine(appId.Version);
                Console.ResetColor();
                appIds.Add(appId);
            }
            for (int x = 0; x < asms.Count; x++)
            {
                Console.WriteLine();
                AddToLog("############## - Patching ", false, ConsoleColor.White);
                ChangeColorFromType(appIds[x].Type);
                Console.Write("'" + Enum.GetName(typeof(TargetType), appIds[x].Type) + "'");
                AddToLog("... - " + PrintToWidth('#', Console.CursorLeft+6), true, ConsoleColor.White);
                Console.WriteLine();
                AddToLog(" Deobfuscating '" + Enum.GetName(typeof(TargetType), appIds[x].Type) + "'...", false, ConsoleColor.Yellow);
                Console.CursorLeft = 35;
                try { new StringResolver(asms[x]); }
                catch
                {
                    AddToLog("Failed!", false, ConsoleColor.Red);
                    continue;
                }
                AddToLog("successfully cleaned up!", true, ConsoleColor.Green);
                Patch(asms[x], appIds[x]);
                Console.WriteLine();
                AddToLog(" Saving Patch...", false, ConsoleColor.DarkGreen);
                Console.CursorLeft = 35;
                try
                {
                    asms[x].Write(paths[x].Replace(".exe", null) + "_patched.exe");
                }
                catch
                {
                    AddToLog("Failed!", false, ConsoleColor.Red);
                }
                AddToLog("saved! (" + Path.GetFileName(paths[x]).Replace(".exe", null) + "_patched.exe)", true, ConsoleColor.Green);
            }
            Console.WriteLine();
            Console.WriteLine("-------------------------");
            AddToLog("   " + asms.Count + " file(s) patched!", true, ConsoleColor.Green);
            Console.WriteLine();
            Console.WriteLine(" Press any key to exit...");
            Console.ReadKey();
        }

        static void ChangeColorFromType(TargetType type)
        {
            switch (type)
            {
                case TargetType.SecureDelete: ChangeForeColor(ConsoleColor.Red); break;
                case TargetType.SecretDisk: ChangeForeColor(ConsoleColor.Cyan); break;
                case TargetType.PreventRestore:
                case TargetType.DuplicateFileFinder: ChangeForeColor(ConsoleColor.DarkYellow); break;
                case TargetType.Wipe: ChangeForeColor(ConsoleColor.DarkRed); break;
            }
        }

        static FieldDef GetActivationField(ModuleDefMD asm)
        {
            foreach (TypeDef type in Utils.GetAllTypes(asm))
            {
                foreach (MethodDef method in type.Methods)
                {
                    if (!method.HasBody || method.Body.Instructions.Count == 0) { continue; }
                    for (int x = 0; x < method.Body.Instructions.Count; x++)
                    {
                        Instruction inst = method.Body.Instructions[x];
                        if (inst.OpCode == OpCodes.Ldstr && inst.Operand.ToString() == uactivatedFieldName &&
                            method.Body.Instructions[x+1].OpCode == OpCodes.Ldsfld)
                        {
                            return method.Body.Instructions[x + 1].Operand as FieldDef;
                        }
                    }
                }
            }
            return null;
        }

        static void Patch(ModuleDefMD asm, AppIdentity appId)
        {
            AddToLog(" Searching for '" + uactivatedFieldName + "'...", false, ConsoleColor.Yellow);
            Console.CursorLeft = 35;
            FieldDef uactivate = GetActivationField(asm);
            if (uactivate == null)
            {
                AddToLog(" Failed!", false, ConsoleColor.Red);
                return;
            }
            AddToLog("found!", true, ConsoleColor.Green);
            Console.WriteLine();
            foreach (TypeDef type in asm.Types)
            {
                for (int x = 0; x < type.Methods.Count; x++)
                {
                    MethodDef method = type.Methods[x];
                    if (!method.HasBody) { continue; }
                    method.Body.KeepOldMaxStack = true;
                    for (int i = 0; i < method.Body.Instructions.Count; i++)
                    {
                        Instruction inst = method.Body.Instructions[i];
                        if (inst.OpCode.Equals(OpCodes.Stsfld) &&
                            inst.Operand is FieldDef &&
                            inst.Operand as FieldDef == uactivate &&
                            method.Body.Instructions[i-1].IsLdcI4())
                        {
                            Console.Write(" ");
                            AddToLog("Patched ", false, ConsoleColor.White);
                            AddToLog("'IL_" + method.Body.Instructions[i - 1].Offset.ToString("X4") + "'", false, ConsoleColor.Cyan);
                            AddToLog(" at ", false, ConsoleColor.White);
                            AddToLog("'" + type.Namespace + "." + type.Name + "'", false, ConsoleColor.DarkCyan);
                            AddToLog("!", false, ConsoleColor.White);
                            Console.CursorLeft = 55;
                            AddToLog("false", false, ConsoleColor.Red);
                            AddToLog(" -> ", false, ConsoleColor.DarkRed);
                            AddToLog("true", true, ConsoleColor.Green);
                            method.Body.Instructions.Insert(i - 1, new Instruction(OpCodes.Ldc_I4_1));
                            method.Body.Instructions.RemoveAt(i);
                        }
                    }
                }
            }
        }

        static string PrintToWidth(char c, int pos, int offset = -1)
        {
            string result = "";
            for (int i = pos; i < Console.WindowWidth + offset; i++) { result += c; }
            return result;
        }

        static void ChangeForeColor(ConsoleColor ForeColor) { ChangeColor(ForeColor, Console.BackgroundColor); }
        static void ChangeBackColor(ConsoleColor BackColor) { ChangeColor(Console.ForegroundColor, BackColor); }
        public static void ChangeColor(ConsoleColor ForeColor, ConsoleColor BackColor) { Console.ForegroundColor = ForeColor; Console.BackgroundColor = BackColor; }

        static void AddToLog(string text, bool endLine = true, ConsoleColor ForeColor = ConsoleColor.Gray, ConsoleColor BackColor = ConsoleColor.Black)
        {
            ChangeColor(ForeColor, BackColor);
            if (endLine) { Console.WriteLine(text); }
            else { Console.Write(text); }
            Console.ResetColor();
        }

        static AppIdentity Identify(ModuleDefMD asm)
        {
            AppIdentity id = new AppIdentity();
            foreach (CustomAttribute ca in asm.Assembly.CustomAttributes)
            {
                foreach (CAArgument caa in ca.ConstructorArguments)
                {
                    switch (ca.Constructor.DeclaringType.FullName.Replace("System.Reflection.", null).ToLower())
                    {
                        case "assemblyfileversionattribute": id.Version = caa.Value.ToString(); break;
                        case "assemblytitleattribute":
                            switch (caa.Value.ToString().ToLower())
                            {
                                case "securely erase files or folders": case "Secure Delete": id.Type = TargetType.SecureDelete; break;
                                case "duplicate File Finder": id.Type = TargetType.DuplicateFileFinder; break;
                                case "secret disk": id.Type = TargetType.SecretDisk; break;
                                case "prevent restore": id.Type = TargetType.PreventRestore; break;
                                case "wipe": id.Type = TargetType.Wipe; break;
                            }
                            break;
                        case "assemblydescriptionattribute":
                            switch (caa.Value.ToString().ToLower())
                            {
                                case "creates virtual disk": id.Type = TargetType.SecretDisk; break;
                                case "find duplicate files to avoid mess": id.Type = TargetType.DuplicateFileFinder; break;
                                case "deletes selected files securely without chance for recovery": id.Type = TargetType.SecureDelete; break;
                                case "prevents recovery of already deleted files": id.Type = TargetType.PreventRestore; break;
                                case "deletes personal traces and garbage": id.Type = TargetType.Wipe; break;
                            }
                            break;
                    }
                }
            }
            return id;
        }

        public enum TargetType
        {
            DuplicateFileFinder,
            Wipe,
            SecureDelete,
            SecretDisk,
            PreventRestore
        }
    }
}
