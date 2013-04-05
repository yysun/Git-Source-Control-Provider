namespace GitScc.Diff
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Text;
    using CancellationToken = System.Threading.CancellationToken;
    using Constants = NGit.Constants;
    using Directory = System.IO.Directory;
    using FileSystemEventArgs = System.IO.FileSystemEventArgs;
    using FileSystemWatcher = System.IO.FileSystemWatcher;
    using Path = System.IO.Path;
    using Stopwatch = System.Diagnostics.Stopwatch;
    using WatcherChangeTypes = System.IO.WatcherChangeTypes;

    public class DiffUpdateBackgroundParser : BackgroundParser
    {
        private readonly IGitCommands _commands;
        private readonly ITextBuffer _documentBuffer;
        private readonly FileSystemWatcher _watcher;

        public DiffUpdateBackgroundParser(ITextBuffer textBuffer, ITextBuffer documentBuffer, TaskScheduler taskScheduler, ITextDocumentFactoryService textDocumentFactoryService, IGitCommands commands)
            : base(textBuffer, taskScheduler, textDocumentFactoryService)
        {
            _documentBuffer = documentBuffer;
            _commands = commands;
            ReparseDelay = TimeSpan.FromMilliseconds(500);

            ITextDocument textDocument;
            if (TextDocumentFactoryService.TryGetTextDocument(TextBuffer, out textDocument))
            {
                GitFileStatusTracker tracker = new GitFileStatusTracker(Path.GetDirectoryName(textDocument.FilePath));
                if (tracker.HasGitRepository && tracker.Repository.Resolve(Constants.HEAD) != null)
                {
                    _watcher = new FileSystemWatcher(tracker.Repository.Directory.GetAbsolutePath());
                    _watcher.IncludeSubdirectories = true;
                    _watcher.Changed += HandleFileSystemChanged;
                    _watcher.Created += HandleFileSystemChanged;
                    _watcher.Deleted += HandleFileSystemChanged;
                    _watcher.Renamed += HandleFileSystemChanged;
                    _watcher.EnableRaisingEvents = true;
                }
            }
        }

        public override string Name
        {
            get
            {
                return "Git Diff Analyzer";
            }
        }

        private void HandleFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            Action action = () => ProcessFileSystemChange(e);
            Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, SccProviderService.TaskScheduler)
                .HandleNonCriticalExceptions();
        }

        private void ProcessFileSystemChange(FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed && Directory.Exists(e.FullPath))
                return;

            if (string.Equals(Path.GetExtension(e.Name), ".lock", StringComparison.OrdinalIgnoreCase))
                return;

            MarkDirty(true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_watcher != null)
                    _watcher.Dispose();
            }

            base.Dispose(disposing);
        }

        public ITextBuffer DocumentBuffer
        {
            get
            {
                return _documentBuffer;
            }
        }

        protected override void ReParseImpl()
        {
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                ITextSnapshot snapshot = TextBuffer.CurrentSnapshot;
                ITextDocument textDocument;
                if (!TextDocumentFactoryService.TryGetTextDocument(DocumentBuffer, out textDocument))
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
