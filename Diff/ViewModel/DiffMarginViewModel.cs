namespace GitScc.Diff.ViewModel
{
    using System;
    using System.Collections.ObjectModel;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.Command;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using TaskScheduler = System.Threading.Tasks.TaskScheduler;

    public class DiffMarginViewModel : ViewModelBase
    {
        private readonly DiffMargin _margin;
        private readonly IWpfTextView _textView;
        private readonly IGitCommands _gitCommands;
        private readonly ObservableCollection<DiffViewModel> _diffViewModels;
        private readonly DiffUpdateBackgroundParser _parser;
        private readonly RelayCommand<DiffViewModel> _previousChangeCommand;
        private readonly RelayCommand<DiffViewModel> _nextChangeCommand;

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
            _diffViewModels = new ObservableCollection<DiffViewModel>();
            _previousChangeCommand = new RelayCommand<DiffViewModel>(PreviousChange, PreviousChangeCanExecute);
            _nextChangeCommand = new RelayCommand<DiffViewModel>(NextChange, NextChangeCanExecute);

            _textView.LayoutChanged += OnLayoutChanged;
            _textView.ViewportHeightChanged += OnViewportHeightChanged;

            _parser = new DiffUpdateBackgroundParser(textView.TextBuffer, TaskScheduler.Default, textDocumentFactoryService, gitCommands);
            _parser.ParseComplete += HandleParseComplete;
            _parser.RequestParse(false);
        }

        private void OnViewportHeightChanged(object sender, EventArgs e)
        {
            RefreshDiffViewModelPositions();
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            RefreshDiffViewModelPositions();
        }

        public ObservableCollection<DiffViewModel> DiffViewModels
        {
            get
            {
                return _diffViewModels;
            }
        }

        public RelayCommand<DiffViewModel> PreviousChangeCommand
        {
            get
            {
                return _previousChangeCommand;
            }
        }

        public RelayCommand<DiffViewModel> NextChangeCommand
        {
            get
            {
                return _nextChangeCommand;
            }
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
            var diffViewModel = DiffViewModels[diffViewModelIndex];
            var diffLine = _textView.TextSnapshot.GetLineFromLineNumber(diffViewModel.LineNumber);
            currentDiffViewModel.ShowPopup = false;

            _textView.VisualElement.Focus();
            _textView.Caret.MoveTo(diffLine.Start);
            _textView.Caret.EnsureVisible();
        }

        private void RefreshDiffViewModelPositions()
        {
            foreach (var diffViewModel in DiffViewModels)
                diffViewModel.RefreshPosition();
        }

        private void HandleParseComplete(object sender, ParseResultEventArgs e)
        {
            _margin.Dispatcher.BeginInvoke((Action)(() =>
            {
                DiffViewModels.Clear();

                DiffParseResultEventArgs diffResult = e as DiffParseResultEventArgs;
                if (diffResult == null)
                    return;

                foreach (HunkRangeInfo hunkRangeInfo in diffResult.Diff)
                    DiffViewModels.Add(new DiffViewModel(_margin, hunkRangeInfo, _textView));
            }));
        }
    }
}
