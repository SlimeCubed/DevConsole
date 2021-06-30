using UnityEngine;

namespace DevConsole.BindEvents
{
    /// <summary>
    /// Checks if a key string is pressed each frame.
    /// </summary>
    public class KeyNameEvent : IBindEvent
    {
        /// <summary>
        /// The key name to check for.
        /// </summary>
        public string KeyName { get; }

        /// <summary>
        /// The specific time to activate during the key press.
        /// </summary>
        public KeyMode Mode { get; }

        /// <summary>
        /// Creates a new event.
        /// </summary>
        /// <param name="keyName">The key name to check for.</param>
        public KeyNameEvent(string keyName, KeyMode mode = KeyMode.Down)
        {
            KeyName = keyName;
            Mode = mode;
        }

        /// <inheritdoc/>
        public bool Activate()
        {
            switch (Mode)
            {
                default:
                case KeyMode.Down: return Input.GetKeyDown(KeyName);
                case KeyMode.HoldDown: return Input.GetKey(KeyName);
                case KeyMode.Up: return Input.GetKeyUp(KeyName);
                case KeyMode.HoldUp: return !Input.GetKey(KeyName);
            }
        }

        /// <inheritdoc/>
        public bool BindsEqual(IBindEvent otherBind)
        {
            if (otherBind is KeyNameEvent other)
                return KeyName == other.KeyName
                    && Mode == other.Mode;
            return false;
        }
    }
}
