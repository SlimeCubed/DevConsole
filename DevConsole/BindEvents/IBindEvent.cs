namespace DevConsole.BindEvents
{
    /// <summary>
    /// Polls an event to determine whether a bind will activate this frame.
    /// </summary>
    public interface IBindEvent
    {
        /// <summary>
        /// Determines whether the bind should activate this frame.
        /// </summary>
        /// <returns><c>true</c> to execute the bind, <c>false</c> otherwise.</returns>
        bool Activate();

        /// <summary>
        /// Checks if two bind events are equal.
        /// </summary>
        /// <param name="otherBind">The <see cref="IBindEvent"/> to compare to.</param>
        /// <returns><c>true</c> if the bind events are the same, <c>false</c> otherwise.</returns>
        bool BindsEqual(IBindEvent otherBind);
    }
}
