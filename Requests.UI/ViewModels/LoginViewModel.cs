using Requests.Data;
using Requests.Repositories.Implementations;
using Requests.Repositories.Interfaces; 
using Requests.Services;
using Requests.UI.Views;
using System.Windows;
using System.Windows.Input;

namespace Requests.UI.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private string _username;
        private string _password; 
        private string _errorMessage;

        private readonly AuthService _authService;

        public LoginViewModel()
        {
            var context = new AppDbContext();
            var userRepo = new UserRepository(context);
            var auditRepo = new Repository<Requests.Data.Models.AuditLog>(context);

            _authService = new AuthService(userRepo, auditRepo);

            LoginCommand = new RelayCommand(ExecuteLogin);
        }

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public ICommand LoginCommand { get; }

        private void ExecuteLogin(object parameter)
        {
            var passwordBox = parameter as System.Windows.Controls.PasswordBox;
            var password = passwordBox?.Password;

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Введіть логін та пароль!";
                return;
            }

            var user = _authService.Login(Username, password);

            if (user != null)
            {
                if (user.IsSystemAdmin)
                {
                    var adminWindow = new AdminWindow(user);
                    adminWindow.Show();
                }
                else if (user.Position.Name == "Директор")
                {
                    MessageBox.Show("Вікно директора ще в розробці");
                }
                else
                {
                    MessageBox.Show($"Вітаємо, {user.FullName}! Ваше вікно в розробці.");
                }

                Application.Current.Windows[0].Close();
            }
            else
            {
                ErrorMessage = "Невірний логін або пароль!";
            }
        }
    }
}