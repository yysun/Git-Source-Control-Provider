namespace GitScc.Diff
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Text;
    using Stopwatch = System.Diagnostics.Stopwatch;

    public class DiffUpdateBackgroundParser : BackgroundParser
    {
        private readonly IGitCommands _commands;

        public DiffUpdateBackgroundParser(ITextBuffer textBuffer, TaskScheduler taskScheduler, ITextDocumentFactoryService textDocumentFactoryService, IGitCommands commands)
            : base(textBuffer, taskScheduler, textDocumentFactoryService)
        {
            _commands = commands;
            ReparseDelay = TimeSpan.FromMilliseconds(500);
        }

        public override string Name
        {
            get
            {
                return "Git Diff Analyzer";
            }
        }

        protected override void ReParseImpl()
        {
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                ITextSnapshot snapshot = TextBuffer.CurrentSnapshot;
                ITextDocument textDocument;
                if (!TextDocumentFactoryService.TryGetTextDocument(TextBuffer, out textDocument))
                    textDocument = null;

                IEnumerable<HunkRangeInfo> diff;
                if (textDocument != null)
                    diff = _commands.GetGitDiffFor(textDocument, snapshot);
                else
                    diff = Enumerable.Empty<HunkRangeInfo>();

                DiffParseResultEventArgs result = new DiffParseResultEventArgs(snapshot, stopwatch.Elapsed, diff.ToList());
                OnParseComplete(result);
            }
            catch (InvalidOperationException)
            {
                base.MarkDirty(true);
                throw;
            }
        }
    }
}
