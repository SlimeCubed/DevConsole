using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HelpHandler = System.Func<string>;
using RunHandler = System.Action<string[]>;
using RunGameHandler = System.Action<RainWorldGame, string[]>;
using AutocompleteHandler = System.Func<string[], System.Collections.Generic.IEnumerable<string>>;

namespace DevConsole.Commands
{
    /// <summary>
    /// Allows commands to be constructed without inheritance.
    /// </summary>
    public class CommandBuilder
    {
        private BuiltCommand command;

        /// <summary>
        /// Creates a new command builder.
        /// </summary>
        public CommandBuilder()
        {
            command = new BuiltCommand();
        }

        /// <summary>
        /// Creates a new command builder and sets the command's name.
        /// </summary>
        /// <param name="name">The name of the command.</param>
        public CommandBuilder(string name) : this()
        {
            Name(name);
        }

        /// <summary>
        /// Sets the name of the command.
        /// </summary>
        /// <param name="name">The name of the command.</param>
        /// <returns>The <see cref="CommandBuilder"/> instance for chaining.</returns>
        public CommandBuilder Name(string name)
        {
            AssertCommand();
            if (command.name != null) throw new InvalidOperationException("Command may not have more than one name!");
            command.name = name;

            return this;
        }

        /// <summary>
        /// Sets a delegate to be called when the command runs.
        /// </summary>
        /// <param name="handler">The delegate to call. The command name is omitted from the arguments array.</param>
        /// <returns>The <see cref="CommandBuilder"/> instance for chaining.</returns>
        public CommandBuilder Run(RunHandler handler)
        {
            AssertCommand();
            if (command.runHandler != null) throw new InvalidOperationException("Command may not have multiple run handlers!");
            command.runHandler = handler;

            return this;
        }

        /// <summary>
        /// Sets a delegate to be called when the command runs while in-game, displaying a warning otherwise.
        /// </summary>
        /// <param name="handler">The delegate to call. The command name is omitted from the arguments array.</param>
        /// <returns>The <see cref="CommandBuilder"/> instance for chaining.</returns>
        public CommandBuilder RunGame(RunGameHandler handler)
        {
            AssertCommand();
            Run(args =>
            {
                var game = UnityEngine.Object.FindObjectOfType<RainWorld>()?.processManager?.currentMainLoop as RainWorldGame;
                if (game == null)
                    GameConsole.WriteLine("This command can only run while in-game!");
                else
                    handler(game, args);
            });

            return this;
        }

        /// <summary>
        /// Sets a delegate to be called when autocompletion is needed for the command.
        /// </summary>
        /// <param name="handler">A delegate that returns all available options for the argument when given all completed arguments before it.
        /// The command name is omitted.</param>
        /// <returns>The <see cref="CommandBuilder"/> instance for chaining.</returns>
        public CommandBuilder AutoComplete(AutocompleteHandler handler)
        {
            AssertCommand();
            if (command.autocompleteHandler != null) throw new InvalidOperationException("Command may not have multiple autocomplete handlers!");
            command.autocompleteHandler = handler;

            return this;
        }

        /// <summary>
        /// Sets an array of options to search when autocompletion is needed for the command.
        /// </summary>
        /// <param name="options">A list of all available options for each parameter. The command name is omitted.</param>
        /// <returns>The <see cref="CommandBuilder"/> instance for chaining.</returns>
        public CommandBuilder AutoComplete(string[][] options)
        {
            var command = this.command;

            return AutoComplete(args =>
            {
                if (args.Length < options.Length) return options[args.Length];
                return null;
            });
        }

        /// <summary>
        /// Sets a delegate to be called when the help text is gotten for the command.
        /// </summary>
        /// <param name="handler">A delegate that returns help text for the command, including command name and parameters.</param>
        /// <returns>The <see cref="CommandBuilder"/> instance for chaining.</returns>
        public CommandBuilder Help(HelpHandler handler)
        {
            AssertCommand();
            if (command.helpHandler != null) throw new InvalidOperationException("Command may not have multiple help handlers!");
            command.helpHandler = handler;

            return this;
        }

        /// <summary>
        /// Sets the help text for this command.
        /// </summary>
        /// <param name="summary">The help text for this command.</param>
        /// <returns>The <see cref="CommandBuilder"/> instance for chaining.</returns>
        public CommandBuilder Help(string summary)
        {
            return Help(() => summary);
        }

        /// <summary>
        /// Hides this command's summary when help is gotten.
        /// </summary>
        /// <returns>The <see cref="CommandBuilder"/> instance for chaining.</returns>
        public CommandBuilder HideHelp()
        {
            return Help(() => null);
        }

        /// <summary>
        /// Registers the command. The <see cref="CommandBuilder"/> instance may not be used afterwards.
        /// </summary>
        public void Register()
        {
            AssertCommand();
            if (command.runHandler == null) throw new InvalidOperationException("Command must have a run handler!");
            if (command.name == null) throw new InvalidOperationException("Command must have a name!");

            GameConsole.RegisterCommand(command);
            command = null;
        }

        private void AssertCommand()
        {
            if (command == null) throw new InvalidOperationException("Only one command may be made with the same builder!");
        }

        private class BuiltCommand : ICommandHandler, IAutoCompletable
        {
            public string name;
            public RunHandler runHandler;
            public AutocompleteHandler autocompleteHandler;
            public HelpHandler helpHandler;

            public string Help()
            {
                if (helpHandler != null)
                    return helpHandler.Invoke();
                else
                    return name;
            }

            public bool RunCommand(string[] args, string rawCommand)
            {
                if(args.Length > 0 && args[0] == name)
                {
                    runHandler(args.Skip(1).ToArray());
                    return true;
                }
                return false;
            }

            public IEnumerable<string> GetArgOptions(string[] currentArgs)
            {
                if (currentArgs.Length == 0) return new string[] { name };
                else if (currentArgs[0] == name) return autocompleteHandler?.Invoke(currentArgs.Skip(1).ToArray());
                else return null;
            }
        }
    }
}
