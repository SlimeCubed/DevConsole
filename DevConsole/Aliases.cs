using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DevConsole
{
    /// <summary>
    /// Allows complex commands to be renamed into simple aliases.
    /// </summary>
    public static class Aliases
    {
        private static Dictionary<string, string[]> aliases = new Dictionary<string, string[]>();

        /// <summary>
        /// Creates or overwrites a command that executes the given commands.
        /// </summary>
        /// <param name="name">The name of the new command.</param>
        /// <param name="commands">The list of commands to run.</param>
        public static void SetAlias(string name, string[] commands)
        {
            aliases[name] = (string[])commands.Clone();
        }

        /// <summary>
        /// Removes an existing alias. This only works for aliases, not normal commands.
        /// </summary>
        /// <param name="name">The name of the aliased command to remove.</param>
        public static void RemoveAlias(string name)
        {
            aliases.Remove(name);
        }

        /// <summary>
        /// Retrieves a list of commands associated with an alias.
        /// </summary>
        /// <param name="name">The name of the aliased command.</param>
        /// <returns>An array of commands to be executed or <c>null</c> if no such alias exists.</returns>
        public static string[] GetAlias(string name)
        {
            return aliases.TryGetValue(name, out string[] cmds) ? cmds : null;
        }

        /// <summary>
        /// Executes the commands associated with an alias.
        /// </summary>
        /// <param name="name">The name of the aliased command.</param>
        /// <returns><c>true</c> if there was an alias to run, <c>false</c> otherwise.</returns>
        public static bool RunAlias(string[] args)
        {
            if (args.Length == 0) return false;
            var commands = GetAlias(args[0]);
            if (commands == null) return false;
            foreach (var cmd in commands)
                GameConsole.RunCommand(cmd);
            return true;
        }
    }
}
