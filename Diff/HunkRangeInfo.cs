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

            OriginalHunkRange = new HunkRange(edit.GetBeginA(), edit.GetLengthA());
            NewHunkRange = new HunkRange(edit.GetBeginB(), edit.GetLengthB());
            OriginalText = originalText.GetString(edit.GetBeginA(), edit.GetEndA(), true).Split('\n').Select(i => i.TrimEnd('\r')).ToList();

            switch (edit.GetType())
            {
            case Edit.Type.INSERT:
                IsAddition = true;
                break;

            case Edit.Type.DELETE:
                IsDeletion = true;
                break;

            case Edit.Type.REPLACE:
                IsModification = true;
                break;

            case Edit.Type.EMPTY:
            default:
                break;
            }
        }

        public ITextSnapshot Snapshot
        {
            get
            {
                return _snapshot;
            }
        }

        public HunkRange OriginalHunkRange { get; private set; }
        public HunkRange NewHunkRange { get; private set; }
        public List<string> OriginalText { get; private set; }

        public bool IsAddition { get; private set; }
        public bool IsModification { get; private set; }
        public bool IsDeletion { get; private set; }
    }
}
