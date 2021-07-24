using UnityEngine;

namespace DevConsole.BindEvents
{
    /// <summary>
    /// Checks if a key code is pressed each frame.
    /// </summary>
    public class KeyCodeEvent : IBindEvent
    {
        /// <summary>
        /// The <see cref="KeyCode"/> to check for.
        /// </summary>
        public KeyCode Key { get; }

        /// <summary>
        /// The specific time to activate during the key press.
        /// </summary>
        public KeyMode Mode { get; }

        /// <summary>
        /// Creates a new event.
        /// </summary>
        /// <param name="keyCode">The <see cref="KeyCode"/> to check for.</param>
        /// <param name="mode">Determines when to trigger the event during each keypress.</param>
        public KeyCodeEvent(KeyCode keyCode, KeyMode mode = KeyMode.Down)
        {
            Key = keyCode;
            Mode = mode;
        }

        /// <inheritdoc/>
        public bool Activate()
        {
            switch(Mode)
            {
                default:
                case KeyMode.Down: return Input.GetKeyDown(Key);
                case KeyMode.HoldDown: return Input.GetKey(Key);
                case KeyMode.Up: return Input.GetKeyUp(Key);
                case KeyMode.HoldUp: return !Input.GetKey(Key);
            }
        }

        /// <inheritdoc/>
        public bool BindsEqual(IBindEvent otherBind)
        {
            if (otherBind is KeyCodeEvent other)
                return Key == other.Key
                    && Mode == other.Mode;
            return false;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Key} {Mode}";
        }
    }
}
