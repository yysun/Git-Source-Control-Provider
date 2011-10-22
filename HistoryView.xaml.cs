using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GitScc.DataServices;

namespace GitScc
{
    /// <summary>
    /// Interaction logic for HistoryView.xaml
    /// </summary>
    public partial class HistoryView : UserControl
    {
        HistoryToolWindow toolWindow;
        private GitFileStatusTracker tracker;
        private ObservableCollection<string> selectedCommits;

        public HistoryView(HistoryToolWindow toolWindow)
        {
            InitializeComponent();
            this.toolWindow = toolWindow;
            this.details.toolWindow = toolWindow;
            this.selectedCommits = new ObservableCollection<string>();
        }

        public void InsertNewEditor(object editor)
        {
            //diffEditorHost.Content = editor;
        }

        DateTime lastTimeRefresh = DateTime.Now.AddDays(-1);

        internal void Refresh(GitFileStatusTracker tracker)
        {
            try
            {
                this.branchList.ItemsSource = null;
                this.tagList.ItemsSource = null;

                CloseCommitDetails_Executed(this, null);
                selectedCommits.Clear();
                SetSelectedCommitCount();
                this.HistoryGraph.Show(tracker);

                this.tracker = tracker;
                if (tracker != null)
                {
                    double delta = DateTime.Now.Subtract(lastTimeRefresh).TotalMilliseconds;
                    if (delta < 1000) return; //no refresh within 1 second

                    this.branchList.ItemsSource = tracker.RepositoryGraph.Refs
                        .Where(r => r.Type == RefTypes.Branch)
                        .Select(r => r.Name);

                    this.tagList.ItemsSource = tracker.RepositoryGraph.Refs
                        .Where(r => r.Type == RefTypes.Tag)
                        .Select(r => r.Name);

                    lastTimeRefresh = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine("History View Refresh: {0}", ex.ToString());
            }
        }

        private void OpenFile(string fileName)
        {
            this.toolWindow.SetDisplayedFile(fileName);
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.DefaultExt = ".xps";
            dlg.Filter = "XPS documents (.xps)|*.xps";
            if (dlg.ShowDialog() == true)
            {
                this.HistoryGraph.SaveToFile(dlg.FileName);
            }
        }

        private void checkBox1_Click(object sender, RoutedEventArgs e)
        {
            this.HistoryGraph.SetSimplified(checkBox1.IsChecked==true);
            this.lableView.Content = checkBox1.IsChecked == true ? "Simplified view: ON" : "Simplified view: OFF";
            this.selectedCommits.Clear();
            SetSelectedCommitCount();
        }

        private void branchList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var listbox = sender as ListBox;
            Point clickPoint = e.GetPosition(listbox);
            var element = listbox.InputHitTest(clickPoint) as TextBlock;

            if(element!=null) ShowCommitDetails(element.Text);
        }

        private void ShowCommitDetails(string id)
        {
            if (id != null)
            {
                this.details.RenderTransform.SetValue(TranslateTransform.XProperty, this.ActualWidth);
                this.details.Visibility = Visibility.Visible;
                var animationDuration = TimeSpan.FromSeconds(.5);
                var animation = new DoubleAnimation(0, new Duration(animationDuration));
                animation.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut };
                this.details.RenderTransform.BeginAnimation(TranslateTransform.XProperty, animation);

                this.details.Show(this.tracker, id);
            }
        }

        private void CloseCommitDetails_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var animationDuration = TimeSpan.FromSeconds(.2);
            var animation = new DoubleAnimation(this.ActualWidth+100, new Duration(animationDuration));
            animation.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseIn };
            animation.Completed += (o, _) => this.details.Visibility = Visibility.Hidden;
            this.details.RenderTransform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private void OpenCommitDetails_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ShowCommitDetails(e.Parameter as string);
        }

        private void SelectCommit_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var commit = e.Parameter as string;
            if (this.selectedCommits.Contains(commit)) 
                selectedCommits.Remove(commit);
            else
                this.selectedCommits.Add(commit);

            SetSelectedCommitCount();
        }

        private void btnCompare_Click(object sender, RoutedEventArgs e)
        {
            this.details.RenderTransform.SetValue(TranslateTransform.XProperty, this.ActualWidth);
            this.details.Visibility = Visibility.Visible;
            var animationDuration = TimeSpan.FromSeconds(.5);
            var animation = new DoubleAnimation(0, new Duration(animationDuration));
            animation.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut };
            this.details.RenderTransform.BeginAnimation(TranslateTransform.XProperty, animation);

            this.details.Show(this.tracker, this.selectedCommits[0], this.selectedCommits[1]);
        }

        private void SetSelectedCommitCount()
        {
            this.btnCompare.IsEnabled = this.selectedCommits.Count() == 2;
            this.btnCommitCount.Visibility = this.selectedCommits.Count() > 0 ?
                Visibility.Visible : Visibility.Collapsed;
            this.btnCommitCount.Content = this.selectedCommits.Count().ToString();
        }

        private void btnCommitCount_Click(object sender, RoutedEventArgs e)
        {
            //selectedCommits.Clear();
            //SetSelectedCommitCount();
        }
    }

    public static class HistoryViewCommands
    {
        public static readonly RoutedUICommand CloseCommitDetails = new RoutedUICommand("CloseCommitDetails", "CloseCommitDetails", typeof(HistoryView));
        public static readonly RoutedUICommand OpenCommitDetails = new RoutedUICommand("OpenCommitDetails", "OpenCommitDetails", typeof(HistoryView));
        public static readonly RoutedUICommand SelectCommit = new RoutedUICommand("SelectCommit", "SelectCommit", typeof(HistoryView));
    
    }
}
