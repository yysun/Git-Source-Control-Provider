namespace GitScc.Diff
{
    using System.Collections.Generic;
    using ITextDocument = Microsoft.VisualStudio.Text.ITextDocument;
    using ITextSnapshot = Microsoft.VisualStudio.Text.ITextSnapshot;

    public interface IGitCommands
    {
        IEnumerable<HunkRangeInfo> GetGitDiffFor(ITextDocument textDocument, ITextSnapshot snapshot);
    }
}
