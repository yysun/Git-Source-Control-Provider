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
            this.toolBar.GitViewModel = GitViewModel.Current;

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

        private void Grid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            var y = e.GetPosition(rootGrid).Y;
            //System.Diagnostics.Debug.WriteLine(y);
            if (y < 60 && toolBar.Visibility == Visibility.Collapsed)
            {
                toolBar.Visibility = Visibility.Visible;
                this.toolBar.RenderTransform.SetValue(TranslateTransform.YProperty, -60.0);
                this.toolBar.Visibility = Visibility.Visible;
                var animationDuration = TimeSpan.FromSeconds(1.0);
                var animation = new DoubleAnimation(0, new Duration(animationDuration));
                animation.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut };
                this.toolBar.RenderTransform.BeginAnimation(TranslateTransform.YProperty, animation);

            }
            else if (y > 60 && toolBar.Visibility == Visibility.Visible)
            {
                var animationDuration = TimeSpan.FromSeconds(1.0);
                var animation = new DoubleAnimation(-60.0, new Duration(animationDuration));
                animation.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut };
                animation.Completed += (o, _) => this.toolBar.Visibility = Visibility.Collapsed;
                this.toolBar.RenderTransform.BeginAnimation(TranslateTransform.YProperty, animation);

            }
        }
    }
}
