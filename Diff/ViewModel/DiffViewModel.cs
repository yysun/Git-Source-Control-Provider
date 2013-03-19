namespace GitScc.Diff.ViewModel
{
    using System;
    using System.Linq;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Media;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.Command;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Formatting;

    public class DiffViewModel : ViewModelBase
    {
        private readonly DiffMargin _margin;
        private readonly HunkRangeInfo _hunkRangeInfo;
        private readonly IWpfTextView _textView;

        private readonly ICommand _copyOldTextCommand;
        private readonly ICommand _rollbackCommand;
        private readonly ICommand _showPopUpCommand;

        private bool _isDiffTextVisible;
        private bool _showPopup;
        private bool _reverted;

        private bool _isVisible;
        private double _height;
        private double _top;

        public DiffViewModel(DiffMargin margin, HunkRangeInfo hunkRangeInfo, IWpfTextView textView)
        {
            _margin = margin;
            _hunkRangeInfo = hunkRangeInfo;
            _textView = textView;
            _margin.BrushesChanged += HandleBrushesChanged;

            _copyOldTextCommand = new RelayCommand(CopyOldText, CopyOldTextCanExecute);
            _showPopUpCommand = new RelayCommand(ShowPopUp);
            _rollbackCommand = new RelayCommand(Rollback, RollbackCanExecute);

            ShowPopup = false;

            SetDisplayProperties(false, null);

            DiffText = GetDiffText();

            IsDiffTextVisible = GetIsDiffTextVisible();
        }

        public Brush DiffBrush
        {
            get
            {
                if (_hunkRangeInfo.IsAddition)
                    return _margin.AdditionBrush;
                else if (_hunkRangeInfo.IsModification)
                    return _margin.ModificationBrush;
                else
                    return _margin.RemovedBrush;
            }
        }

        public FontFamily FontFamily
        {
            get
            {
                if (_margin.ClassificationFormatMap.DefaultTextProperties.TypefaceEmpty)
                    return new FontFamily("Consolas");

                return _margin.ClassificationFormatMap.DefaultTextProperties.Typeface.FontFamily;
            }
        }

        public FontStretch FontStretch
        {
            get
            {
                if (_margin.ClassificationFormatMap.DefaultTextProperties.TypefaceEmpty)
                    return FontStretches.Normal;

                return _margin.ClassificationFormatMap.DefaultTextProperties.Typeface.Stretch;
            }
        }

        public FontStyle FontStyle
        {
            get
            {
                if (_margin.ClassificationFormatMap.DefaultTextProperties.TypefaceEmpty)
                    return FontStyles.Normal;

                return _margin.ClassificationFormatMap.DefaultTextProperties.Typeface.Style;
            }
        }

        public FontWeight FontWeight
        {
            get
            {
                if (_margin.ClassificationFormatMap.DefaultTextProperties.TypefaceEmpty)
                    return FontWeights.Normal;

                return _margin.ClassificationFormatMap.DefaultTextProperties.Typeface.Weight;
            }
        }

        public double FontSize
        {
            get
            {
                if (_margin.ClassificationFormatMap.DefaultTextProperties.FontRenderingEmSizeEmpty)
                    return 12.0;

                return _margin.ClassificationFormatMap.DefaultTextProperties.FontRenderingEmSize;
            }
        }

        public double MaxWidth
        {
            get
            {
                return _margin.TextView.ViewportWidth;
            }
        }

        public double MaxHeight
        {
            get
            {
                return Math.Max(_margin.TextView.ViewportHeight * 2.0 / 3.0, 400);
            }
        }

        public Brush Background
        {
            get
            {
                if (_margin.ClassificationFormatMap.DefaultTextProperties.BackgroundBrushEmpty)
                    return _margin.TextView.Background;

                return _margin.ClassificationFormatMap.DefaultTextProperties.BackgroundBrush;
            }
        }

        public Brush Foreground
        {
            get
            {
                if (_margin.ClassificationFormatMap.DefaultTextProperties.ForegroundBrushEmpty)
                    return (Brush)Application.Current.Resources[VsBrushes.ToolWindowTextKey];

                return _margin.ClassificationFormatMap.DefaultTextProperties.ForegroundBrush;
            }
        }

        public int LineNumber { get { return _hunkRangeInfo.NewHunkRange.StartingLineNumber; } }

        private void HandleBrushesChanged(object sender, EventArgs e)
        {
            RaisePropertyChanged(() => DiffBrush);
        }

        private void SetDisplayProperties(bool approximate, TextViewLayoutChangedEventArgs e)
        {
            UpdateDimensions(approximate, e);
            Coordinates = string.Format("Top:{0}, Height:{1}, New number of Lines: {2}, StartingLineNumber: {3}", Top, Height, _hunkRangeInfo.NewHunkRange.NumberOfLines, _hunkRangeInfo.NewHunkRange.StartingLineNumber);
        }

        private bool GetIsDiffTextVisible()
        {
            return _hunkRangeInfo.IsDeletion || _hunkRangeInfo.IsModification;
        }

        private string GetDiffText()
        {
            if (_hunkRangeInfo.OriginalText != null && _hunkRangeInfo.OriginalText.Any())
            {
                return _hunkRangeInfo.IsModification || _hunkRangeInfo.IsDeletion ? String.Join(Environment.NewLine, _hunkRangeInfo.OriginalText) : string.Empty;
            }

            return string.Empty;
        }

        private void UpdateDimensions(bool approximate, TextViewLayoutChangedEventArgs e)
        {
            if (_reverted)
                return;

            if (approximate)
            {
                // TODO: we might be able to improve support for items initially off screen
                if (!IsVisible)
                    return;

                if (e.OldViewState.EditSnapshot != e.NewViewState.EditSnapshot)
                {
                    // TODO: handle to prevent flickering during edits
                    IsVisible = false;
                    return;
                }

                double shift = e.NewViewState.ViewportTop - e.OldViewState.ViewportTop;
                this.Top -= shift;
                // TODO: update height for blocks which were partially off-screen
                return;
            }

            ITextSnapshot snapshot = _textView.TextBuffer.CurrentSnapshot;
            HunkRangeInfo hunkRangeInfo = _hunkRangeInfo.TranslateTo(snapshot);

            int startLineNumber = hunkRangeInfo.NewHunkRange.StartingLineNumber;
            int endLineNumber = startLineNumber + hunkRangeInfo.NewHunkRange.NumberOfLines - 1;
            if (startLineNumber < 0
                || startLineNumber >= snapshot.LineCount
                || endLineNumber < 0
                || endLineNumber >= snapshot.LineCount)
            {
                IsVisible = false;
                return;
            }

            ITextSnapshotLine startLine = _textView.TextSnapshot.GetLineFromLineNumber(startLineNumber);
            ITextSnapshotLine endLine = _textView.TextSnapshot.GetLineFromLineNumber(endLineNumber);
            if (startLine != null && endLine != null)
            {
                if (endLine.LineNumber < startLine.LineNumber)
                {
                    SnapshotSpan span = new SnapshotSpan(endLine.Start, startLine.End);
                    if (!_textView.TextViewLines.FormattedSpan.IntersectsWith(span))
                    {
                        IsVisible = false;
                        return;
                    }
                }
                else
                {
                    SnapshotSpan span = new SnapshotSpan(startLine.Start, endLine.End);
                    if (!_textView.TextViewLines.FormattedSpan.IntersectsWith(span))
                    {
                        IsVisible = false;
                        return;
                    }
                }

                IWpfTextViewLine startLineView = _textView.GetTextViewLineContainingBufferPosition(startLine.Start);
                IWpfTextViewLine endLineView = _textView.GetTextViewLineContainingBufferPosition(endLine.Start);
                if (startLineView == null || endLineView == null)
                {
                    IsVisible = false;
                    return;
                }

                if (_textView.TextViewLines.LastVisibleLine.EndIncludingLineBreak < startLineView.Start
                    || _textView.TextViewLines.FirstVisibleLine.Start > endLineView.EndIncludingLineBreak)
                {
                    IsVisible = false;
                    return;
                }

                double startTop;
                switch (startLineView.VisibilityState)
                {
                case VisibilityState.FullyVisible:
                    startTop = startLineView.Top - _textView.ViewportTop;
                    break;

                case VisibilityState.Hidden:
                    startTop = startLineView.Top - _textView.ViewportTop;
                    break;

                case VisibilityState.PartiallyVisible:
                    startTop = startLineView.Top - _textView.ViewportTop;
                    break;

                case VisibilityState.Unattached:
                    // if the closest line was past the end we would have already returned
                    startTop = 0;
                    break;

                default:
                    // shouldn't be reachable, but definitely hide if this is the case
                    IsVisible = false;
                    return;
                }

                if (startTop >= _textView.ViewportHeight + _textView.LineHeight)
                {
                    // shouldn't be reachable, but definitely hide if this is the case
                    IsVisible = false;
                    return;
                }

                double stopBottom;
                switch (endLineView.VisibilityState)
                {
                case VisibilityState.FullyVisible:
                    stopBottom = endLineView.Bottom - _textView.ViewportTop;
                    break;

                case VisibilityState.Hidden:
                    stopBottom = endLineView.Bottom - _textView.ViewportTop;
                    break;

                case VisibilityState.PartiallyVisible:
                    stopBottom = endLineView.Bottom - _textView.ViewportTop;
                    break;

                case VisibilityState.Unattached:
                    // if the closest line was before the start we would have already returned
                    stopBottom = _textView.ViewportHeight;
                    break;

                default:
                    // shouldn't be reachable, but definitely hide if this is the case
                    IsVisible = false;
                    return;
                }

                if (stopBottom <= -_textView.LineHeight)
                {
                    // shouldn't be reachable, but definitely hide if this is the case
                    IsVisible = false;
                    return;
                }

                if (stopBottom <= startTop)
                {
                    if (hunkRangeInfo.IsDeletion)
                    {
                        double center = (startTop + stopBottom) / 2.0;
                        Top = center - (_textView.LineHeight / 2.0);
                        Height = _textView.LineHeight;
                        IsVisible = true;
                    }
                    else
                    {
                        // could be reachable if translation changes an addition to empty
                        IsVisible = false;
                    }

                    return;
                }

                Top = startTop;
                Height = stopBottom - startTop;
                IsVisible = true;
            }
        }

        public void RefreshPosition(bool approximate, TextViewLayoutChangedEventArgs e)
        {
            SetDisplayProperties(approximate, e);
        }

        public Thickness Margin
        {
            get
            {
                return new Thickness(DiffMargin.ChangeLeft, 0, 0, 0);
            }
        }

        public double Width
        {
            get
            {
                return DiffMargin.ChangeWidth;
            }
        }

        public double Height
        {
            get { return _height; }
            set
            {
                _height = value;
                RaisePropertyChanged(() => Height);
            }
        }

        public double Top
        {
            get { return _top; }
            private set
            {
                _top = value;
                RaisePropertyChanged(() => Top);
            }
        }

        public bool IsDeletion { get { return _hunkRangeInfo.IsDeletion;} }

        public ICommand ShowPopUpCommand
        {
            get
            {
                return _showPopUpCommand;
            }
        }

        public bool ShowPopup
        {
            get { return _showPopup; }
            set
            {
                if (value == _showPopup) return;
                _showPopup = value;
                RaisePropertyChanged(() => ShowPopup);
            }
        }

        public string Coordinates { get; private set; }

        public string DiffText { get; private set; }

        public bool IsVisible
        {
            get { return _isVisible; }
            set { _isVisible = value;
            RaisePropertyChanged(() => IsVisible);}
        }

        public bool IsDiffTextVisible
        {
            get { return _isDiffTextVisible; }
            set
            {
                if (value == _isDiffTextVisible) return;
                _isDiffTextVisible = value;
                RaisePropertyChanged(() => IsDiffTextVisible);
            }
        }

        public ICommand CopyOldTextCommand
        {
            get
            {
                return _copyOldTextCommand;
            }
        }

        public ICommand RollbackCommand
        {
            get
            {
                return _rollbackCommand;
            }
        }

        private bool CopyOldTextCanExecute()
        {
            return _hunkRangeInfo.IsModification || _hunkRangeInfo.IsDeletion;
        }

        private void CopyOldText()
        {
            Clipboard.SetText(DiffText);
        }

        private bool RollbackCanExecute()
        {
            if (!_hunkRangeInfo.CanRollback)
                return false;

            return _hunkRangeInfo.Snapshot == _textView.TextBuffer.CurrentSnapshot;
        }

        private void Rollback()
        {
            var snapshot = _hunkRangeInfo.Snapshot;
            if (snapshot != snapshot.TextBuffer.CurrentSnapshot)
                return;

            using (ITextEdit edit = snapshot.TextBuffer.CreateEdit())
            {
                Span newSpan;
                if (_hunkRangeInfo.IsDeletion)
                {
                    ITextSnapshotLine startLine = snapshot.GetLineFromLineNumber(_hunkRangeInfo.NewHunkRange.StartingLineNumber);
                    newSpan = new Span(startLine.Start.Position, 0);
                }
                else
                {
                    ITextSnapshotLine startLine = snapshot.GetLineFromLineNumber(_hunkRangeInfo.NewHunkRange.StartingLineNumber);
                    ITextSnapshotLine endLine = snapshot.GetLineFromLineNumber(_hunkRangeInfo.NewHunkRange.StartingLineNumber + _hunkRangeInfo.NewHunkRange.NumberOfLines - 1);
                    newSpan = Span.FromBounds(startLine.Start.Position, endLine.EndIncludingLineBreak.Position);
                }

                if (_hunkRangeInfo.IsAddition)
                {
                    ITextSnapshotLine startLine = snapshot.GetLineFromLineNumber(_hunkRangeInfo.NewHunkRange.StartingLineNumber);
                    ITextSnapshotLine endLine = snapshot.GetLineFromLineNumber(_hunkRangeInfo.NewHunkRange.StartingLineNumber + _hunkRangeInfo.NewHunkRange.NumberOfLines - 1);
                    edit.Delete(Span.FromBounds(startLine.Start.Position, endLine.EndIncludingLineBreak.Position));
                }
                else
                {
                    string lineBreak = snapshot.GetLineFromLineNumber(0).GetLineBreakText();
                    if (string.IsNullOrEmpty(lineBreak))
                        lineBreak = Environment.NewLine;

                    string originalText = string.Join(lineBreak, _hunkRangeInfo.OriginalText);
                    if (_hunkRangeInfo.NewHunkRange.StartingLineNumber + _hunkRangeInfo.NewHunkRange.NumberOfLines != snapshot.LineCount)
                        originalText += lineBreak;

                    edit.Replace(newSpan, originalText);
                }

                edit.Apply();

                // immediately hide the change
                _reverted = true;
                ShowPopup = false;
                IsVisible = false;
            }
        }

        private void ShowPopUp()
        {
            ShowPopup = true;
        }
    }
}
