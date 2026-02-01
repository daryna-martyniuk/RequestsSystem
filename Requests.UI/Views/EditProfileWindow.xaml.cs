using Requests.Data.Models;
using Requests.Services;
using Requests.UI.ViewModels;
using System.Windows;

namespace Requests.UI.Views
{
    public partial class EditProfileWindow : Window
    {
        public EditProfileWindow(User user)
        {
            InitializeComponent();

            // Використовуємо фабрики з App.xaml.cs
            // Це набагато чистіше, ніж створювати контекст і репозиторії тут
            var authService = App.CreateAuthService();
            var adminService = App.CreateAdminService();

            var vm = new EditProfileViewModel(user, authService, adminService, (result) =>
            {
                DialogResult = result;
                Close();
            });

            DataContext = vm;
        }
    }
}