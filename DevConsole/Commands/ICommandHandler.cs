namespace DevConsole.Commands
{
    /// <summary>
    /// Defines a command that may be run through the <see cref="GameConsole"/>.
    /// </summary>
    public interface ICommandHandler
    {
        /// <summary>
        /// Checks command syntax and executes the command.
        /// </summary>
        /// <param name="args">All user-input command line arguments, including the command name.</param>
        /// <param name="rawCommand">The raw string entered by the user into the console.</param>
        /// <returns><c>true</c> if the command syntax matches, <c>false</c> otherwise.
        /// This should return <c>true</c> even if the command errors.</returns>
        bool RunCommand(string[] args, string rawCommand);

        /// <summary>
        /// Generates a short description of the command's syntax.
        /// </summary>
        /// <returns>A short description of the command's syntax, including its name and arguments.</returns>
        string Help();
    }
}
