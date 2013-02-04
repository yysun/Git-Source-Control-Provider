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

    public class DiffMargin : Canvas, IWpfTextViewMargin
    {
        public const string MarginName = "GitDiffMargin";

        internal const double ChangeLeft = 2.5;
        internal const double ChangeWidth = 5.0;
        private const double MarginWidth = 10.0;

        private readonly IWpfTextView _textView;
        private readonly IEditorFormatMap _editorFormatMap;
        private readonly DiffMarginControl _gitDiffBarControl;
        private bool _isDisposed;

        private Brush _additionBrush;
        private Brush _modificationBrush;
        private Brush _removedBrush;

        /// <summary>
        ///   Creates a <see cref="GitDiffMargin" /> for a given <see cref="IWpfTextView" /> .
        /// </summary>
        /// <param name="textView"> The <see cref="IWpfTextView" /> to attach the margin to. </param>
        public DiffMargin(IWpfTextView textView, ITextDocumentFactoryService textDocumentFactoryService, IEditorFormatMapService editorFormatMapService)
        {
            Width = MarginWidth;
            _textView = textView;
            _editorFormatMap = editorFormatMapService.GetEditorFormatMap(textView);

            _editorFormatMap.FormatMappingChanged += HandleFormatMappingChanged;
            _textView.Closed += (sender, e) => _editorFormatMap.FormatMappingChanged -= HandleFormatMappingChanged;
            UpdateBrushes();

            HandleOptionChanged(null, null);
            _textView.Options.OptionChanged += HandleOptionChanged;

            IsVisibleChanged += (sender, e) =>
            {
                if ((bool)e.NewValue)
                    _textView.LayoutChanged += HandleLayoutChanged;
                else
                    _textView.LayoutChanged -= HandleLayoutChanged;
            };

            _gitDiffBarControl = new DiffMarginControl();
            _gitDiffBarControl.DataContext = new DiffMarginViewModel(this, _textView, textDocumentFactoryService, new GitCommands());
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
                return _textView.Options.IsSelectionMarginEnabled();
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
            GC.SuppressFinalize(this);
            _isDisposed = true;
        }

        private void HandleFormatMappingChanged(object sender, FormatItemsEventArgs e)
        {
            if (e.ChangedItems.Contains(DiffFormatNames.Addition)
                || e.ChangedItems.Contains(DiffFormatNames.Modification)
                || e.ChangedItems.Contains(DiffFormatNames.Removed))
            {
                UpdateBrushes();
            }
        }

        private void HandleOptionChanged(object sender, EditorOptionChangedEventArgs e)
        {
        }

        private void HandleLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            InvalidateVisual();
        }

        private void UpdateBrushes()
        {
            _additionBrush = GetBrush(_editorFormatMap.GetProperties(DiffFormatNames.Addition));
            _modificationBrush = GetBrush(_editorFormatMap.GetProperties(DiffFormatNames.Modification));
            _removedBrush = GetBrush(_editorFormatMap.GetProperties(DiffFormatNames.Removed));
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
