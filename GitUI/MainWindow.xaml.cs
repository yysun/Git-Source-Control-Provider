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

			if (gitViewModel.Tacker.HasGitRepository)
				this.Title = gitViewModel.Tacker.GitWorkingDirectory;

			this.gitViewModel.GraphChanged += (o, _) =>
			{
				Action act = () => 
				{
					if (gitViewModel.Tacker.HasGitRepository) 
						this.Title = gitViewModel.Tacker.GitWorkingDirectory;
					this.graph.Show(gitViewModel.Tacker, false);
				};
				this.Dispatcher.BeginInvoke(act);
			};

			this.graph.Show(gitViewModel.Tacker, true);
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
		#endregion

		#region commands

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

			this.loading.Visibility = Visibility.Collapsed;
			this.topToolBar.GitViewModel = gitViewModel;
			this.Title = gitViewModel.Tacker.HasGitRepository ?
				string.Format("{0} ({1})", gitViewModel.Tacker.GitWorkingDirectory, gitViewModel.Tacker.CurrentBranch) :
				string.Format("{0} (No Repository)", gitViewModel.WorkingDirectory);
		}

		private void RefreshGraph_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			this.loading.Visibility = Visibility.Visible;
			gitViewModel.Refresh();
			this.graph.Show(gitViewModel.Tacker, true);
		}
		
		#endregion

		#region select commit command
/*
		private void SelectCommit_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			try
			{
				var commit = e.Parameter as string;
				if (this.selectedCommits.Contains(commit))
					selectedCommits.Remove(commit);
				else
					this.selectedCommits.Add(commit);

				SetSelectedCommitCount();
			}
			catch (Exception ex)
			{
				ShowStatusMessage(ex.Message);
				Log.WriteLine("History Tool Window - SelectCommit_Executed: {0}", ex.ToString());
			}
		}

		private void btnCompare_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				this.details.RenderTransform.SetValue(TranslateTransform.XProperty, this.ActualWidth);
				this.details.Visibility = Visibility.Visible;
				var animationDuration = TimeSpan.FromSeconds(.5);
				var animation = new DoubleAnimation(0, new Duration(animationDuration));
				animation.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut };
				this.details.RenderTransform.BeginAnimation(TranslateTransform.XProperty, animation);

				this.details.Show(this.tracker, this.selectedCommits[0], this.selectedCommits[1]);
			}
			catch (Exception ex)
			{
				ShowStatusMessage(ex.Message);
				Log.WriteLine("History Tool Window - btnCompare_Click: {0}", ex.ToString());
			}
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
*/
		#endregion    
	
	}
}
