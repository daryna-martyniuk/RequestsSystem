using Requests.Data.Models;
using Requests.Services;
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

        public MyWorkspaceViewModel(User currentUser, EmployeeService employeeService)
        {
            _currentUser = currentUser;
            _employeeService = employeeService;

            CreateRequestCommand = new RelayCommand(CreateRequest);
            RefreshCommand = new RelayCommand(o => LoadData());

            LoadData();
        }

        private void LoadData()
        {
            var requests = _employeeService.GetMyRequests(_currentUser.Id);
            MyRequests = new ObservableCollection<Request>(requests);
            OnPropertyChanged(nameof(MyRequests));
        }

        private void CreateRequest(object obj)
        {
            MessageBox.Show("Відкриття вікна створення запиту...");
        }
    }
}