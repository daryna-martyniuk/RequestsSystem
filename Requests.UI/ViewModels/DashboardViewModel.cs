using Requests.Data;
using Requests.Data.Models;
using Requests.Repositories.Implementations;
using Requests.Services;
using Requests.UI.Views;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Requests.UI.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly EmployeeService _employeeService;
        private readonly User _currentUser;

        // Поточна активна вкладка
        private object _currentView;
        public object CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        // === ВИДИМІСТЬ КНОПОК МЕНЮ (НА ОСНОВІ РОЛІ) ===

        // Всім доступно
        public Visibility IsWorkspaceVisible => Visibility.Visible;

        // Тільки Адмін
        public Visibility IsAdminVisible => _currentUser.IsSystemAdmin ? Visibility.Visible : Visibility.Collapsed;

        // Керівник або Директор
        public Visibility IsManagerVisible =>
            (_currentUser.Position.Name == ServiceConstants.PositionHead ||
             _currentUser.Position.Name == ServiceConstants.PositionDirector)
            ? Visibility.Visible : Visibility.Collapsed;

        // Тільки Директор
        public Visibility IsDirectorVisible =>
            _currentUser.Position.Name == ServiceConstants.PositionDirector
            ? Visibility.Visible : Visibility.Collapsed;


        // === КОМАНДИ НАВІГАЦІЇ ===
        public ICommand ShowWorkspaceCommand { get; }      // "Дашборд / Мій кабінет"

        // Адмінські вкладки
        public ICommand ShowUsersCommand { get; }          // "Користувачі" (частина AdminView)
        public ICommand ShowStructureCommand { get; }      // "Структура" (частина AdminView)
        public ICommand ShowLogsCommand { get; }           // "Логи" (частина AdminView)

        // Директорські вкладки
        public ICommand ShowAllRequestsCommand { get; }    // "Всі запити"
        public ICommand ShowStatsCommand { get; }          // "Статистика"

        // Загальні
        public ICommand EditProfileCommand { get; }
        public ICommand LogoutCommand { get; }

        public string UserName => _currentUser.FullName;
        public string UserRole => _currentUser.Position.Name;

        public DashboardViewModel(User currentUser)
        {
            _currentUser = currentUser;

            // Ініціалізація сервісів
            var context = new AppDbContext();
            var reqRepo = new RequestRepository(context);
            var statusRepo = new Repository<RequestStatus>(context);
            var commentRepo = new Repository<RequestComment>(context);
            var attachRepo = new Repository<RequestAttachment>(context);
            var auditRepo = new Repository<AuditLog>(context);

            _employeeService = new EmployeeService(reqRepo, statusRepo, commentRepo, attachRepo, auditRepo);

            // === НАЛАШТУВАННЯ НАВІГАЦІЇ ===

            // 1. Дашборд (для всіх)
            ShowWorkspaceCommand = new RelayCommand(o => CurrentView = new MyWorkspaceViewModel(_currentUser, _employeeService));

            // 2. Адмінські вкладки (Ми відкриваємо AdminView, але передаємо параметр, яку саме під-вкладку відкрити)
            // Примітка: Щоб це працювало ідеально, AdminViewModel має вміти приймати стартову вкладку.
            // Поки що просто відкриваємо AdminView (де за замовчуванням "Користувачі").
            ShowUsersCommand = new RelayCommand(o => {
                var adminVM = new AdminViewModel(_currentUser);
                // adminVM.SwitchView("Users"); // Можна додати такий метод в AdminVM
                CurrentView = adminVM;
            });

            ShowStructureCommand = new RelayCommand(o => {
                var adminVM = new AdminViewModel(_currentUser);
                // Тут ми трохи "хачимо": імітуємо натискання кнопки перемикання всередині VM
                adminVM.SwitchViewCommand.Execute("Structure");
                CurrentView = adminVM;
            });

            ShowLogsCommand = new RelayCommand(o => {
                var adminVM = new AdminViewModel(_currentUser);
                adminVM.SwitchViewCommand.Execute("Logs");
                CurrentView = adminVM;
            });

            // 3. Директорські вкладки (Заглушки)
            ShowAllRequestsCommand = new RelayCommand(o => MessageBox.Show("Всі запити (TODO)"));
            ShowStatsCommand = new RelayCommand(o => MessageBox.Show("Статистика (TODO)"));

            EditProfileCommand = new RelayCommand(EditProfile);
            LogoutCommand = new RelayCommand(Logout);

            // Старт з Workspace
            CurrentView = new MyWorkspaceViewModel(_currentUser, _employeeService);
        }

        private void EditProfile(object obj)
        {
            var profileWindow = new EditProfileWindow(_currentUser);
            profileWindow.ShowDialog();
            OnPropertyChanged(nameof(UserName));
        }

        private void Logout(object obj)
        {
            new LoginWindow().Show();
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.Close();
                    break;
                }
            }
        }
    }
}