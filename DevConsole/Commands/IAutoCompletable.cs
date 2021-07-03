using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DevConsole.Commands
{
    /// <summary>
    /// Defines how a command should be autocompleted.
    /// </summary>
    public interface IAutoCompletable
    {
        /// <summary>
        /// Gets all options for the next argument in the command, returning null if the given arguments aren't valid.
        /// </summary>
        /// <param name="currentArgs">An array of all arguments that have been completed.</param>
        /// <returns>An array of all possible arguments that may follow or null if this command doesn't match.</returns>
        IEnumerable<string> GetArgOptions(string[] currentArgs);
    }
}
