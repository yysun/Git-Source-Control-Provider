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
    /// Interaction logic for CommitTag.xaml
    /// </summary>
    public partial class CommitTag : UserControl
    {
        public CommitTag()
        {
            InitializeComponent();
        }

        private void DeleteTag_Click(object sender, RoutedEventArgs e)
        {
            dynamic tag = this.DataContext;

            if (MessageBox.Show("Are you sure you want to delete tag: " + tag.Name,
                "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var ret = GitViewModel.Current.DeleteTag(tag.Name);

                if (!string.IsNullOrWhiteSpace(ret))
                    HistoryViewCommands.ShowMessage.Execute(new { Message = ret, Error = true }, this);

                //if(!string.IsNullOrWhiteSpace(ret)) MessageBox.Show(ret);

            }
        }
    }
}
