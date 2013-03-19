namespace GitScc.Diff
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using GitScc.Diff.View;
    using GitScc.Diff.ViewModel;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;

    public sealed class DiffMargin : IWpfTextViewMargin
    {
        public const string MarginName = "GitDiffMargin";

        internal const double ChangeLeft = 2.5;
        internal const double ChangeWidth = 5.0;
        private const double MarginWidth = 10.0;

        private readonly IWpfTextView _textView;
        private readonly IClassificationFormatMap _classificationFormatMap;
        private readonly IEditorFormatMap _editorFormatMap;
        private readonly DiffMarginViewModel _viewModel;
        private readonly DiffMarginControl _gitDiffBarControl;
        private bool _isDisposed;

        private Brush _additionBrush;
        private Brush _modificationBrush;
        private Brush _removedBrush;

        /// <summary>
        ///   Creates a <see cref="GitDiffMargin" /> for a given <see cref="IWpfTextView" /> .
        /// </summary>
        /// <param name="textView"> The <see cref="IWpfTextView" /> to attach the margin to. </param>
        internal DiffMargin(IWpfTextView textView, DiffMarginFactory factory)
        {
            _textView = textView;
            _classificationFormatMap = factory.ClassificationFormatMapService.GetClassificationFormatMap(textView);
            _editorFormatMap = factory.EditorFormatMapService.GetEditorFormatMap(textView);

            _editorFormatMap.FormatMappingChanged += HandleFormatMappingChanged;
            _textView.Closed += (sender, e) => _editorFormatMap.FormatMappingChanged -= HandleFormatMappingChanged;
            UpdateBrushes();

            _textView.Options.OptionChanged += HandleOptionChanged;

            _gitDiffBarControl = new DiffMarginControl();
            _viewModel = new DiffMarginViewModel(this, _textView, factory.TextDocumentFactoryService, new GitCommands());
            _gitDiffBarControl.DataContext = _viewModel;
            _gitDiffBarControl.Width = MarginWidth;
            UpdateVisibility();
        }

        public event EventHandler BrushesChanged;

        /// <summary>
        ///   The <see cref="Sytem.Windows.FrameworkElement" /> that implements the visual representation of the margin.
        /// </summary>
        public FrameworkElement VisualElement
        {
            get
            {
                ThrowIfDisposed();
                return _gitDiffBarControl;
            }
        }

        public IClassificationFormatMap ClassificationFormatMap
        {
            get
            {
                return _classificationFormatMap;
            }
        }

        public IWpfTextView TextView
        {
            get
            {
                return _textView;
            }
        }

        public double MarginSize
        {
            get
            {
                ThrowIfDisposed();
                return _gitDiffBarControl.ActualWidth;
            }
        }

        public bool Enabled
        {
            get
            {
                ThrowIfDisposed();
                return _textView.Options.GetOptionValue(GitTextViewOptions.DiffMarginId);
            }
        }

        public Brush AdditionBrush
        {
            get
            {
                return _additionBrush ?? Brushes.Transparent;
            }
        }

        public Brush ModificationBrush
        {
            get
            {
                return _modificationBrush ?? Brushes.Transparent;
            }
        }

        public Brush RemovedBrush
        {
            get
            {
                return _removedBrush ?? Brushes.Transparent;
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
            _viewModel.Cleanup();
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }

        private void OnBrushesChanged(EventArgs e)
        {
            var t = BrushesChanged;
            if (t != null)
                t(this, e);
        }

        private void HandleFormatMappingChanged(object sender, FormatItemsEventArgs e)
        {
            if (_isDisposed)
                return;

            if (e.ChangedItems.Contains(DiffFormatNames.Addition)
                || e.ChangedItems.Contains(DiffFormatNames.Modification)
                || e.ChangedItems.Contains(DiffFormatNames.Removed))
            {
                UpdateBrushes();
            }
        }

        private void HandleOptionChanged(object sender, EditorOptionChangedEventArgs e)
        {
            if (!_isDisposed && e.OptionId == GitTextViewOptions.DiffMarginName)
                UpdateVisibility();
        }

        private void UpdateBrushes()
        {
            _additionBrush = GetBrush(_editorFormatMap.GetProperties(DiffFormatNames.Addition));
            _modificationBrush = GetBrush(_editorFormatMap.GetProperties(DiffFormatNames.Modification));
            _removedBrush = GetBrush(_editorFormatMap.GetProperties(DiffFormatNames.Removed));
            OnBrushesChanged(EventArgs.Empty);
        }

        private void UpdateVisibility()
        {
            ThrowIfDisposed();
            _gitDiffBarControl.Visibility = Enabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private static Brush GetBrush(ResourceDictionary properties)
        {
            if (properties == null)
                return Brushes.Transparent;

            if (properties.Contains(EditorFormatDefinition.BackgroundColorId))
            {
                Color color = (Color)properties[EditorFormatDefinition.BackgroundColorId];
                Brush brush = new SolidColorBrush(color);
                if (brush.CanFreeze)
                    brush.Freeze();

                return brush;
            }
            else if (properties.Contains(EditorFormatDefinition.BackgroundBrushId))
            {
                Brush brush = (Brush)properties[EditorFormatDefinition.BackgroundBrushId];
                if (brush.CanFreeze)
                    brush.Freeze();

                return brush;
            }

            return Brushes.Transparent;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);
        }
    }
}
