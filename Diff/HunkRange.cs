namespace GitScc.Diff
{
    public struct HunkRange
    {
        private readonly int _startingLineNumber;
        private readonly int _numberOfLines;

        public HunkRange(int startingLineNumber, int numberOfLines)
        {
            _startingLineNumber = startingLineNumber;
            _numberOfLines = numberOfLines;
        }

        public int StartingLineNumber
        {
            get
            {
                return _startingLineNumber;
            }
        }

        public int NumberOfLines
        {
            get
            {
                return _numberOfLines;
            }
        }
    }
}
