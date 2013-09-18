namespace GitScc.Diff
{
    using System.Collections.Generic;
    using Buffer = System.Buffer;
    using Constants = NGit.Constants;
    using CoreConfig = NGit.CoreConfig;
    using Edit = NGit.Diff.Edit;
    using EditList = NGit.Diff.EditList;
    using Environment = System.Environment;
    using HistogramDiff = NGit.Diff.HistogramDiff;
    using ITextDocument = Microsoft.VisualStudio.Text.ITextDocument;
    using ITextSnapshot = Microsoft.VisualStudio.Text.ITextSnapshot;
    using Path = System.IO.Path;
    using RawText = NGit.Diff.RawText;
    using RawTextComparator = NGit.Diff.RawTextComparator;
    using WorkingTreeOptions = NGit.Treewalk.WorkingTreeOptions;

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

            byte[] preamble = textDocument.Encoding.GetPreamble();
            byte[] content = textDocument.Encoding.GetBytes(currentText);
            if (preamble.Length > 0)
            {
                byte[] completeContent = new byte[preamble.Length + content.Length];
                Buffer.BlockCopy(preamble, 0, completeContent, 0, preamble.Length);
                Buffer.BlockCopy(content, 0, completeContent, preamble.Length, content.Length);
                content = completeContent;
            }

            byte[] previousContent = GetPreviousRevision(tracker, fileName);
            RawText b = new RawText(content);
            RawText a = new RawText(previousContent ?? new byte[0]);
            EditList edits = diff.Diff(RawTextComparator.DEFAULT, a, b);
            foreach (Edit edit in edits)
                yield return new HunkRangeInfo(snapshot, edit, a, b);
        }

        private static byte[] GetPreviousRevision(GitFileStatusTracker tracker, string fileName)
        {
            byte[] cachedBytes = tracker.GetFileContent(fileName);
            if (cachedBytes == null)
                return cachedBytes;

            if (Environment.NewLine != "\r\n")
                return cachedBytes;

            WorkingTreeOptions options = tracker.Repository.GetConfig().Get(WorkingTreeOptions.KEY);
            if (options.GetAutoCRLF() != CoreConfig.AutoCRLF.TRUE)
                return cachedBytes;

            int lines = 0;
            for (int i = 0; i < cachedBytes.Length; i++)
            {
                if (cachedBytes[i] == '\n' && (i == 0 || cachedBytes[i - 1] != '\r'))
                    lines++;
            }

            byte[] result = new byte[cachedBytes.Length + lines];
            for (int i = 0, j = 0; i < cachedBytes.Length; i++)
            {
                byte current = cachedBytes[i];
                if (current == '\n' && (i == 0 || cachedBytes[i - 1] != '\r'))
                    result[j++] = (byte)'\r';

                result[j++] = current;
            }

            return result;
        }
    }
}
