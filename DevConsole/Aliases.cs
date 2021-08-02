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
        private static readonly Dictionary<string, string> aliases = new Dictionary<string, string>();

        /// <summary>
        /// Creates or overwrites a command that executes the given commands.
        /// </summary>
        /// <param name="name">The name of the new command.</param>
        /// <param name="command">The list of commands to run.</param>
        public static void SetAlias(string name, string command)
        {
            aliases[name] = command;
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
        public static string GetAlias(string name)
        {
            return aliases.TryGetValue(name, out string cmds) ? cmds : null;
        }

        /// <summary>
        /// Executes the commands associated with an alias.
        /// </summary>
        /// <param name="args">The list of arguments input to the command line.</param>
        /// <returns><c>true</c> if there was an alias to run, <c>false</c> otherwise.</returns>
        public static bool RunAlias(string[] args)
        {
            if (args.Length == 0) return false;
            var command = GetAlias(args[0]);
            if (command == null) return false;
            GameConsole.RunCommand(command);
            return true;
        }

        /// <summary>
        /// Gets all aliases.
        /// </summary>
        /// <returns>A list of all registered aliases.</returns>
        public static IEnumerable<string> GetAliases() => aliases.Keys;
    }
}
