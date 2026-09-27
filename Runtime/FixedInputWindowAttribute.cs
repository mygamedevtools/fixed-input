using System;
using UnityEngine;

namespace MyGameDevTools.FixedInput
{
    /// <summary>
    /// Declares how many steps past its set tick a field is allowed to stay armed before the
    /// diagnostics call it lost.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class FixedInputWindowAttribute : PropertyAttribute
    {
        /// <summary>The widest window any reader of this field is expected to pass.</summary>
        public uint Window { get; }

        public FixedInputWindowAttribute(uint window) => Window = window;
    }
}
