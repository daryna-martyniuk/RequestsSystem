using Requests.Data;
using Requests.Data.Models;
using System.Linq;
using System.Windows;

namespace Requests.UI.Views
{
    public partial class SelectDepartmentWindow : Window
    {
        public Department SelectedDepartment { get; private set; }

        public SelectDepartmentWindow()
        {
            InitializeComponent();
            LoadDepartments();
        }

        private void LoadDepartments()
        {
            using (var ctx = new AppDbContext())
            {
                DepartmentsCombo.ItemsSource = ctx.Departments.ToList();
            }
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            if (DepartmentsCombo.SelectedItem is Department dept)
            {
                SelectedDepartment = dept;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Оберіть відділ!", "Помилка");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}