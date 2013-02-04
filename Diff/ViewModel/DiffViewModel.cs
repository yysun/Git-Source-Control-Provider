namespace GitScc.Diff.ViewModel
{
    using System;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Media;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.Command;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using System.Linq;
    using Microsoft.VisualStudio.Text.Formatting;

    public class DiffViewModel : ViewModelBase
    {
        private readonly DiffMargin _margin;
        private readonly HunkRangeInfo _hunkRangeInfo;
        private readonly IWpfTextView _textView;

        private bool _isDiffTextVisible;
        private bool _showPopup;
        private ICommand _copyOldTextCommand;
        private ICommand _rollbackCommand;
        private ICommand _showPopUpCommand;

        private bool _isVisible;
        private double _height;
        private double _top;

        public DiffViewModel(DiffMargin margin, HunkRangeInfo hunkRangeInfo, IWpfTextView textView)
        {
            _margin = margin;
            _hunkRangeInfo = hunkRangeInfo;
            _textView = textView;

            ShowPopup = false;

            SetDisplayProperties();

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

        public int LineNumber { get { return (int)_hunkRangeInfo.NewHunkRange.StartingLineNumber; } }

        private void SetDisplayProperties()
        {
            Height = GetHeight();
            Top = GetTopCoordinate();

            IsVisible = GetIsVisible();

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

        private bool GetIsVisible()
        {
            var hunkStartLineNumber = (int) _hunkRangeInfo.NewHunkRange.StartingLineNumber;
            var hunkEndLineNumber = (int) (_hunkRangeInfo.NewHunkRange.StartingLineNumber + _hunkRangeInfo.NewHunkRange.NumberOfLines);

            var hunkStartline = _textView.TextSnapshot.GetLineFromLineNumber(hunkStartLineNumber);
            var hunkEndline = _textView.TextSnapshot.GetLineFromLineNumber(hunkEndLineNumber);
            if (hunkStartline != null && hunkEndline != null)
            {
                var topLine = _textView.GetTextViewLineContainingBufferPosition(hunkStartline.Start);
                var bottomLine = _textView.GetTextViewLineContainingBufferPosition(hunkEndline.End);

                if (IsHunkFullyVisible(topLine, bottomLine))
                {
                    return true;
                }

                if (topLine.VisibilityState != VisibilityState.FullyVisible && bottomLine.VisibilityState == VisibilityState.FullyVisible)
                {
                    Top = 0;

                    var numberofHiddenLines = _textView.ViewportTop/_textView.LineHeight;

                    var hiddenHunkLines = numberofHiddenLines - hunkStartLineNumber;

                    Height = Height - Math.Ceiling(Math.Abs(hiddenHunkLines) * _textView.LineHeight);

                    return true;
                }

                if (topLine.VisibilityState == VisibilityState.FullyVisible && bottomLine.VisibilityState != VisibilityState.FullyVisible)
                {
                    Height = _textView.ViewportBottom - (Top + 1);

                    return true;
                }

                if (topLine.VisibilityState != VisibilityState.FullyVisible && bottomLine.VisibilityState != VisibilityState.FullyVisible)
                {
                    if ((hunkEndLineNumber - hunkStartLineNumber) * _textView.LineHeight >= _textView.ViewportHeight)
                    {
                        if ((hunkEndLineNumber + 1) * _textView.LineHeight >= _textView.ViewportBottom)
                        {
                            Top = 0;
                            Height = _textView.ViewportHeight;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool IsHunkFullyVisible(IWpfTextViewLine topLine, IWpfTextViewLine bottomLine)
        {
            return topLine.VisibilityState == VisibilityState.FullyVisible && bottomLine.VisibilityState == VisibilityState.FullyVisible;
        }

        private double GetHeight()
        {
            var lineHeight = _textView.LineHeight;

            if (_hunkRangeInfo.IsDeletion)
            {
                return _textView.LineHeight;
            }
            
            return _hunkRangeInfo.NewHunkRange.NumberOfLines*lineHeight;
        }

        private double GetTopCoordinate()
        {
            var start = _textView.TextSnapshot.GetLineFromLineNumber((int) _hunkRangeInfo.NewHunkRange.StartingLineNumber).Extent;
            var end = _textView.TextSnapshot.GetLineFromLineNumber((int)_hunkRangeInfo.NewHunkRange.StartingLineNumber + (int)_hunkRangeInfo.NewHunkRange.NumberOfLines).Extent;

            var span = new SnapshotSpan(_textView.TextSnapshot, Span.FromBounds(start.Start, end.End));
            var g = _textView.TextViewLines.GetMarkerGeometry(span);

            if (g != null)
            {
                return g.Bounds.Top - _textView.ViewportTop;
            }

            var ratio = _hunkRangeInfo.NewHunkRange.StartingLineNumber / (_textView.ViewportHeight / _textView.LineHeight);
            var topCoordinate = Math.Ceiling((ratio * _textView.ViewportHeight) - _textView.ViewportTop);
            return topCoordinate;
        }

        public void RefreshPosition()
        {
            SetDisplayProperties();
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
            get { return _showPopUpCommand ?? (_showPopUpCommand = new RelayCommand(ShowPopUp)); }
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
            get { return _copyOldTextCommand ?? (_copyOldTextCommand = new RelayCommand(CopyOldText, CopyOldTextCanExecute)); }
        }

        public ICommand RollbackCommand
        {
            get { return _rollbackCommand ?? (_rollbackCommand = new RelayCommand(Rollback, RollbackCanExecute)); }
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
            return _hunkRangeInfo.IsModification || _hunkRangeInfo.IsDeletion;
        }

        private void Rollback()
        {
            var snapshot = _hunkRangeInfo.Snapshot;
            if (snapshot != snapshot.TextBuffer.CurrentSnapshot)
                return;

            using (ITextEdit edit = snapshot.TextBuffer.CreateEdit())
            {
                if (_hunkRangeInfo.NewHunkRange.NumberOfLines == 1)
                {
                    var line = snapshot.GetLineFromLineNumber((int) _hunkRangeInfo.NewHunkRange.StartingLineNumber);

                    var text = line.ExtentIncludingLineBreak.GetText();

                    if(text.EndsWith("\r\n"))
                    {
                        edit.Replace(line.ExtentIncludingLineBreak, _hunkRangeInfo.OriginalText[0] + "\r\n");
                    }
                    else
                    {
                        edit.Replace(line.ExtentIncludingLineBreak, _hunkRangeInfo.OriginalText[0]);
                    }
                }
                else
                {
                    for (var n = 0; n <= _hunkRangeInfo.NewHunkRange.NumberOfLines; n++)
                    {
                        var line = snapshot.GetLineFromLineNumber((int)_hunkRangeInfo.NewHunkRange.StartingLineNumber + n);
                        edit.Delete(line.Start.Position, line.Length);
                    }

                    var startLine = snapshot.GetLineFromLineNumber((int) _hunkRangeInfo.NewHunkRange.StartingLineNumber);

                    foreach (var line in _hunkRangeInfo.OriginalText)
                    {
                        edit.Insert(startLine.Start.Position, line + "\r\n");                        
                    }
                }

                edit.Apply();
            }
        }

        private void ShowPopUp()
        {
            ShowPopup = true;
        }
    }
}