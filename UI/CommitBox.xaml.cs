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

        public bool Selected { get; set; }

        public CommitBox()
        {
            InitializeComponent();
        }

        private void root_MouseUp(object sender, MouseButtonEventArgs e)
        {
            HistoryViewCommands.OpenCommitDetails.Execute(this.txtId.Text, null);
        }

        private void txtComment_MouseEnter(object sender, MouseEventArgs e)
        {
            VisualStateManager.GoToElementState(this.root, "MouseOverState", true);
        }

        private void txtComment_MouseLeave(object sender, MouseEventArgs e)
        {
            VisualStateManager.GoToElementState(this.root, "NormalState", true);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Selected = !this.Selected;
            VisualStateManager.GoToElementState(this.root, this.Selected ? "SelectedSate" : "NotSelectedState", true);
            HistoryViewCommands.SelectCommit.Execute(this.txtId.Text, null);
            e.Handled = true;
        }
    }
}
