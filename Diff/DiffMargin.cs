namespace GitScc.Diff
{
    using System;
    using System.Windows.Controls;
    using GitScc.Diff.View;
    using GitScc.Diff.ViewModel;
    using Microsoft.VisualStudio.Text.Editor;

    internal class DiffMargin : Canvas, IWpfTextViewMargin
    {
        public const string MarginName = "GitDiffMargin";
        private bool _isDisposed = false;
        private readonly IWpfTextView _textView;
        private readonly DiffMarginControl _gitDiffBarControl;

        /// <summary>
        ///   Creates a <see cref="GitDiffMargin" /> for a given <see cref="IWpfTextView" /> .
        /// </summary>
        /// <param name="textView"> The <see cref="IWpfTextView" /> to attach the margin to. </param>
        public DiffMargin(IWpfTextView textView)
        {
            Width = 9;
            _textView = textView;

            _gitDiffBarControl = new DiffMarginControl
            {
                DataContext = new DiffMarginViewModel(_textView, new GitCommands())
            };

            Children.Add(_gitDiffBarControl);
        }

        /// <summary>
        ///   The <see cref="Sytem.Windows.FrameworkElement" /> that implements the visual representation of the margin.
        /// </summary>
        public System.Windows.FrameworkElement VisualElement
        {
            // Since this margin implements Canvas, this is the object which renders
            // the margin.
            get
            {
                ThrowIfDisposed();
                return this;
            }
        }

        public double MarginSize
        {
            // Since this is a horizontal margin, its width will be bound to the width of the text view.
            // Therefore, its size is its height.
            get
            {
                ThrowIfDisposed();
                return this.ActualHeight;
            }
        }

        public bool Enabled
        {
            // The margin should always be enabled
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
            return (marginName == DiffMargin.MarginName) ? (IWpfTextViewMargin)this : null;
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
