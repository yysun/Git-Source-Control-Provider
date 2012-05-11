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
using Microsoft.VisualBasic;
using GitScc.DataServices;

namespace GitScc.UI
{
    /// <summary>
    /// Interaction logic for CommitBox.xaml
    /// </summary>
    public partial class CommitBox : UserControl
    {
        // need to match the size of top grid
        internal const int HEIGHT = 120;
        internal const int WIDTH = 200;

        private bool selected;
        public bool Selected
        {
            get { return selected; }
            set
            {
                this.selected = value;
                VisualStateManager.GoToElementState(this.root, this.selected ? "SelectedSate" : "NotSelectedState", true);
            }
        }

        public CommitBox()
        {
            InitializeComponent();
        }

        private void root_MouseEnter(object sender, MouseEventArgs e)
        {
            VisualStateManager.GoToElementState(this.root, "MouseOverState", true);
        }

        private void root_MouseLeave(object sender, MouseEventArgs e)
        {
            VisualStateManager.GoToElementState(this.root, "NormalState", true);
        }

        private void root_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //this.Selected = !this.Selected;
            HistoryViewCommands.OpenCommitDetails.Execute(this.txtId.Text, null);
        }

        private void NewTag_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                dynamic commit = this.DataContext;
                var text = string.Format("{0}\r\n\r\n{1}\r\n\r\n{2}, {3}",
                    commit.ShortId, commit.Comments, commit.Author, commit.Date);

                string tag = Interaction.InputBox(text, "git tag", "");

                if (string.IsNullOrWhiteSpace(tag)) return;

                var tag1 = ((Ref[])commit.Refs).Where(r => r.Type == RefTypes.Tag
                    && r.Name == tag).FirstOrDefault();
                if (tag1 != null && tag1.Id.StartsWith(commit.ShortId)) return;

                string tagId = GitViewModel.Current.GetTagId(tag);

                if (!string.IsNullOrWhiteSpace(tagId))
                {
                    MessageBox.Show("Tag already exists for " + tagId, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    var ret = GitViewModel.Current.AddTag(tag, commit.ShortId);
                    if (!string.IsNullOrWhiteSpace(ret))
                        HistoryViewCommands.ShowMessage.Execute(new { Message = ret, Error = true }, this);

                    //if(!string.IsNullOrWhiteSpace(ret)) 
                    //    MessageBox.Show(ret, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NewBranch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                dynamic commit = this.DataContext;
                var text = string.Format("{0}\r\n\r\n{1}\r\n\r\n{2}, {3}",
                    commit.ShortId, commit.Comments, commit.Author, commit.Date);

                string branch = Interaction.InputBox(text, "git branch", "");

                if (string.IsNullOrWhiteSpace(branch)) return;

                var branch1 = ((Ref[])commit.Refs).Where(r => r.Type == RefTypes.Branch
                    && r.Name == branch).FirstOrDefault();
                if (branch1 != null && branch1.Id.StartsWith(commit.ShortId)) return;

                string branchId = GitViewModel.Current.GetBranchId(branch);

                if (!string.IsNullOrWhiteSpace(branchId))
                {
                    MessageBox.Show("Branch already exists for " + branchId, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    var ret = GitViewModel.Current.AddBranch(branch, commit.ShortId);
                    if (!string.IsNullOrWhiteSpace(ret))
                        HistoryViewCommands.ShowMessage.Execute(new { Message = ret, Error = true }, this);

                    //if (!string.IsNullOrWhiteSpace(ret))
                    //    MessageBox.Show(ret, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            HistoryViewCommands.SelectCommit.Execute(this.txtId.Text, this);
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.DefaultExt = ".zip";
            dlg.Filter = "Archive (.zip)|*.zip";
            dlg.FileName = this.txtId.Text + ".zip";
            if (dlg.ShowDialog() == true)
            {
                var ret = GitViewModel.Current.Archive(this.txtId.Text, dlg.FileName);
                if (!string.IsNullOrWhiteSpace(ret))
                    HistoryViewCommands.ShowMessage.Execute(new { Message = ret, Error = true }, this);

                //if (!string.IsNullOrWhiteSpace(ret))
                //{
                //    MessageBox.Show(ret, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                //}
            }
        }
    }
}
