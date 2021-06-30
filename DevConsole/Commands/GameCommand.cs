using System;
using System.Linq;
using Object = UnityEngine.Object;

namespace DevConsole.Commands
{
    /// <summary>
    /// A simple command that may only be run when in game.
    /// </summary>
    public class GameCommand : ICommandHandler
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

        private RunGameCommandHandler handler;

        /// <summary>
        /// Creates a new in-game-only command to be registered with <see cref="GameConsole.RegisterCommand(ICommandHandler)"/>.
        /// </summary>
        /// <param name="commandName">The first word the user must type into the console to run this command.</param>
        /// <param name="handler">A delegate called when the user runs this command.</param>
        public GameCommand(string commandName, RunGameCommandHandler handler)
        {
            CommandName = commandName;
            Summary = commandName;
            this.handler = handler;
        }

        /// <inheritdoc/>
        public virtual bool RunCommand(string[] args, string rawCommand)
        {
            if (args[0] == CommandName)
            {
                var game = Object.FindObjectOfType<RainWorld>()?.processManager?.currentMainLoop as RainWorldGame;
                if (game == null) return false;
                handler(game, args.Skip(1).ToArray());
                return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public string Help() => Summary;

        /// <summary>
        /// Runs an in-game command.
        /// </summary>
        /// <param name="game">The current <see cref="RainWorldGame"/> instance.</param>
        /// <param name="args">The arguments passed to the command excluding the command name.</param>
        public delegate void RunGameCommandHandler(RainWorldGame game, string[] args);
    }
}
