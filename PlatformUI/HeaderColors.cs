namespace GitScc.PlatformUI
{
    using System.Diagnostics;
    using Microsoft.VisualStudio.Shell;

    public static class HeaderColors
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static object SeparatorLineBrushKey
        {
            get
            {
                return GetResourceKey("SeparatorLineBrushKey", VsBrushes.CommandBarMenuSeparatorKey);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static object MouseOverBrushKey
        {
            get
            {
                return GetResourceKey("MouseOverBrushKey", VsBrushes.CommandBarMouseOverBackgroundGradientKey);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static object MouseOverTextBrushKey
        {
            get
            {
                return GetResourceKey("MouseOverTextBrushKey", VsBrushes.ButtonTextKey);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static object MouseDownBrushKey
        {
            get
            {
                return GetResourceKey("MouseDownBrushKey", VsBrushes.CommandBarMouseDownBackgroundGradientKey);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static object MouseDownTextBrushKey
        {
            get
            {
                return GetResourceKey("MouseDownTextBrushKey", VsBrushes.ButtonTextKey);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static object GlyphBrushKey
        {
            get
            {
                return GetResourceKey("GlyphBrushKey", VsBrushes.ButtonTextKey);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static object MouseOverGlyphBrushKey
        {
            get
            {
                return GetResourceKey("MouseOverGlyphBrushKey", VsBrushes.ButtonTextKey);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static object MouseDownGlyphBrushKey
        {
            get
            {
                return GetResourceKey("MouseDownGlyphBrushKey", VsBrushes.ButtonTextKey);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static object DefaultBrushKey
        {
            get
            {
                return GetResourceKey("DefaultBrushKey", VsBrushes.CommandBarMenuBackgroundGradientKey);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static object DefaultTextBrushKey
        {
            get
            {
                return GetResourceKey("DefaultTextBrushKey", VsBrushes.ButtonTextKey);
            }
        }

        private static object GetResourceKey(string resourceName, object defaultKey)
        {
            return PlatformColorHelper.GetResourceKey(typeof(HeaderColors), resourceName) ?? defaultKey;
        }
    }
}
