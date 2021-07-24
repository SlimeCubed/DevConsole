using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using MonoMod.RuntimeDetour;

namespace DevConsole
{
    using BindEvents;

    /// <summary>
    /// Controls bindings from keys to commands.
    /// </summary>
    public static class Bindings
    {
        private static readonly List<BindingInfo> bindings = new();
        private static IDisposable runUpdateBindsHook;
        private static bool updateBindsRecursionLock;

        /// <summary>
        /// Schedules a command to run every time an <see cref="IBindEvent"/> activates.
        /// </summary>
        /// <param name="bindEvent">The event to bind to.</param>
        /// <param name="command">The command to run.</param>
        /// <param name="syncWithUpdate">If true, then this command will only run immediately before fixed-timestep updates.</param>
        public static void Bind(IBindEvent bindEvent, string command, bool syncWithUpdate)
        {
            if (bindEvent == null) throw new ArgumentException("Bind event may not be null!", nameof(bindEvent));
            if (string.IsNullOrEmpty(command)) return;

            if (syncWithUpdate && runUpdateBindsHook == null)
            {
                runUpdateBindsHook = new HookUtils.DeepHook(
                    typeof(MainLoopProcess).GetMethod(nameof(MainLoopProcess.Update)),
                    new Action<Action<object>, object>(MainLoopProcess_Update).Method
                );
            }

            var newBind = new BindingInfo(bindEvent, syncWithUpdate);
            int bindInd = SearchBinds(newBind);
            if (bindInd < 0)
            {
                bindings.Add(newBind);
                bindInd = bindings.Count - 1;
            }
            bindings[bindInd].commands.Add(command);
        }

        private static void MainLoopProcess_Update(Action<object> orig, object self)
        {
            if (updateBindsRecursionLock)
            {
                orig(self);
                return;
            }

            if (self is MainLoopProcess mlp && mlp.processActive && mlp.manager.currentMainLoop == mlp)
                RunUpdate();

            updateBindsRecursionLock = true;
            try { orig(self); }
            finally { updateBindsRecursionLock = false; }
        }

        /// <summary>
        /// Removes all instances of the given command bound to this <see cref="IBindEvent"/>.
        /// </summary>
        /// <param name="bindEvent">The event to unbind from.</param>
        /// <param name="command">The command to search for.</param>
        /// <param name="syncWithUpdate">True if the command runs each fixed-step update, false if it runs each frame.</param>
        public static void Unbind(IBindEvent bindEvent, string command, bool syncWithUpdate)
        {
            if (bindEvent == null) throw new ArgumentNullException("Bind event may not be null!", nameof(bindEvent));
            if (command == null) throw new ArgumentNullException("Command may not be null!", nameof(command));

            int bindInd = SearchBinds(new BindingInfo(bindEvent, syncWithUpdate));
            if (bindInd >= 0)
            {
                bindings[bindInd].commands.RemoveAll(testCmd => testCmd == command);
                if (bindings[bindInd].commands.Count == 0)
                    bindings.RemoveAt(bindInd);
            }

            DisposeHooks();
        }

        /// <summary>
        /// Unbinds all commands bound to an <see cref="IBindEvent"/>.
        /// </summary>
        /// <param name="bindEvent">The event to clear all binds from.</param>
        /// <param name="syncWithUpdate">True if the command runs each fixed-step update, false if it runs each frame.</param>
        public static void UnbindAll(IBindEvent bindEvent, bool syncWithUpdate)
        {
            if (bindEvent == null) throw new ArgumentException("Bind event may not be null!", nameof(bindEvent));

            int bindInd = SearchBinds(new BindingInfo(bindEvent, syncWithUpdate));
            if (bindInd >= 0) bindings.RemoveAt(bindInd);

            DisposeHooks();
        }

        /// <summary>
        /// Removes all bindings.
        /// </summary>
        public static void UnbindAll()
        {
            bindings.Clear();

            DisposeHooks();
        }

        /// <summary>
        /// Retrieve an array of all commands bound to this event.
        /// </summary>
        /// <param name="bindEvent">The event to get binds from.</param>
        /// <param name="syncWithUpdate">True if the command runs each fixed-step update, false if it runs each frame.</param>
        /// <returns>An array of all commands attached to this <see cref="IBindEvent"/>.</returns>
        public static string[] GetBoundCommands(IBindEvent bindEvent, bool syncWithUpdate)
        {
            if (bindEvent == null) throw new ArgumentException("Bind event may not be null!", nameof(bindEvent));

            int bindInd = SearchBinds(new BindingInfo(bindEvent, syncWithUpdate));
            if (bindInd >= 0)
                return bindings[bindInd].commands.ToArray();
            else
                return new string[0];
        }

        private static readonly Queue<string> commandQueue = new();
        internal static void RunFrame()
        {
            // Queue up all commands to run
            // It's valid to bind a command to unbind another, which would mess stuff up
            foreach (var bind in bindings)
            {
                if (bind.bindEvent.Activate())
                {
                    if (bind.syncWithUpdate)
                    {
                        bind.fireOnUpdate = true;
                    }
                    else
                    {
                        foreach (var cmd in bind.commands)
                            commandQueue.Enqueue(cmd);
                    }
                }
            }

            // Run 'em
            while (commandQueue.Count > 0)
                GameConsole.RunCommand(commandQueue.Dequeue());
        }

        internal static void RunUpdate()
        {
            // Queue up all commands to run
            // It's valid to bind a command to unbind another, which would mess stuff up
            foreach (var bind in bindings)
            {
                if (bind.syncWithUpdate && (bind.fireOnUpdate || bind.bindEvent.Activate()))
                {
                    bind.fireOnUpdate = false;
                    foreach (var cmd in bind.commands)
                        commandQueue.Enqueue(cmd);
                }
            }

            // Run 'em
            while (commandQueue.Count > 0)
                GameConsole.RunCommand(commandQueue.Dequeue());
        }

        private static int SearchBinds(BindingInfo bindInfo)
        {
            for (int i = 0; i < bindings.Count; i++)
                if (bindInfo.Equals(bindings[i])) return i;
            return -1;
        }

        private static void DisposeHooks()
        {
            if (runUpdateBindsHook != null && bindings.All(bind => !bind.syncWithUpdate))
            {
                runUpdateBindsHook.Dispose();
                runUpdateBindsHook = null;
            }
        }

        // Contains information about a key binding
        private class BindingInfo
        {
            public readonly IBindEvent bindEvent;
            public readonly bool syncWithUpdate;

            public readonly List<string> commands = new();
            public bool fireOnUpdate;

            public BindingInfo(IBindEvent bindEvent, bool syncWithUpdate)
            {
                this.bindEvent = bindEvent;
                this.syncWithUpdate = syncWithUpdate;
            }

            public override bool Equals(object obj)
            {
                if (obj is not BindingInfo other) return false;

                return bindEvent.BindsEqual(other.bindEvent)
                    && syncWithUpdate == other.syncWithUpdate;
            }

            public override int GetHashCode()
            {
                int hashCode = -970748325;
                hashCode = hashCode * -1521134295 + bindEvent.GetHashCode();
                hashCode = hashCode * -1521134295 + syncWithUpdate.GetHashCode();
                return hashCode;
            }
        }
    }
}
