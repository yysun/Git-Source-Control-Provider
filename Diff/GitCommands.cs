namespace GitScc.Diff
{
    using System.Collections.Generic;
    using NGit.Diff;
    using Constants = NGit.Constants;
    using ITextDocument = Microsoft.VisualStudio.Text.ITextDocument;
    using ITextSnapshot = Microsoft.VisualStudio.Text.ITextSnapshot;
    using Path = System.IO.Path;

    public class GitCommands : IGitCommands
    {
        public IEnumerable<HunkRangeInfo> GetGitDiffFor(ITextDocument textDocument, ITextSnapshot snapshot)
        {
            string fileName = textDocument.FilePath;
            GitFileStatusTracker tracker = new GitFileStatusTracker(Path.GetDirectoryName(fileName));
            if (!tracker.HasGitRepository || tracker.Repository.Resolve(Constants.HEAD) == null)
                yield break;

            GitFileStatus status = tracker.GetFileStatus(fileName);
            if (status == GitFileStatus.New || status == GitFileStatus.Added)
                yield break;

            HistogramDiff diff = new HistogramDiff();
            diff.SetFallbackAlgorithm(null);
            string currentText = snapshot.GetText();
            RawText b = new RawText(textDocument.Encoding.GetBytes(currentText));
            RawText a = new RawText(tracker.GetFileContent(fileName) ?? new byte[0]);
            EditList edits = diff.Diff(RawTextComparator.WS_IGNORE_TRAILING, a, b);
            foreach (Edit edit in edits)
                yield return new HunkRangeInfo(edit, a, b);
        }
    }
}
