using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DevConsole.BindEvents
{
    /// <summary>
    /// Specifies a certain part of a keypress.
    /// </summary>
    public enum KeyMode
    {
        /// <summary>
        /// Triggers when the key is first pressed down.
        /// </summary>
        Down,
        /// <summary>
        /// Triggers each frame that the key is held down.
        /// </summary>
        HoldDown,
        /// <summary>
        /// Triggers when the key is first released.
        /// </summary>
        Up,
        /// <summary>
        /// Triggers each frame that the key is not pressed.
        /// </summary>
        HoldUp
    }
}
