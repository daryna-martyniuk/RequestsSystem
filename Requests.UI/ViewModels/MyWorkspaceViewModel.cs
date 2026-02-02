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
        private readonly ManagerService _managerService;
        private readonly User _currentUser;

        // Колекції
        public ObservableCollection<Request> MyRequests { get; set; }
        public ObservableCollection<DepartmentTask> MyTasks { get; set; }
        public ObservableCollection<Request> PendingApprovals { get; set; }

        // НОВЕ: Колекція вхідних задач для відділу
        public ObservableCollection<DepartmentTask> IncomingDepartmentTasks { get; set; }

        public bool IsManager =>
            _currentUser.Position.Name == ServiceConstants.PositionHead ||
            _currentUser.Position.Name == ServiceConstants.PositionDirector ||
            _currentUser.Position.Name == ServiceConstants.PositionDeputyHead;

        // Видимість вкладки для керівника
        public Visibility ManagerVisibility => IsManager ? Visibility.Visible : Visibility.Collapsed;

        public ICommand CreateRequestCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand OpenDetailsCommand { get; }
        public ICommand EditRequestCommand { get; }
        public ICommand DeleteRequestCommand { get; }
        public ICommand CancelRequestCommand { get; }
        public ICommand ApproveCommand { get; }
        public ICommand RejectCommand { get; }

        // НОВІ КОМАНДИ
        public ICommand AssignExecutorCommand { get; }
        public ICommand ForwardTaskCommand { get; }
        public ICommand DiscussTaskCommand { get; }

        public MyWorkspaceViewModel(User user, EmployeeService service)
        {
            _currentUser = user;
            _employeeService = service;
            _managerService = App.CreateManagerService();

            MyRequests = new ObservableCollection<Request>();
            MyTasks = new ObservableCollection<DepartmentTask>();
            PendingApprovals = new ObservableCollection<Request>();
            IncomingDepartmentTasks = new ObservableCollection<DepartmentTask>();

            CreateRequestCommand = new RelayCommand(CreateRequest);
            RefreshCommand = new RelayCommand(o => LoadData());
            OpenDetailsCommand = new RelayCommand(OpenDetails);

            EditRequestCommand = new RelayCommand(EditRequest);
            DeleteRequestCommand = new RelayCommand(DeleteRequest);
            CancelRequestCommand = new RelayCommand(CancelRequest);

            ApproveCommand = new RelayCommand(Approve);
            RejectCommand = new RelayCommand(Reject);

            // Ініціалізація нових команд
            AssignExecutorCommand = new RelayCommand(AssignExecutor);
            ForwardTaskCommand = new RelayCommand(ForwardTask);
            DiscussTaskCommand = new RelayCommand(DiscussTask);

            LoadData();
        }

        private void LoadData()
        {
            MyRequests.Clear();
            var reqs = _employeeService.GetMyRequests(_currentUser.Id);
            foreach (var r in reqs) MyRequests.Add(r);

            MyTasks.Clear();
            var tasks = _employeeService.GetMyTasks(_currentUser.Id);
            foreach (var t in tasks) MyTasks.Add(t);

            if (IsManager)
            {
                PendingApprovals.Clear();
                var approvals = _managerService.GetPendingApprovals(_currentUser.DepartmentId);
                foreach (var a in approvals) PendingApprovals.Add(a);

                // Завантажуємо вхідні задачі на відділ
                IncomingDepartmentTasks.Clear();
                var incoming = _managerService.GetIncomingTasks(_currentUser.DepartmentId);
                foreach (var t in incoming) IncomingDepartmentTasks.Add(t);
            }
        }

        // === ЛОГІКА РОЗПОДІЛУ ЗАДАЧ ===

        private void AssignExecutor(object obj)
        {
            if (obj is DepartmentTask task)
            {
                // Отримуємо список співробітників мого відділу
                var employees = _managerService.GetMyEmployees(_currentUser.DepartmentId);

                // Відкриваємо вікно вибору
                var dialog = new SelectUserWindow(employees);
                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        var selectedEmployee = dialog.SelectedUser;
                        _managerService.AssignExecutor(task.Id, selectedEmployee.Id, _currentUser.Id);
                        MessageBox.Show($"Виконавця {selectedEmployee.FullName} призначено!");
                        LoadData();
                    }
                    catch (Exception ex) { MessageBox.Show("Помилка: " + ex.Message); }
                }
            }
        }

        private void ForwardTask(object obj)
        {
            if (obj is DepartmentTask task)
            {
                // Відкриваємо вікно вибору відділу
                var dialog = new SelectDepartmentWindow();
                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        var newDept = dialog.SelectedDepartment;
                        if (newDept.Id == _currentUser.DepartmentId)
                        {
                            MessageBox.Show("Не можна переслати задачу на свій же відділ!");
                            return;
                        }

                        if (MessageBox.Show($"Переслати задачу у відділ '{newDept.Name}'?", "Підтвердження", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            _managerService.ForwardTask(task.Id, newDept.Id, _currentUser.Id);
                            MessageBox.Show("Задачу переслано.");
                            LoadData();
                        }
                    }
                    catch (Exception ex) { MessageBox.Show("Помилка: " + ex.Message); }
                }
            }
        }

        private void DiscussTask(object obj)
        {
            if (obj is DepartmentTask task)
            {
                // Використовуємо EditNameWindow для введення коментаря (щоб не створювати нове вікно)
                var dialog = new EditNameWindow("");
                dialog.Title = "Коментар до обговорення";
                // Тут можна було б змінити текст Label у вікні, але не будемо ускладнювати

                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        string comment = dialog.ResultName;
                        _managerService.SetRequestToDiscussion(task.RequestId, _currentUser.Id, comment);
                        MessageBox.Show("Запит повернуто на уточнення/обговорення.");
                        LoadData();
                    }
                    catch (Exception ex) { MessageBox.Show("Помилка: " + ex.Message); }
                }
            }
        }

        // ... Інші методи (Approve, Reject, Create...) без змін ...

        private void Approve(object obj)
        {
            if (obj is Request req)
            {
                try
                {
                    _managerService.ApproveRequest(req.Id, _currentUser.Id, req);
                    MessageBox.Show("Запит погоджено!");
                    LoadData();
                }
                catch (Exception ex) { MessageBox.Show("Помилка: " + ex.Message); }
            }
        }

        private void Reject(object obj)
        {
            if (obj is Request req)
            {
                if (MessageBox.Show("Відхилити запит?", "Підтвердження", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        _managerService.RejectRequest(req.Id, _currentUser.Id, "Відхилено керівником");
                        LoadData();
                    }
                    catch (Exception ex) { MessageBox.Show("Помилка: " + ex.Message); }
                }
            }
        }

        private void CreateRequest(object obj)
        {
            var win = new CreateRequestWindow(_currentUser, _employeeService);
            if (win.ShowDialog() == true) LoadData();
        }

        private void EditRequest(object obj)
        {
            if (obj is Request req)
            {
                var fullReq = _employeeService.GetRequestDetails(req.Id);
                if (fullReq != null)
                {
                    var win = new CreateRequestWindow(_currentUser, _employeeService, fullReq);
                    if (win.ShowDialog() == true) LoadData();
                }
            }
        }

        private void DeleteRequest(object obj)
        {
            if (obj is Request req)
            {
                if (MessageBox.Show("Видалити чернетку?", "Підтвердження", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        _employeeService.DeleteRequest(req.Id, _currentUser.Id);
                        LoadData();
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
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
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
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