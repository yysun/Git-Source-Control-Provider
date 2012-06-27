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
using NGit;
using NGit.Api;
using GitScc.DataServices;

namespace GitScc.UI
{
    /// <summary>
    /// Interaction logic for BranchPicker.xaml
    /// </summary>
    public partial class BranchPicker : UserControl
    {
        private Window window;
        private Repository repository;
        private IList<DataServices.Ref> list;

        public string BranchName { get; set; }
        public bool CreateNew { get; set; }

        public BranchPicker(Repository repository, IList<DataServices.Ref> list)
        {
            InitializeComponent();
            this.repository = repository;
            this.list = list;
        }

        internal bool? Show()
        {
            window = new Window
            {
                Title = "Switch (checkout) branch",
                Content = this,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = System.Windows.ResizeMode.NoResize,
                Width = 350,
                Height = 200
            };

            comboBranches.ItemsSource = list.Where(r=>r.Type == RefTypes.Branch).Select(r => r.Name);
            comboBranches.SelectedValue = repository.GetBranch();
            return window.ShowDialog(); 
        }

        private void comboBranches_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            radioButton1.IsChecked = true;
            btnOK.IsEnabled = true;
        }

        private void txtNewBranch_GotFocus(object sender, RoutedEventArgs e)
        {
            radioButton2.IsChecked = true;
            btnOK.IsEnabled = txtNewBranch.Text.Length > 0;
        }

        private void txtNewBranch_TextChanged(object sender, TextChangedEventArgs e)
        {
            btnOK.IsEnabled = txtNewBranch.Text.Length > 0;
        }
        
        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Git git = new Git(this.repository);
                
                git.Checkout().SetName(radioButton1.IsChecked == true ? 
                        comboBranches.SelectedValue.ToString() : txtNewBranch.Text)
                    .SetCreateBranch(radioButton2.IsChecked == true)
                    .Call();

                window.DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

    }
}
