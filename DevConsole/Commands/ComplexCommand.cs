using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DevConsole.Commands
{
    /// <summary>
    /// A command constructed from a delegate that runs the command and a delegate that gets help text.
    /// </summary>
    /// <remarks>
    /// This may be used instead of implementing the <see cref="ICommandHandler"/> interface so that
    /// the mod's types may be loaded without loading DevConsole.
    /// </remarks>
    public class ComplexCommand : ICommandHandler
    {
        private readonly RunCommandHandler run;
        private readonly HelpHandler help;

        /// <summary>
        /// Creates a new complex command to be registered with <see cref="GameConsole.RegisterCommand(ICommandHandler)"/>.
        /// </summary>
        /// <param name="run">A delegate to handle running this command.</param>
        /// <param name="help">A delegate to produce help text for this command.</param>
        public ComplexCommand(RunCommandHandler run, HelpHandler help)
        {
            this.run = run;
            this.help = help;
        }

        /// <inheritdoc/>
        public string Help() => help?.Invoke();

        /// <inheritdoc/>
        public bool RunCommand(string[] args, string rawCommand) => run?.Invoke(args, rawCommand) ?? false;

        /// <summary>
        /// Gets a short summary of the command's syntax, including the name and arguments.
        /// </summary>
        /// <returns>A string containing a short summary of the command's syntax, including the name and arguments.</returns>
        public delegate string HelpHandler();

        /// <summary>
        /// Runs a command or returns false if the arguments do not match the command's syntax.
        /// </summary>
        /// <param name="args">A list of arguments parsed from <paramref name="rawCommand" />. </param>
        /// <param name="rawCommand">The raw string entered by the user into the console.</param>
        /// <returns><c>true</c> if the command syntax matches, <c>false</c> otherwise.
        /// This should return <c>true</c> even if the command errors.</returns>
        public delegate bool RunCommandHandler(string[] args, string rawCommand);
    }
}
