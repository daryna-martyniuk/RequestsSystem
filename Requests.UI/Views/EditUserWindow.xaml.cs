using Requests.Data.Models;
using Requests.Services;
using Requests.UI.ViewModels;
using System.Windows;

namespace Requests.UI.Views
{
    public partial class EditUserWindow : Window
    {
        public EditUserWindow(User user, AdminService service, int adminId)
        {
            InitializeComponent();

            var viewModel = new EditUserViewModel(user, service, adminId, (result) =>
            {
                this.DialogResult = result;
                this.Close();
            });

            this.DataContext = viewModel;
        }
    }
}