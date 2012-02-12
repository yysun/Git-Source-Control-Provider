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
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			GitBash.GitExePath = GitSccOptions.Current.GitBashPath;

			if (!GitBash.Exists) GitBash.GitExePath = TryFindFile(new string[] {
					@"C:\Program Files\Git\bin\sh.exe",
					@"C:\Program Files (x86)\Git\bin\sh.exe",
			});

			this.gitViewModel =
			this.bottomToolBar.GitViewModel = GitViewModel.Current;

			if (gitViewModel.Tracker.HasGitRepository)
				this.Title = gitViewModel.Tracker.GitWorkingDirectory;

			this.gitViewModel.GraphChanged += (o, _) =>
			{
				Action act = () => 
				{
					if (gitViewModel.Tracker.HasGitRepository) 
						this.Title = gitViewModel.Tracker.GitWorkingDirectory;
					this.graph.Show(gitViewModel.Tracker, false);
				};
				this.Dispatcher.BeginInvoke(act);
			};

			this.graph.Show(gitViewModel.Tracker, true);
		}

		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.F5)
			{
				HistoryViewCommands.RefreshGraph.Execute(null, this);
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

		#region toolbars
		private void rootGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)        
		{
			if (this.topToolBar.Visibility == Visibility.Collapsed)
			{
				ShowTopToolBar();
				//ShowBottomToolBarBar();
			}
			else
			{
				//HideTopToolBar();
				HideBottomToolBar();
			}
		}

		private void Grid_PreviewMouseMove(object sender, MouseEventArgs e)
		{
			//var y = e.GetPosition(rootGrid).Y;
			//if (y < 60)
			//{
			//    ShowTopToolBar();
			//}
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
		#endregion

		#region show commit details

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
					this.details.Show(this.gitViewModel.Tracker, id);
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
				Log.WriteLine("MainWindow.OpenCommitDetails_Executed: {0}", ex.ToString());
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
				Log.WriteLine("MainWindow.CloseCommitDetails_Executed: {0}", ex.ToString());
			}
		}

		private void ScrollToCommit_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			try
			{
				this.graph.ScrollToCommit(e.Parameter as string);
			}
			catch (Exception ex)
			{
				Log.WriteLine("MainWindow.ScrollToCommit_Executed: {0}", ex.ToString());
			}
		}

		private void GraphLoaded_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			//var animationDuration = TimeSpan.FromSeconds(5);
			//var animation = new DoubleAnimation(0.8, new Duration(animationDuration));
			////animation.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseIn };
			//animation.Completed += (o, _) =>
			//{
			//    this.loading.Visibility = Visibility.Collapsed;
			//    this.loading.Opacity = 1;
			//};
			//this.loading.BeginAnimation(UIElement.OpacityProperty, animation);

			gitViewModel.DisableAutoRefresh();

			this.loading.Visibility = Visibility.Collapsed;
			this.topToolBar.GitViewModel = gitViewModel;
			this.Title = gitViewModel.Tracker.HasGitRepository ?
				string.Format("{0} ({1})", gitViewModel.Tracker.GitWorkingDirectory, gitViewModel.Tracker.CurrentBranch) :
				string.Format("{0} (No Repository)", gitViewModel.WorkingDirectory);

			gitViewModel.EnableAutoRefresh();
		}

		private void RefreshGraph_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			this.loading.Visibility = Visibility.Visible;
			gitViewModel.Refresh(true);
			this.graph.Show(gitViewModel.Tracker, true);
		}
		
		#endregion

		#region select and comapre commits
		
		private void SelectCommit_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			this.topToolBar.SelectCommit(e.Parameter as string, null);
		}

		private void CompareCommits_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			try
			{
				this.details.RenderTransform.SetValue(TranslateTransform.XProperty, this.ActualWidth);
				this.details.Visibility = Visibility.Visible;
				var animationDuration = TimeSpan.FromSeconds(.5);
				var animation = new DoubleAnimation(0, new Duration(animationDuration));
				animation.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut };

				loading.Visibility = Visibility.Visible;
				animation.Completed += (_, x) =>
				{
					var ids = e.Parameter as string[];
					this.details.Show(this.gitViewModel.Tracker, ids[0], ids[1]);
					loading.Visibility = Visibility.Collapsed;
				};

				this.details.RenderTransform.BeginAnimation(TranslateTransform.XProperty, animation);
			}
			catch (Exception ex)
			{
				Log.WriteLine("MainWindow.CompareCommits_Executed {0}", ex.ToString());
			}
		}

		#endregion    

		private void Window_Drop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				this.Activate();

				var dropped = ((string[])e.Data.GetData(DataFormats.FileDrop, true))[0];

				if (!Directory.Exists(dropped)) dropped = Path.GetDirectoryName(dropped);
				if (Directory.Exists(dropped) && GitFileStatusTracker.GetRepositoryDirectory(dropped) != null &&
					MessageBox.Show("Do you want to open Git repository from " + dropped,
					"Git repository found", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
				{
					this.gitViewModel.Open(dropped);
					this.graph.Show(gitViewModel.Tracker, true);
				}
			}
		}	
	}
}
