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
using GitUI;

namespace GitScc.UI
{
    /// <summary>
    /// Interaction logic for CommitHead.xaml
    /// </summary>
    public partial class CommitHead : UserControl
    {
        public CommitHead()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (BranchName == "HEAD")
            {
                this.border.Background = this.border.BorderBrush =
                this.polygon.Fill = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));
                this.textBlock.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                this.menuCheckoutBranch.IsEnabled = this.menuDeleteBranch.IsEnabled = false;
            }
            //else
            //{
            //    this.menuCheckoutBranch.Header = "checkout branch: " + BranchName;
            //    this.menuDeleteBranch.Header =   "delete branch:  " + BranchName;
            //}
        }

        private string BranchName { get { return this.textBlock.Text; } }

        private void CheckoutBranch_Click(object sender, RoutedEventArgs e)
        {
            var ret = GitViewModel.Current.CheckoutBranch(BranchName);

            if (!string.IsNullOrWhiteSpace(ret))
                HistoryViewCommands.ShowMessage.Execute(new { Message = ret, Error = true }, this);

            //if (!string.IsNullOrWhiteSpace(ret)) MessageBox.Show(ret, "Git Checkout Result");
        }

        private void DeleteBranch_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to delete branch: " + BranchName,
                "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var ret = GitViewModel.Current.DeleteBranch(BranchName);
                
                if (ret.StartsWith("error"))
                    HistoryViewCommands.ShowMessage.Execute(new { Message = ret, Error = true }, this);
                else
                    HistoryViewCommands.ShowMessage.Execute(new { Message = ret, Error = false }, this);

                //if (ret.StartsWith("error"))
                //    MessageBox.Show(ret, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                //else
                //    MessageBox.Show(ret, "Branch Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
