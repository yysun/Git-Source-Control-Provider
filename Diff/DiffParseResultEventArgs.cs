namespace GitScc.Diff
{
    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.Text;

    public class DiffParseResultEventArgs : ParseResultEventArgs
    {
        private readonly List<HunkRangeInfo> _diff;

        public DiffParseResultEventArgs(ITextSnapshot snapshot, TimeSpan elapsedTime, List<HunkRangeInfo> diff)
            : base(snapshot, elapsedTime)
        {
            _diff = diff;
        }

        public List<HunkRangeInfo> Diff
        {
            get
            {
                return _diff;
            }
        }
    }
}
