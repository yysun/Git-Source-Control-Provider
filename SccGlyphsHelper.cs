namespace GitScc
{
    using Color = System.Drawing.Color;
    using Image = System.Drawing.Image;
    using ImageList = System.Windows.Forms.ImageList;
    using Size = System.Drawing.Size;
    using VsStateIcon = Microsoft.VisualStudio.Shell.Interop.VsStateIcon;

    internal static class SccGlyphsHelper
    {
        // Remember the base index where our custom scc glyph start
        private static uint _customSccGlyphBaseIndex = 0;

        // Our custom image list
        private static ImageList _customSccGlyphsImageList;

        // Indexes of icons in our custom image list
        private enum CustomSccGlyphs2010
        {
            Untracked = 0,
            Staged = 1,
            Modified = 2,
            Tracked = 3,
        };

        // Indexes of icons in our custom image list
        private enum CustomSccGlyphs2012
        {
            New = 0,
            Staged = 1,
            Conflicted = 2,
            Merged = 3,
        };

        public static VsStateIcon Tracked
        {
            get
            {
                if (GitSccOptions.IsVisualStudio2010 && GitSccOptions.Current.UseTGitIconSet)
                    return (VsStateIcon)(_customSccGlyphBaseIndex + (uint)CustomSccGlyphs2010.Tracked);

                return VsStateIcon.STATEICON_CHECKEDIN;
            }
        }

        public static VsStateIcon Modified
        {
            get
            {
                if (GitSccOptions.IsVisualStudio2010 && GitSccOptions.Current.UseTGitIconSet)
                    return (VsStateIcon)(_customSccGlyphBaseIndex + (uint)CustomSccGlyphs2010.Modified);

                return VsStateIcon.STATEICON_CHECKEDOUT;
            }
        }

        public static VsStateIcon New
        {
            get
            {
                if (GitSccOptions.IsVisualStudio2010)
                    return (VsStateIcon)(_customSccGlyphBaseIndex + (uint)CustomSccGlyphs2010.Untracked);

                return (VsStateIcon)(_customSccGlyphBaseIndex + (uint)CustomSccGlyphs2012.New);
            }
        }

        public static VsStateIcon Added
        {
            get
            {
                return Staged;
            }
        }

        public static VsStateIcon Staged
        {
            get
            {
                if (GitSccOptions.IsVisualStudio2010)
                    return (VsStateIcon)(_customSccGlyphBaseIndex + (uint)CustomSccGlyphs2010.Staged);

                return (VsStateIcon)(_customSccGlyphBaseIndex + (uint)CustomSccGlyphs2012.Staged);
            }
        }

        public static VsStateIcon NotControlled
        {
            get
            {
                return VsStateIcon.STATEICON_NOSTATEICON;
            }
        }

        public static VsStateIcon Ignored
        {
            get
            {
                return VsStateIcon.STATEICON_EXCLUDEDFROMSCC;
            }
        }

        public static VsStateIcon Conflict
        {
            get
            {
                if (GitSccOptions.IsVisualStudio2010)
                    return VsStateIcon.STATEICON_DISABLED;

                return (VsStateIcon)(_customSccGlyphBaseIndex + (uint)CustomSccGlyphs2012.Conflicted);
            }
        }

        public static VsStateIcon Merged
        {
            get
            {
                if (GitSccOptions.IsVisualStudio2010)
                    return Modified;

                return (VsStateIcon)(_customSccGlyphBaseIndex + (uint)CustomSccGlyphs2012.Merged);
            }
        }

        public static VsStateIcon Default
        {
            get
            {
                return VsStateIcon.STATEICON_NOSTATEICON;
            }
        }

        public static uint GetCustomGlyphList(uint baseIndex)
        {
            // If this is the first time we got called, construct the image list, remember the index, etc
            if (_customSccGlyphsImageList == null)
            {
                // The shell calls this function when the provider becomes active to get our custom glyphs
                // and to tell us what's the first index we can use for our glyphs
                // Remember the index in the scc glyphs (VsStateIcon) where our custom glyphs will start
                _customSccGlyphBaseIndex = baseIndex;

                // Create a new imagelist
                _customSccGlyphsImageList = new ImageList();

                // Set the transparent color for the imagelist (the SccGlyphs.bmp uses magenta for background)
                _customSccGlyphsImageList.TransparentColor = Color.FromArgb(255, 0, 255);

                // Set the corret imagelist size (7x16 pixels, otherwise the system will either stretch the image or fill in with black blocks)
                _customSccGlyphsImageList.ImageSize = new Size(7, 16);

                // Add the custom scc glyphs we support to the list
                // NOTE: VS2005 and VS2008 are limited to 4 custom scc glyphs (let's hope this will change in future versions)
                Image sccGlyphs = GitSccOptions.IsVisualStudio2010 ? Resources.SccGlyphs : Resources.SccGlyphs2012;
                _customSccGlyphsImageList.Images.AddStrip(sccGlyphs);
            }

            // Return a Win32 HIMAGELIST handle to our imagelist to the shell (by keeping the ImageList a member of the class we guarantee the Win32 object is still valid when the shell needs it)
            return (uint)_customSccGlyphsImageList.Handle;

        }
    }
}
