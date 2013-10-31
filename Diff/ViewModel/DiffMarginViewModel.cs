namespace GitScc.Diff.ViewModel
{
    using System;
    using System.Collections.Generic;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.Command;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using ErrorHandler = Microsoft.VisualStudio.ErrorHandler;
    using TaskScheduler = System.Threading.Tasks.TaskScheduler;
    using DispatcherPriority = System.Windows.Threading.DispatcherPriority;

    public class DiffMarginViewModel : ViewModelBase
    {
        private readonly DiffMargin _margin;
        private readonly IWpfTextView _textView;
        private readonly IGitCommands _gitCommands;
        private readonly DiffUpdateBackgroundParser _parser;
        private readonly RelayCommand<DiffViewModel> _previousChangeCommand;
        private readonly RelayCommand<DiffViewModel> _nextChangeCommand;
        private List<DiffViewModel> _diffViewModels;

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
            _diffViewModels = new List<DiffViewModel>();
            _previousChangeCommand = new RelayCommand<DiffViewModel>(PreviousChange, PreviousChangeCanExecute);
            _nextChangeCommand = new RelayCommand<DiffViewModel>(NextChange, NextChangeCanExecute);

            _textView.LayoutChanged += OnLayoutChanged;

            _parser = new DiffUpdateBackgroundParser(textView.TextBuffer, textView.TextDataModel.DocumentBuffer, TaskScheduler.Default, textDocumentFactoryService, gitCommands);
            _parser.ParseComplete += HandleParseComplete;
            _parser.RequestParse(false);
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            RefreshDiffViewModelPositions(true, e);

            Action action = RefreshDiffViewModelPositions;
            _margin.VisualElement.Dispatcher.BeginInvoke(action, DispatcherPriority.ApplicationIdle);
        }

        public List<DiffViewModel> DiffViewModels
        {
            get
            {
                return _diffViewModels;
            }

            private set
            {
                _diffViewModels = value;
                RaisePropertyChanged(() => DiffViewModels);
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

        public override void Cleanup()
        {
            _parser.Dispose();
            base.Cleanup();
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
            try
            {
                RefreshDiffViewModelPositions(false, null);
            }
            catch (Exception ex)
            {
                if (ErrorHandler.IsCriticalException(ex))
                    throw;
            }
        }

        private void RefreshDiffViewModelPositions(bool approximate, TextViewLayoutChangedEventArgs e)
        {
            foreach (var diffViewModel in DiffViewModels)
                diffViewModel.RefreshPosition(approximate, e);
        }

        private void HandleParseComplete(object sender, ParseResultEventArgs e)
        {
            _margin.VisualElement.Dispatcher.BeginInvoke((Action)(() =>
            {
                try
                {
                    List<DiffViewModel> diffViewModels = new List<DiffViewModel>();
                    DiffParseResultEventArgs diffResult = e as DiffParseResultEventArgs;
                    if (diffResult != null)
                    {
                        foreach (HunkRangeInfo hunkRangeInfo in diffResult.Diff)
                            diffViewModels.Add(new DiffViewModel(_margin, hunkRangeInfo, _textView));
                    }

                    DiffViewModels = diffViewModels;
                }
                catch (Exception ex)
                {
                    if (ErrorHandler.IsCriticalException(ex))
                        throw;
                }
            }));
        }
    }
}
