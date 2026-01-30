using Requests.Data.Models;
using Requests.Services;
using Requests.UI.Views;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Requests.UI.ViewModels
{
    public class MyWorkspaceViewModel : ViewModelBase
    {
        private readonly EmployeeService _employeeService;
        private readonly User _currentUser;

        public ObservableCollection<Request> MyRequests { get; set; }

        public ICommand CreateRequestCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand OpenDetailsCommand { get; }

        public MyWorkspaceViewModel(User currentUser, EmployeeService employeeService)
        {
            _currentUser = currentUser;
            _employeeService = employeeService;

            CreateRequestCommand = new RelayCommand(CreateRequest);
            RefreshCommand = new RelayCommand(o => LoadData());

            // Команда відкриття деталей
            OpenDetailsCommand = new RelayCommand(OpenDetails);

            LoadData();
        }

        private void LoadData()
        {
            if (_currentUser != null && _employeeService != null)
            {
                var requests = _employeeService.GetMyRequests(_currentUser.Id);
                MyRequests = new ObservableCollection<Request>(requests);
                OnPropertyChanged(nameof(MyRequests));
            }
        }

        private void CreateRequest(object obj)
        {
            var createWindow = new Requests.UI.Views.CreateRequestWindow(_currentUser, _employeeService);
            if (createWindow.ShowDialog() == true)
            {
                LoadData();
            }
        }

        private void OpenDetails(object obj)
        {
            if (obj is Request req)
            {
                // Завантажуємо повну інформацію
                var fullRequest = _employeeService.GetRequestDetails(req.Id);
                if (fullRequest != null)
                {
                    var detailsWindow = new RequestDetailsWindow(fullRequest, _currentUser, _employeeService);
                    detailsWindow.ShowDialog();
                    // Оновлюємо після закриття
                    LoadData();
                }
            }
        }
    }
}