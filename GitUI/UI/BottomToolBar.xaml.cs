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

namespace GitUI.UI
{
    /// <summary>
    /// Interaction logic for BottomToolBar.xaml
    /// </summary>
    public partial class BottomToolBar : UserControl
    {
        private GitViewModel gitViewModel;
        internal GitViewModel GitViewModel
        {
            set
            {
                gitViewModel = value;
                //this.txtDirectory.Text = gitViewModel.Tacker.GitWorkingDirectory;
            }
        }
        public BottomToolBar()
        {
            InitializeComponent();
        }

        private void btnGo_Click(object sender, RoutedEventArgs e)
        {

        }


    }
}
