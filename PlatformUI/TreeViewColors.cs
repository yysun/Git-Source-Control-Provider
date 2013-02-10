namespace GitScc.PlatformUI
{
    using System.Diagnostics;
    using Microsoft.VisualStudio.Shell;

    public static class TreeViewColors
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static object SelectedItemActiveBrushKey
        {
            get
            {
                return GetResourceKey("SelectedItemActiveBrushKey", VsBrushes.AccentBorderKey);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static object SelectedItemActiveTextBrushKey
        {
            get
            {
                return GetResourceKey("SelectedItemActiveTextBrushKey", VsBrushes.AccentBorderKey);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static object SelectedItemInactiveBrushKey
        {
            get
            {
                return GetResourceKey("SelectedItemInactiveBrushKey", VsBrushes.AccentBorderKey);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static object SelectedItemInactiveTextBrushKey
        {
            get
            {
                return GetResourceKey("SelectedItemInactiveTextBrushKey", VsBrushes.AccentBorderKey);
            }
        }

        private static object GetResourceKey(string resourceName, object defaultKey)
        {
            return PlatformColorHelper.GetResourceKey(typeof(TreeViewColors), resourceName) ?? defaultKey;
        }
    }
}
