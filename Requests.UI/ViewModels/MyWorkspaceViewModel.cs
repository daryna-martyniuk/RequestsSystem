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
        private readonly User _currentUser;

        public ObservableCollection<Request> MyRequests { get; set; }

        public ICommand CreateRequestCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand OpenDetailsCommand { get; }

        // Нові команди
        public ICommand EditRequestCommand { get; }
        public ICommand DeleteRequestCommand { get; }
        public ICommand CancelRequestCommand { get; }

        public MyWorkspaceViewModel(User currentUser, EmployeeService employeeService)
        {
            _currentUser = currentUser;
            _employeeService = employeeService;

            CreateRequestCommand = new RelayCommand(CreateRequest);
            RefreshCommand = new RelayCommand(o => LoadData());
            OpenDetailsCommand = new RelayCommand(OpenDetails);

            EditRequestCommand = new RelayCommand(EditRequest);
            DeleteRequestCommand = new RelayCommand(DeleteRequest);
            CancelRequestCommand = new RelayCommand(CancelRequest);

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