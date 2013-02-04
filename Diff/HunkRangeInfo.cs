namespace GitScc.Diff
{
    using System.Collections.Generic;
    using System.Linq;
    using NGit.Diff;

    public class HunkRangeInfo
    {
        public HunkRangeInfo(Edit edit, RawText originalText, RawText workingText)
        {
            OriginaleHunkRange = new HunkRange(edit.GetBeginA(), edit.GetLengthA());
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

        public HunkRange OriginaleHunkRange { get; private set; }
        public HunkRange NewHunkRange { get; private set; }
        public List<string> OriginalText { get; private set; }

        public bool IsAddition { get; private set; }
        public bool IsModification { get; private set; }
        public bool IsDeletion { get; private set; }
    }
}
