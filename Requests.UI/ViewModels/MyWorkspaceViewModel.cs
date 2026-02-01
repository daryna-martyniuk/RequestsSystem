using Requests.Data.Models;
using Requests.Services;
using Requests.UI.Views;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Requests.UI.ViewModels
{
    public class MyWorkspaceViewModel : ViewModelBase
    {
        private readonly EmployeeService _employeeService;
        // Додамо ManagerService, щоб отримати "На погодження"
        // (Або можна розширити EmployeeService, але це порушує SRP)
        private readonly ManagerService _managerService;
        private readonly User _currentUser;

        // Колекції
        public ObservableCollection<Request> MyRequests { get; set; }
        public ObservableCollection<DepartmentTask> MyTasks { get; set; } // Мої завдання
        public ObservableCollection<Request> PendingApprovals { get; set; } // На погодження

        // Видимість секції "На погодження"
        public bool IsManager =>
            _currentUser.Position.Name == ServiceConstants.PositionHead ||
            _currentUser.Position.Name == ServiceConstants.PositionDirector ||
            _currentUser.Position.Name == ServiceConstants.PositionDeputyHead; // + Заступники

        public ICommand CreateRequestCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand OpenDetailsCommand { get; }
        public ICommand EditRequestCommand { get; }
        public ICommand DeleteRequestCommand { get; }
        public ICommand CancelRequestCommand { get; }

        // Команди погодження
        public ICommand ApproveCommand { get; }
        public ICommand RejectCommand { get; }

        public MyWorkspaceViewModel(User currentUser, EmployeeService employeeService)
        {
            _currentUser = currentUser;
            _employeeService = employeeService;

            // Тимчасово створюємо ManagerService вручну (або через App.Factory)
            // В ідеалі передати через конструктор
            if (IsManager)
            {
                _managerService = App.CreateManagerService(); // Потрібно додати цей метод в App.xaml.cs
            }

            CreateRequestCommand = new RelayCommand(CreateRequest);
            RefreshCommand = new RelayCommand(o => LoadData());
            OpenDetailsCommand = new RelayCommand(OpenDetails);
            EditRequestCommand = new RelayCommand(EditRequest);
            DeleteRequestCommand = new RelayCommand(DeleteRequest);
            CancelRequestCommand = new RelayCommand(CancelRequest);

            ApproveCommand = new RelayCommand(Approve);
            RejectCommand = new RelayCommand(Reject);

            LoadData();
        }

        private void LoadData()
        {
            // 1. Мої запити
            var requests = _employeeService.GetMyRequests(_currentUser.Id);
            MyRequests = new ObservableCollection<Request>(requests);

            // 2. Мої завдання
            var tasks = _employeeService.GetMyTasks(_currentUser.Id);
            MyTasks = new ObservableCollection<DepartmentTask>(tasks);

            // 3. На погодження (тільки для керівників)
            if (IsManager && _managerService != null)
            {
                var pending = _managerService.GetPendingApprovals(_currentUser.DepartmentId);
                PendingApprovals = new ObservableCollection<Request>(pending);
                OnPropertyChanged(nameof(PendingApprovals));
            }

            OnPropertyChanged(nameof(MyRequests));
            OnPropertyChanged(nameof(MyTasks));
        }

        private void Approve(object obj)
        {
            if (obj is Request req)
            {
                _managerService.ApproveRequest(req.Id, _currentUser.Id);
                LoadData();
            }
        }

        private void Reject(object obj)
        {
            if (obj is Request req)
            {
                _managerService.RejectRequest(req.Id, _currentUser.Id);
                LoadData();
            }
        }

        private void CreateRequest(object obj)
        {
            // Виклик конструктора для СТВОРЕННЯ (null)
            var window = new Requests.UI.Views.CreateRequestWindow(_currentUser, _employeeService, null);
            if (window.ShowDialog() == true) LoadData();
        }

        private void EditRequest(object obj)
        {
            if (obj is Request req)
            {
                // Завантажуємо повний об'єкт, щоб мати доступ до Tasks
                var fullReq = _employeeService.GetRequestDetails(req.Id);
                if (fullReq != null)
                {
                    // Виклик конструктора для РЕДАГУВАННЯ
                    var window = new Requests.UI.Views.CreateRequestWindow(_currentUser, _employeeService, fullReq);
                    if (window.ShowDialog() == true) LoadData();
                }
            }
        }

        private void DeleteRequest(object obj)
        {
            if (obj is Request req)
            {
                if (MessageBox.Show("Ви точно хочете видалити цей запит?", "Підтвердження", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        _employeeService.DeleteRequest(req.Id, _currentUser.Id);
                        LoadData();
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message, "Помилка"); }
                }
            }
        }

        private void CancelRequest(object obj)
        {
            if (obj is Request req)
            {
                if (MessageBox.Show("Скасувати виконання цього запиту?", "Підтвердження", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        _employeeService.CancelRequest(req.Id, _currentUser.Id);
                        LoadData();
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message, "Помилка"); }
                }
            }
        }

        private void OpenDetails(object obj)
        {
            if (obj is Request req)
            {
                var fullRequest = _employeeService.GetRequestDetails(req.Id);
                if (fullRequest != null)
                {
                    var detailsWindow = new RequestDetailsWindow(fullRequest, _currentUser, _employeeService);
                    detailsWindow.ShowDialog();
                    LoadData();
                }
            }
        }
    }
}