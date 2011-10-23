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
            if (this.textBlock.Text == "HEAD")
            {
                this.border.Background = this.border.BorderBrush = 
                this.polygon.Fill = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));
                this.textBlock.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            }
        }
    }
}
