namespace GitScc.Diff
{
    using System.Collections.Generic;
    using NGit.Diff;
    using Constants = NGit.Constants;
    using File = System.IO.File;
    using Path = System.IO.Path;

    public class GitCommands : IGitCommands
    {
        public IEnumerable<HunkRangeInfo> GetGitDiffFor(string filename)
        {
            GitFileStatusTracker tracker = new GitFileStatusTracker(Path.GetDirectoryName(filename));
            if (!tracker.HasGitRepository || tracker.Repository.Resolve(Constants.HEAD) == null)
                yield break;

            GitFileStatus status = tracker.GetFileStatus(filename);
            if (status == GitFileStatus.New || status == GitFileStatus.Added)
                yield break;

            HistogramDiff diff = new HistogramDiff();
            diff.SetFallbackAlgorithm(null);
            string fullName = Path.Combine(tracker.GitWorkingDirectory, filename.Replace('/', Path.DirectorySeparatorChar));
            RawText b = new RawText(File.Exists(fullName) ? File.ReadAllBytes(fullName) : new byte[0]);
            RawText a = new RawText(tracker.GetFileContent(filename) ?? new byte[0]);
            EditList edits = diff.Diff(RawTextComparator.WS_IGNORE_TRAILING, a, b);
            foreach (Edit edit in edits)
                yield return new HunkRangeInfo(edit, a, b);
        }
    }
}
