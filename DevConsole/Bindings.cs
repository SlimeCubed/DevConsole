using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DevConsole
{
    using BindEvents;

    /// <summary>
    /// Controls bindings from keys to commands.
    /// </summary>
    public static class Bindings
    {
        private static readonly List<KeyValuePair<IBindEvent, List<string>>> bindings = new List<KeyValuePair<IBindEvent, List<string>>>();

        /// <summary>
        /// Schedules a command to run every time an <see cref="IBindEvent"/> activates.
        /// </summary>
        /// <param name="bindEvent">The event to bind to.</param>
        /// <param name="command">The command to run.</param>
        public static void Bind(IBindEvent bindEvent, string command)
        {
            if (bindEvent == null) throw new ArgumentException("Bind event may not be null!", nameof(bindEvent));
            if (string.IsNullOrEmpty(command)) return;

            int bindInd = SearchBinds(bindEvent);
            if(bindInd < 0)
            {
                bindings.Add(new KeyValuePair<IBindEvent, List<string>>(bindEvent, new List<string>()));
                bindInd = bindings.Count - 1;
            }
            bindings[bindInd].Value.Add(command);
        }

        /// <summary>
        /// Removes all instances of the given command bound to this <see cref="IBindEvent"/>.
        /// </summary>
        /// <param name="bindEvent">The event to unbind from.</param>
        /// <param name="command">The command to search for.</param>
        public static void Unbind(IBindEvent bindEvent, string command)
        {
            if (bindEvent == null) throw new ArgumentException("Bind event may not be null!", nameof(bindEvent));

            int bindInd = SearchBinds(bindEvent);
            if (bindInd >= 0)
            {
                bindings[bindInd].Value.RemoveAll(testCmd => testCmd == command);
                if (bindings[bindInd].Value.Count == 0)
                    bindings.RemoveAt(bindInd);
            }
        }

        /// <summary>
        /// Unbinds all commands bound to an <see cref="IBindEvent"/>.
        /// </summary>
        /// <param name="bindEvent">The event to clear all binds from.</param>
        public static void UnbindAll(IBindEvent bindEvent)
        {
            if (bindEvent == null) throw new ArgumentException("Bind event may not be null!", nameof(bindEvent));

            int bindInd = SearchBinds(bindEvent);
            if (bindInd >= 0)
                bindings.RemoveAt(bindInd);
        }

        /// <summary>
        /// Removes all bindings.
        /// </summary>
        public static void UnbindAll()
        {
            bindings.Clear();
        }

        /// <summary>
        /// Retrieve an array of all commands bound to this event.
        /// </summary>
        /// <param name="bindEvent">The event to get binds from.</param>
        /// <returns>An array of all commands attached to this <see cref="IBindEvent"/>.</returns>
        public static string[] GetBoundCommands(IBindEvent bindEvent)
        {
            if (bindEvent == null) throw new ArgumentException("Bind event may not be null!", nameof(bindEvent));

            int bindInd = SearchBinds(bindEvent);
            if (bindInd >= 0)
                return bindings[bindInd].Value.ToArray();
            else
                return new string[0];
        }

        private static Queue<string> commandQueue = new Queue<string>();
        internal static void Run()
        {
            // Queue up all commands to run
            // It's valid to bind a command to unbind another, which would mess stuff up
            foreach (var pair in bindings)
            {
                if (pair.Key.Activate())
                {
                    foreach (var cmd in pair.Value)
                    {
                        commandQueue.Enqueue(cmd);
                    }
                }
            }

            // Run 'em
            while (commandQueue.Count > 0)
                GameConsole.RunCommand(commandQueue.Dequeue());
        }

        private static int SearchBinds(IBindEvent bindEvent)
        {
            for (int i = 0; i < bindings.Count; i++)
                if (bindEvent.BindsEqual(bindings[i].Key)) return i;
            return -1;
        }
    }
}
