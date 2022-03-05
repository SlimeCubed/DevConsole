using System;
using System.Reflection;
using System.Reflection.Emit;
using OptionalUI;
using Partiality.Modloader;

namespace DevConsole.Config
{
    internal class ConfigGenerator
    {
        private static Type oiType;

        public static object LoadOI(DevConsoleMod mod)
        {
            // Don't do any exception handling - ConfigMachine will do a better job
            if (oiType == null) oiType = CreateOIType();
            return Activator.CreateInstance(oiType, mod);
        }

        public static Type CreateOIType()
        {
            // Define a new type inheriting from OptionInterface
            AssemblyName name = new AssemblyName($"{nameof(DevConsoleMod)}Config");
            AssemblyBuilder asm = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
            ModuleBuilder mod = asm.DefineDynamicModule(name.Name);
            TypeBuilder oi = mod.DefineType(name.Name, TypeAttributes.Public | TypeAttributes.Class, typeof(OptionInterface));

            // DevConsoleModConfig..ctor(DevConsoleMod mod)
            {
                var m = oi.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, new Type[] { typeof(DevConsoleMod) });
                var ilg = m.GetILGenerator();

                // base..ctor(this, mod);
                ilg.Emit(OpCodes.Ldarg_0);
                ilg.Emit(OpCodes.Ldarg_1);
                ilg.Emit(OpCodes.Call, typeof(OptionInterface).GetConstructor(new Type[] { typeof(PartialityMod) }));

                // return;
                ilg.Emit(OpCodes.Ret);
            }

            // void DevConsoleModConfig.Initialize()
            {
                var m = oi.DefineMethod(nameof(OptionInterface.Initialize), MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual, typeof(void), Type.EmptyTypes);
                var ilg = m.GetILGenerator();
                
                // base.Initialize();
                ilg.Emit(OpCodes.Ldarg_0);
                ilg.Emit(OpCodes.Call, typeof(OptionInterface).GetMethod(nameof(OptionInterface.Initialize), Type.EmptyTypes));

                // ConsoleConfig.Initialize(this);
                ilg.Emit(OpCodes.Ldarg_0);
                ilg.Emit(OpCodes.Call, typeof(ConsoleConfig).GetMethod(nameof(ConsoleConfig.Initialize)));

                // return;
                ilg.Emit(OpCodes.Ret);
            }

            // void DevConsoleModConfig.ConfigOnChange()
            {
                var m = oi.DefineMethod(nameof(OptionInterface.ConfigOnChange), MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual, typeof(void), Type.EmptyTypes);
                var ilg = m.GetILGenerator();

                // base.ConfigOnChange();
                ilg.Emit(OpCodes.Ldarg_0);
                ilg.Emit(OpCodes.Call, typeof(OptionInterface).GetMethod(nameof(OptionInterface.ConfigOnChange), Type.EmptyTypes));

                // ConsoleConfig.ConfigOnChange(this);
                ilg.Emit(OpCodes.Ldarg_0);
                ilg.Emit(OpCodes.Call, typeof(ConsoleConfig).GetMethod(nameof(ConsoleConfig.ConfigOnChange)));

                // return;
                ilg.Emit(OpCodes.Ret);
            }

            return oi.CreateType();
        }
    }
}
