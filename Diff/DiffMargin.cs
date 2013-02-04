namespace GitScc.Diff
{
    using System;
    using System.Windows.Controls;
    using GitScc.Diff.View;
    using GitScc.Diff.ViewModel;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;

    internal class DiffMargin : Canvas, IWpfTextViewMargin
    {
        public const string MarginName = "GitDiffMargin";

        internal const double ChangeLeft = 2.5;
        internal const double ChangeWidth = 5.0;
        private const double MarginWidth = 10.0;

        private readonly IWpfTextView _textView;
        private readonly DiffMarginControl _gitDiffBarControl;
        private bool _isDisposed;

        /// <summary>
        ///   Creates a <see cref="GitDiffMargin" /> for a given <see cref="IWpfTextView" /> .
        /// </summary>
        /// <param name="textView"> The <see cref="IWpfTextView" /> to attach the margin to. </param>
        public DiffMargin(IWpfTextView textView, ITextDocumentFactoryService textDocumentFactoryService)
        {
            Width = MarginWidth;
            _textView = textView;

            _gitDiffBarControl = new DiffMarginControl();
            _gitDiffBarControl.DataContext = new DiffMarginViewModel(_textView, textDocumentFactoryService, new GitCommands());
            Children.Add(_gitDiffBarControl);
        }

        /// <summary>
        ///   The <see cref="Sytem.Windows.FrameworkElement" /> that implements the visual representation of the margin.
        /// </summary>
        public System.Windows.FrameworkElement VisualElement
        {
            get
            {
                ThrowIfDisposed();
                return this;
            }
        }

        public double MarginSize
        {
            get
            {
                ThrowIfDisposed();
                return this.ActualWidth;
            }
        }

        public bool Enabled
        {
            get
            {
                ThrowIfDisposed();
                return true;
            }
        }

        /// <summary>
        ///   Returns an instance of the margin if this is the margin that has been requested.
        /// </summary>
        /// <param name="marginName"> The name of the margin requested </param>
        /// <returns> An instance of GitDiffMargin or null </returns>
        public ITextViewMargin GetTextViewMargin(string marginName)
        {
            return string.Equals(marginName, DiffMargin.MarginName, StringComparison.OrdinalIgnoreCase) ? this : null;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _isDisposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);
        }
    }
}
