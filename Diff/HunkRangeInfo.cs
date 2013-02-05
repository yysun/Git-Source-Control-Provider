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
        private readonly bool _canRollback;

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
            _canRollback = true;
        }

        private HunkRangeInfo(ITextSnapshot snapshot, Edit edit, List<string> originalText)
        {
            _snapshot = snapshot;
            _edit = edit;
            _originalText = originalText;
            _canRollback = false;
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

        public bool CanRollback
        {
            get
            {
                if (!_canRollback)
                    return false;

                return IsAddition || IsModification || IsDeletion;
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

        public HunkRangeInfo TranslateTo(ITextSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException("snapshot");

            if (snapshot == _snapshot)
                return this;

            if (IsDeletion)
            {
                // track a point
                ITextSnapshotLine line = _snapshot.GetLineFromLineNumber(_edit.GetBeginB());
                ITrackingPoint trackingPoint = _snapshot.CreateTrackingPoint(line.Start, PointTrackingMode.Negative);

                SnapshotPoint updated = trackingPoint.GetPoint(snapshot);
                int updatedLineNumber = updated.GetContainingLine().LineNumber;
                Edit updatedEdit = new Edit(_edit.GetBeginA(), _edit.GetEndA(), updatedLineNumber, updatedLineNumber);
                return new HunkRangeInfo(snapshot, updatedEdit, _originalText);
            }
            else
            {
                // track a span
                ITextSnapshotLine startLine = _snapshot.GetLineFromLineNumber(_edit.GetBeginB());
                ITextSnapshotLine endLine = _snapshot.GetLineFromLineNumber(_edit.GetEndB() - 1);
                ITrackingSpan trackingSpan = _snapshot.CreateTrackingSpan(new SnapshotSpan(startLine.Start, endLine.EndIncludingLineBreak), SpanTrackingMode.EdgeInclusive);

                SnapshotSpan updated = trackingSpan.GetSpan(snapshot);
                int updatedStartLineNumber = updated.Start.GetContainingLine().LineNumber;
                int updatedEndLineNumber = updated.End.GetContainingLine().LineNumber;
                Edit updatedEdit = new Edit(_edit.GetBeginA(), _edit.GetEndA(), updatedStartLineNumber, updatedEndLineNumber);
                return new HunkRangeInfo(snapshot, updatedEdit, _originalText);
            }
        }
    }
}
