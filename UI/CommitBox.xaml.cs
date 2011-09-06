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

        public CommitBox()
        {
            InitializeComponent();
        }
    }
}
