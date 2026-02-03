using Requests.Data.Models;
using Requests.Services;
using Requests.UI.Views;
using System.Windows;
using System.Windows.Input;

namespace Requests.UI.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly User _currentUser;

        // ViewModels
        private readonly MyWorkspaceViewModel _workspaceViewModel;
        private readonly AdminViewModel _adminViewModel;
        private readonly DepartmentStatsViewModel _deptStatsViewModel;
        private readonly GlobalStatsViewModel _globalStatsViewModel; // Для Директора

        private object _currentView;
        public object CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        public string UserName => _currentUser.FullName;
        public string UserRole => _currentUser.Position.Name;

        public Visibility IsAdminVisible => _currentUser.IsSystemAdmin ? Visibility.Visible : Visibility.Collapsed;

        // Керівник або Директор
        public Visibility IsManagerVisible =>
            (_currentUser.Position.Name == ServiceConstants.PositionHead ||
             _currentUser.Position.Name == ServiceConstants.PositionDirector ||
             _currentUser.Position.Name == ServiceConstants.PositionDeputyHead ||
             _currentUser.Position.Name == ServiceConstants.PositionDeputyDirector)
            ? Visibility.Visible : Visibility.Collapsed;

        public Visibility IsDirectorVisible =>
             (_currentUser.Position.Name == ServiceConstants.PositionDirector ||
              _currentUser.Position.Name == ServiceConstants.PositionDeputyDirector)
             ? Visibility.Visible : Visibility.Collapsed;

        public ICommand ShowWorkspaceCommand { get; }
        public ICommand ShowUsersCommand { get; }
        public ICommand ShowStructureCommand { get; }
        public ICommand ShowLogsCommand { get; }
        public ICommand ShowDepartmentStatsCommand { get; }
        public ICommand ShowGlobalStatsCommand { get; }
        public ICommand EditProfileCommand { get; }
        public ICommand LogoutCommand { get; }

        public DashboardViewModel(User currentUser)
        {
            _currentUser = currentUser;

            // Ініціалізація сервісів через Factory App
            var empService = App.CreateEmployeeService();
            var manService = App.CreateManagerService();
            var dirService = App.CreateDirectorService();
            var repService = App.CreateReportService();
            var admService = App.CreateAdminService();

            // Створення ViewModels
            _workspaceViewModel = new MyWorkspaceViewModel(currentUser, empService);

            if (currentUser.IsSystemAdmin)
                _adminViewModel = new AdminViewModel(currentUser);

            // ВИПРАВЛЕНО: Додано передачу EmployeeService
            _deptStatsViewModel = new DepartmentStatsViewModel(manService, repService, empService, currentUser);

            // Створюємо GlobalStatsViewModel тільки для директорів
            if (IsDirectorVisible == Visibility.Visible)
                _globalStatsViewModel = new GlobalStatsViewModel(dirService, repService, empService, currentUser);

            // Команди навігації
            ShowWorkspaceCommand = new RelayCommand(o => CurrentView = _workspaceViewModel);

            ShowUsersCommand = new RelayCommand(o => { if (_adminViewModel != null) { _adminViewModel.SwitchViewCommand.Execute("Users"); CurrentView = _adminViewModel; } });
            ShowStructureCommand = new RelayCommand(o => { if (_adminViewModel != null) { _adminViewModel.SwitchViewCommand.Execute("Structure"); CurrentView = _adminViewModel; } });
            ShowLogsCommand = new RelayCommand(o => { if (_adminViewModel != null) { _adminViewModel.SwitchViewCommand.Execute("Logs"); CurrentView = _adminViewModel; } });

            ShowDepartmentStatsCommand = new RelayCommand(o => CurrentView = _deptStatsViewModel);
            ShowGlobalStatsCommand = new RelayCommand(o => CurrentView = _globalStatsViewModel);

            EditProfileCommand = new RelayCommand(EditProfile);
            LogoutCommand = new RelayCommand(Logout);

            CurrentView = _workspaceViewModel;
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
                if (window.DataContext == this) { window.Close(); break; }
            }
        }
    }
}