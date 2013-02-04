namespace GitScc.Diff.ViewModel
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.Command;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;

    public class DiffMarginViewModel : ViewModelBase
    {
        private readonly DiffMargin _margin;
        private readonly IWpfTextView _textView;
        private readonly IGitCommands _gitCommands;

        private ITextDocument _document;
        private RelayCommand<DiffViewModel> _previousChangeCommand;
        private RelayCommand<DiffViewModel> _nextChangeCommand;

        public DiffMarginViewModel(DiffMargin margin, IWpfTextView textView, ITextDocumentFactoryService textDocumentFactoryService, IGitCommands gitCommands)
        {
            if (margin == null)
                throw new ArgumentNullException("margin");
            if (textView == null)
                throw new ArgumentNullException("textView");
            if (textDocumentFactoryService == null)
                throw new ArgumentNullException("textDocumentFactoryService");
            if (gitCommands == null)
                throw new ArgumentNullException("gitCommands");

            _margin = margin;
            _textView = textView;
            _gitCommands = gitCommands;

            DiffViewModels = new ObservableCollection<DiffViewModel>();

            _textView.Closed += TextViewClosed;
            _textView.TextBuffer.Changed += TextBufferChanged;

            _textView.LayoutChanged += OnLayoutChanged;
            _textView.ViewportHeightChanged += OnViewportHeightChanged;

            // Delay the initial check until the view gets focus
            _textView.GotAggregateFocus += GotAggregateFocus;

            if (!textDocumentFactoryService.TryGetTextDocument(_textView.TextBuffer, out _document))
                _document = null;

            if (_document != null)
                _document.FileActionOccurred += FileActionOccurred;
        }

        private void OnViewportHeightChanged(object sender, EventArgs e)
        {
            RefreshDiffViewModelPositions();
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            RefreshDiffViewModelPositions();
        }

        private void GotAggregateFocus(object sender, EventArgs e)
        {
            _textView.GotAggregateFocus -= GotAggregateFocus;

            CreateDiffViewModels();
        }

        private void TextBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            // this is correctly called but the file is not saved and then nothing new is shown
            //todo Modify to work on a copy of the file
            RefreshDiffViewModelPositions();
        }

        public ObservableCollection<DiffViewModel> DiffViewModels { get; set; }

        public RelayCommand<DiffViewModel> PreviousChangeCommand
        {
            get { return _previousChangeCommand ?? (_previousChangeCommand = new RelayCommand<DiffViewModel>(PreviousChange, PreviousChangeCanExecute)); }
        }

        public RelayCommand<DiffViewModel> NextChangeCommand
        {
            get { return _nextChangeCommand ?? (_nextChangeCommand = new RelayCommand<DiffViewModel>(NextChange, NextChangeCanExecute)); }
        }

        private bool PreviousChangeCanExecute(DiffViewModel currentDiffViewModel)
        {
            return DiffViewModels.IndexOf(currentDiffViewModel) > 0;
        }

        private bool NextChangeCanExecute(DiffViewModel currentDiffViewModel)
        {
            return DiffViewModels.IndexOf(currentDiffViewModel) < (DiffViewModels.Count - 1);
        }

        private void PreviousChange(DiffViewModel currentDiffViewModel)
        {
            MoveToChange(currentDiffViewModel, -1);
        }

        private void NextChange(DiffViewModel currentDiffViewModel)
        {
            MoveToChange(currentDiffViewModel, +1);
        }

        private void MoveToChange(DiffViewModel currentDiffViewModel, int indexModifier)
        {
            var diffViewModelIndex = DiffViewModels.IndexOf(currentDiffViewModel) + indexModifier;
            var diffViewModel  = DiffViewModels[diffViewModelIndex];
            var diffLine = _textView.TextSnapshot.GetLineFromLineNumber(diffViewModel.LineNumber);
            currentDiffViewModel.ShowPopup = false;

            _textView.VisualElement.Focus();
            _textView.Caret.MoveTo(diffLine.Start);
            _textView.Caret.EnsureVisible();
        }

        private void RefreshDiffViewModelPositions()
        {
            ActivityLog.LogInformation("GitDiffMargin", "RefreshDiffViewModelPositions: " + _document.FilePath);

            foreach (var diffViewModel in DiffViewModels)
            {
                diffViewModel.RefreshPosition();
            }
        }

        private void CreateDiffViewModels()
        {
            ActivityLog.LogInformation("GitDiffMargin", "CreateDiffViewModels: " + _document.FilePath);

            var rangeInfos = _gitCommands.GetGitDiffFor(_document, _document.TextBuffer.CurrentSnapshot);

            DiffViewModels.Clear();

            foreach (var diffViewModel in rangeInfos.Select(hunkRangeInfo => new DiffViewModel(_margin, hunkRangeInfo, _textView)))
            {
                DiffViewModels.Add(diffViewModel);
            }
        }

        private void FileActionOccurred(object sender, TextDocumentFileActionEventArgs e)
        {
            if ((e.FileActionType & FileActionTypes.ContentLoadedFromDisk) != 0 ||
                (e.FileActionType & FileActionTypes.ContentSavedToDisk) != 0)
            {
                CreateDiffViewModels();
            }
        }

        private void TextViewClosed(object sender, EventArgs e)
        {
            CleanUp();
        }

        private void CleanUp()
        {
            if (_document != null)
            {
                _document.FileActionOccurred -= FileActionOccurred;
                _document = null;
            }

            if (_textView != null)
            {
                _textView.Closed -= TextViewClosed;
                _textView.GotAggregateFocus -= GotAggregateFocus;
                if (_textView.TextBuffer != null)
                {
                    _textView.TextBuffer.Changed -= TextBufferChanged;
                }
            }
        }
    }
}