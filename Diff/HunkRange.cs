namespace GitScc.Diff
{
    public class HunkRange
    {
        public HunkRange(int startingLineNumber, int numberOfLines)
        {
            this.StartingLineNumber = startingLineNumber;
            this.NumberOfLines = numberOfLines;
        }

        public long StartingLineNumber { get; private set; }
        public long NumberOfLines { get; private set; }
    }
}
