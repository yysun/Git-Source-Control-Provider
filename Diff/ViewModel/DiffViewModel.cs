﻿namespace GitScc.Diff.ViewModel
{
    using System;
    using System.Linq;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Media;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.Command;
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

            _copyOldTextCommand = new RelayCommand(CopyOldText, CopyOldTextCanExecute);
            _showPopUpCommand = new RelayCommand(ShowPopUp);
            _rollbackCommand = new RelayCommand(Rollback, RollbackCanExecute);

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

        public int LineNumber { get { return _hunkRangeInfo.NewHunkRange.StartingLineNumber; } }

        private void SetDisplayProperties()
        {
            UpdateDimensions();
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

        private void UpdateDimensions()
        {
            if (_reverted)
                return;

            HunkRangeInfo hunkRangeInfo = _hunkRangeInfo.TranslateTo(_textView.TextBuffer.CurrentSnapshot);

            ITextSnapshotLine startLine = _textView.TextSnapshot.GetLineFromLineNumber(hunkRangeInfo.NewHunkRange.StartingLineNumber);
            ITextSnapshotLine endLine = _textView.TextSnapshot.GetLineFromLineNumber(hunkRangeInfo.NewHunkRange.StartingLineNumber + hunkRangeInfo.NewHunkRange.NumberOfLines - 1);
            if (startLine != null && endLine != null)
            {
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
