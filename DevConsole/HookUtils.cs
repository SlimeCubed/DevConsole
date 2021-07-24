using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using MonoMod.RuntimeDetour;
using System.Diagnostics;

namespace DevConsole
{
    internal static class HookUtils
    {
        private static IEnumerable<Type> GetTypesSafe(this Assembly self)
        {
            try { return self.GetTypes(); }
            catch(ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }

        // A collection of hooks to all methods that override a base method
        // Methods may be skipped if they inherit from a type defined in a different assembly
        public class DeepHook : IDisposable
        {
            private List<Hook> hooks = new();

            public DeepHook(MethodInfo from, MethodInfo to)
            {
                var baseType = from.DeclaringType;
                var baseAsmName = baseType.Assembly.GetName().ToString();

                // Hooking MainLoopProcess.Update takes around 120ms, which can be a noticeable stutter
                //Stopwatch sw = new Stopwatch();

                //sw.Start();
                var overrides = AppDomain.CurrentDomain.GetAssemblies()
                     .Where(asm => asm.GetName().ToString() == baseAsmName || asm.GetReferencedAssemblies().Any(name => name.ToString() == baseAsmName))
                     .SelectMany(GetTypesSafe)
                     .Where(t => baseType.IsAssignableFrom(t))
                     .Select(t => t.GetMethod(
                         from.Name,
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.ExactBinding,
                         null,
                         from.CallingConvention,
                         from.GetParameters().Select(p => p.ParameterType).ToArray(),
                         null))
                     .Where(m => m != null).ToArray();
                //sw.Stop();
                //GameConsole.WriteLine($"Finding methods took {sw.ElapsedMilliseconds}ms");

                //sw.Reset();
                //sw.Start();
                foreach (var fromInst in overrides)
                {
                    hooks.Add(new Hook(fromInst, to));
                }
                //sw.Stop();
                //GameConsole.WriteLine($"Creating hooks took {sw.ElapsedMilliseconds}ms");
            }

            public void Dispose()
            {
                if (hooks != null)
                {
                    foreach (var hook in hooks)
                        hook.Dispose();
                    hooks = null;
                }
            }
        }
    }
}
