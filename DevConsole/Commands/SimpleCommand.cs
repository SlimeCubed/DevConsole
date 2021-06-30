using System;
using System.Linq;

namespace DevConsole.Commands
{
    /// <summary>
    /// A command that starts with a constant command name.
    /// </summary>
    public class SimpleCommand : ICommandHandler
    {
        /// <summary>
        /// The name of the command.
        /// </summary>
        public string CommandName { get; private set; }

        /// <summary>
        /// A short summary of the command's syntax, including the name and arguments.
        /// By default, this is the same as <see cref="CommandName" />.
        /// </summary>
        public string Summary { get; set; }

        private Action<string[]> handler;

        /// <summary>
        /// Creates a new simple command to be registered with <see cref="GameConsole.RegisterCommand(ICommandHandler)"/>.
        /// </summary>
        /// <param name="commandName">The first word the user must type into the console to run this command.</param>
        /// <param name="handler">A delegate called when the user runs this command.</param>
        public SimpleCommand(string commandName, Action<string[]> handler)
        {
            CommandName = commandName;
            Summary = CommandName;
            this.handler = handler;
        }

        /// <inheritdoc/>
        public bool RunCommand(string[] args, string rawCommand)
        {
            if (args[0] == CommandName)
            {
                handler(args.Skip(1).ToArray());
                return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public string Help() => Summary;
    }
}
