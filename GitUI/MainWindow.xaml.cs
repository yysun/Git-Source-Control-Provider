using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using GitScc;
using System.Windows.Media.Animation;

namespace GitUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private GitViewModel gitViewModel;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.gitViewModel =
            this.graph.GitViewModel =
            this.topToolBar.GitViewModel = GitViewModel.Current;

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
            this.graph.ExportGraph();
        }

        private void rootGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ShowTopToolBar();
            ShowBottomToolBarBar();
        }

        private void Grid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            var y = e.GetPosition(rootGrid).Y;
            //System.Diagnostics.Debug.WriteLine(y);
            if (y < 60)
            {
                ShowTopToolBar();
            }
            else if (y > 60)
            {
                HideTopToolBar();
            }
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
            if (this.bottomToolBar.Visibility == Visibility.Collapsed)
            {
                this.bottomToolBar.Visibility = Visibility.Visible;
                this.bottomToolBar.RenderTransform.SetValue(TranslateTransform.YProperty, 60.0);
                this.bottomToolBar.Visibility = Visibility.Visible;
                var animationDuration = TimeSpan.FromSeconds(1.0);
                var animation = new DoubleAnimation(0, new Duration(animationDuration));
                animation.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut };
                this.bottomToolBar.RenderTransform.BeginAnimation(TranslateTransform.YProperty, animation);
            }
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

    }
}
