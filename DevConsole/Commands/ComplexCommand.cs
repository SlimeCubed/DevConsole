using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HelpHandler = System.Func<string>;
using RunCommandHandler = System.Func<string[], string, bool>;

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
    }
}
