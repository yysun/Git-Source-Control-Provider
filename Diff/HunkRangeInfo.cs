namespace GitScc.Diff
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.Text;
    using NGit.Diff;

    public class HunkRangeInfo
    {
        private readonly ITextSnapshot _snapshot;
        private readonly Edit _edit;
        private readonly List<string> _originalText;

        public HunkRangeInfo(ITextSnapshot snapshot, Edit edit, RawText originalText, RawText workingText)
        {
            if (snapshot == null)
                throw new ArgumentNullException("snapshot");
            if (edit == null)
                throw new ArgumentNullException("edit");
            if (originalText == null)
                throw new ArgumentNullException("originalText");
            if (workingText == null)
                throw new ArgumentNullException("workingText");

            _snapshot = snapshot;
            _edit = edit;
            _originalText = originalText.GetString(edit.GetBeginA(), edit.GetEndA(), true).Split('\n').Select(i => i.TrimEnd('\r')).ToList();
        }

        public ITextSnapshot Snapshot
        {
            get
            {
                return _snapshot;
            }
        }

        public HunkRange OriginalHunkRange
        {
            get
            {
                return new HunkRange(_edit.GetBeginA(), _edit.GetLengthA());
            }
        }

        public HunkRange NewHunkRange
        {
            get
            {
                return new HunkRange(_edit.GetBeginB(), _edit.GetLengthB());
            }
        }

        public List<string> OriginalText
        {
            get
            {
                return _originalText;
            }
        }

        public bool IsAddition
        {
            get
            {
                return _edit.GetType() == Edit.Type.INSERT;
            }
        }

        public bool IsModification
        {
            get
            {
                return _edit.GetType() == Edit.Type.REPLACE;
            }
        }

        public bool IsDeletion
        {
            get
            {
                return _edit.GetType() == Edit.Type.DELETE;
            }
        }
    }
}
