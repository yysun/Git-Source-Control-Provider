using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GitScc;

namespace GitUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private GitViewModel gitViewModel;

        private string TryFindFile(string[] paths)
        {
            foreach (var path in paths)
            {
                if (File.Exists(path)) return path;
            }
            return null;
        }

        public MainWindow()
        {
            InitializeComponent();

            GitBash.GitExePath = TryFindFile(new string[] {
                    @"C:\Program Files\Git\bin\sh.exe",
                    @"C:\Program Files (x86)\Git\bin\sh.exe",
            });

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.gitViewModel =
            this.graph.GitViewModel =
            this.topToolBar.GitViewModel =
            this.bottomToolBar.GitViewModel = GitViewModel.Current;

            if (gitViewModel.Tacker.HasGitRepository)
                this.Title = gitViewModel.Tacker.GitWorkingDirectory;

            this.gitViewModel.GraphChanged += (o, _) =>
            {
                Action act = () => 
                {
                    if (gitViewModel.Tacker.HasGitRepository) 
                        this.Title = gitViewModel.Tacker.GitWorkingDirectory;

                    this.graph.GitViewModel = gitViewModel; 
                };
                this.Dispatcher.BeginInvoke(act, null);
            };
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                gitViewModel.Refresh();
            }
        }

        private void ExportGraph(object sender, ExecutedRoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.DefaultExt = ".xps";
            dlg.Filter = "XPS documents (.xps)|*.xps";
            if (dlg.ShowDialog() == true)
            {
                this.graph.SaveToFile(dlg.FileName);
            }
        }

        private void rootGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (this.bottomToolBar.Visibility == Visibility.Collapsed)
            {
                ShowTopToolBar();
                //ShowBottomToolBarBar();
            }
            else
            {
                HideTopToolBar();
                HideBottomToolBar();
            }
        }

        private void Grid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            var y = e.GetPosition(rootGrid).Y;
            if (y < 60)
            {
                ShowTopToolBar();
            }
            //else if (y > this.ActualHeight - 60)
            //{
            //    ShowBottomToolBarBar();
            //}
        }

        private void ShowTopToolBar()
        {
            if (this.topToolBar.Visibility == Visibility.Collapsed)
            {
                this.topToolBar.Visibility = Visibility.Visible;
                this.topToolBar.RenderTransform.SetValue(TranslateTransform.YProperty, -60.0);
                this.topToolBar.Visibility = Visibility.Visible;
                var animationDuration = TimeSpan.FromSeconds(1.0);
                var animation = new DoubleAnimation(0, new Duration(animationDuration));
                animation.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut };
                this.topToolBar.RenderTransform.BeginAnimation(TranslateTransform.YProperty, animation);
            }
        }

        private void HideTopToolBar()
        {
            if (this.topToolBar.Visibility == Visibility.Visible)
            {
                var animationDuration = TimeSpan.FromSeconds(1.0);
                var animation = new DoubleAnimation(-60.0, new Duration(animationDuration));
                animation.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut };
                animation.Completed += (o, _) => this.topToolBar.Visibility = Visibility.Collapsed;
                this.topToolBar.RenderTransform.BeginAnimation(TranslateTransform.YProperty, animation);
            }
        }

        private void ShowBottomToolBarBar()
        {
            //if (this.bottomToolBar.Visibility == Visibility.Collapsed)
            //{
            //    this.bottomToolBar.Visibility = Visibility.Visible;
            //    this.bottomToolBar.RenderTransform.SetValue(TranslateTransform.YProperty, 60.0);
            //    this.bottomToolBar.Visibility = Visibility.Visible;
            //    var animationDuration = TimeSpan.FromSeconds(1.0);
            //    var animation = new DoubleAnimation(0, new Duration(animationDuration));
            //    animation.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut };
            //    this.bottomToolBar.RenderTransform.BeginAnimation(TranslateTransform.YProperty, animation);
            //}
        }

        private void HideBottomToolBar()
        {
            if (this.bottomToolBar.Visibility == Visibility.Visible)
            {
                var animationDuration = TimeSpan.FromSeconds(1.0);
                var animation = new DoubleAnimation(60.0, new Duration(animationDuration));
                animation.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut };
                animation.Completed += (o, _) => this.bottomToolBar.Visibility = Visibility.Collapsed;
                this.bottomToolBar.RenderTransform.BeginAnimation(TranslateTransform.YProperty, animation);
            }
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
                loading.Visibility = Visibility.Visible;
                animation.Completed += (_, e) => 
                {
                    this.details.Show(this.gitViewModel.Tacker, id);
                    loading.Visibility = Visibility.Collapsed;
                };
                this.details.RenderTransform.BeginAnimation(TranslateTransform.XProperty, animation);
                //this.details.Show(this.gitViewModel.Tacker, id);
            }
        }

        private void OpenCommitDetails_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                ShowCommitDetails(e.Parameter as string);
            }
            catch (Exception ex)
            {

            }
        }
        private void CloseCommitDetails_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                var animationDuration = TimeSpan.FromSeconds(.2);
                var animation = new DoubleAnimation(this.ActualWidth + 200, new Duration(animationDuration));
                animation.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseIn };
                animation.Completed += (o, _) => this.details.Visibility = Visibility.Collapsed;
                this.details.RenderTransform.BeginAnimation(TranslateTransform.XProperty, animation);
            }
            catch (Exception ex)
            {
                
            }
        }
    }
}
