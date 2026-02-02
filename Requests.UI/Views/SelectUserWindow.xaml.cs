using Requests.Data.Models;
using System.Collections.Generic;
using System.Windows;

namespace Requests.UI.Views
{
    public partial class SelectUserWindow : Window
    {
        public User SelectedUser { get; private set; }

        public SelectUserWindow(IEnumerable<User> employees)
        {
            InitializeComponent();
            EmployeesList.ItemsSource = employees;
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            if (EmployeesList.SelectedItem is User user)
            {
                SelectedUser = user;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Будь ласка, оберіть співробітника зі списку.", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}