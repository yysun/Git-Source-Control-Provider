/**************************************************************************\
    Copyright Microsoft Corporation. All Rights Reserved.
\**************************************************************************/

namespace Microsoft.Windows.Shell
{
    public abstract class JumpItem
    {
        // This class is just provided to strongly type the JumpList's contents.
        // It's not externally extendable.
        internal JumpItem()
        {
        }

        public string CustomCategory { get; set; }
    }
}
