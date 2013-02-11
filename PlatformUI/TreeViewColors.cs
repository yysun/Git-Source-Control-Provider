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
                return GetResourceKey("SelectedItemActiveBrushKey", VsBrushes.ActiveCaptionKey);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static object SelectedItemActiveTextBrushKey
        {
            get
            {
                return GetResourceKey("SelectedItemActiveTextBrushKey", VsBrushes.CaptionTextKey);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static object SelectedItemInactiveBrushKey
        {
            get
            {
                return GetResourceKey("SelectedItemInactiveBrushKey", VsBrushes.InactiveCaptionKey);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static object SelectedItemInactiveTextBrushKey
        {
            get
            {
                return GetResourceKey("SelectedItemInactiveTextBrushKey", VsBrushes.InactiveCaptionTextKey);
            }
        }

        private static object GetResourceKey(string resourceName, object defaultKey)
        {
            return PlatformColorHelper.GetResourceKey(typeof(TreeViewColors), resourceName) ?? defaultKey;
        }
    }
}
