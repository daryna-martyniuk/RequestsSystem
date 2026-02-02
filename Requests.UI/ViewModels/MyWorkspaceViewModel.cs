using Requests.Data.Models;
using Requests.Services;
using Requests.UI.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq; // Додано для Where()
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

        // РОЗДІЛЕННЯ ЗАВДАНЬ
        public ObservableCollection<DepartmentTask> MyActiveTasks { get; set; } // Нові, В роботі, На паузі
        public ObservableCollection<DepartmentTask> MyCompletedTasks { get; set; } // Виконані

        public ObservableCollection<Request> PendingApprovals { get; set; }
        public ObservableCollection<DepartmentTask> IncomingDepartmentTasks { get; set; }

        public bool IsManager =>
            _currentUser.Position.Name == ServiceConstants.PositionHead ||
            _currentUser.Position.Name == ServiceConstants.PositionDirector ||
            _currentUser.Position.Name == ServiceConstants.PositionDeputyHead;

        public Visibility ManagerVisibility => IsManager ? Visibility.Visible : Visibility.Collapsed;

        // Команди
        public ICommand CreateRequestCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand OpenDetailsCommand { get; }
        public ICommand EditRequestCommand { get; }
        public ICommand DeleteRequestCommand { get; }
        public ICommand CancelRequestCommand { get; }
        public ICommand ApproveCommand { get; }
        public ICommand RejectCommand { get; }
        public ICommand AssignExecutorCommand { get; }
        public ICommand ForwardTaskCommand { get; }
        public ICommand DiscussTaskCommand { get; }
        public ICommand CompleteTaskCommand { get; }
        public ICommand PauseTaskCommand { get; }
        public ICommand ResumeTaskCommand { get; } // НОВА

        public MyWorkspaceViewModel(User user, EmployeeService service)
        {
            _currentUser = user;
            _employeeService = service;
            _managerService = App.CreateManagerService();

            MyRequests = new ObservableCollection<Request>();

            // Ініціалізація нових колекцій
            MyActiveTasks = new ObservableCollection<DepartmentTask>();
            MyCompletedTasks = new ObservableCollection<DepartmentTask>();

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
            AssignExecutorCommand = new RelayCommand(AssignExecutor);
            ForwardTaskCommand = new RelayCommand(ForwardTask);
            DiscussTaskCommand = new RelayCommand(DiscussTask);
            CompleteTaskCommand = new RelayCommand(CompleteTask);
            PauseTaskCommand = new RelayCommand(PauseTask);
            ResumeTaskCommand = new RelayCommand(ResumeTask); // Ініціалізація

            LoadData();
        }

        private void LoadData()
        {
            // 1. Мої запити
            MyRequests.Clear();
            foreach (var r in _employeeService.GetMyRequests(_currentUser.Id)) MyRequests.Add(r);

            // 2. Мої завдання (Розділення)
            MyActiveTasks.Clear();
            MyCompletedTasks.Clear();

            var allTasks = _employeeService.GetMyTasks(_currentUser.Id);
            foreach (var t in allTasks)
            {
                if (t.Status.Name == ServiceConstants.TaskStatusDone)
                {
                    MyCompletedTasks.Add(t);
                }
                else
                {
                    MyActiveTasks.Add(t);
                }
            }

            // 3. Секція керівника
            if (IsManager)
            {
                PendingApprovals.Clear();
                foreach (var a in _managerService.GetPendingApprovals(_currentUser.DepartmentId)) PendingApprovals.Add(a);

                IncomingDepartmentTasks.Clear();
                foreach (var t in _managerService.GetIncomingTasks(_currentUser.DepartmentId)) IncomingDepartmentTasks.Add(t);
            }
        }

        // === УПРАВЛІННЯ СТАТУСОМ ЗАВДАННЯ ===

        private void CompleteTask(object obj)
        {
            if (obj is DepartmentTask task)
            {
                if (MessageBox.Show("Позначити завдання як виконане?", "Підтвердження", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        _employeeService.UpdateTaskStatus(task.Id, _currentUser.Id, ServiceConstants.TaskStatusDone);
                        MessageBox.Show("Чудова робота! Завдання виконано.");
                        LoadData();
                    }
                    catch (Exception ex) { MessageBox.Show("Помилка: " + ex.Message); }
                }
            }
        }

        private void PauseTask(object obj)
        {
            if (obj is DepartmentTask task)
            {
                try
                {
                    _employeeService.PauseTask(task.Id, _currentUser.Id);
                    MessageBox.Show("Завдання поставлено на паузу.");
                    LoadData();
                }
                catch (Exception ex) { MessageBox.Show("Помилка: " + ex.Message); }
            }
        }

        private void ResumeTask(object obj)
        {
            if (obj is DepartmentTask task)
            {
                try
                {
                    _employeeService.ResumeTask(task.Id, _currentUser.Id);
                    MessageBox.Show("Завдання відновлено (В роботі).");
                    LoadData();
                }
                catch (Exception ex) { MessageBox.Show("Помилка: " + ex.Message); }
            }
        }

        private void OpenDetails(object obj)
        {
            Request reqToOpen = null;

            if (obj is Request req)
            {
                reqToOpen = req;
            }
            else if (obj is DepartmentTask task)
            {
                reqToOpen = task.Request;
            }

            if (reqToOpen != null)
            {
                var fullRequest = _employeeService.GetRequestDetails(reqToOpen.Id);
                if (fullRequest != null)
                {
                    var detailsWindow = new RequestDetailsWindow(fullRequest, _currentUser, _employeeService);
                    detailsWindow.ShowDialog();
                    LoadData();
                }
            }
        }

        // ... (Інші методи AssignExecutor, ForwardTask, Approve, Reject... без змін)
        private void AssignExecutor(object obj)
        {
            if (obj is DepartmentTask task)
            {
                var employees = _managerService.GetMyEmployees(_currentUser.DepartmentId);
                var dialog = new SelectUserWindow(employees);
                if (dialog.ShowDialog() == true)
                {
                    try { _managerService.AssignExecutor(task.Id, dialog.SelectedUser.Id, _currentUser.Id); MessageBox.Show("Виконавця призначено!"); LoadData(); }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void ForwardTask(object obj)
        {
            if (obj is DepartmentTask task)
            {
                var dialog = new SelectDepartmentWindow();
                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        if (dialog.SelectedDepartment.Id == _currentUser.DepartmentId) { MessageBox.Show("Не можна на свій відділ!"); return; }
                        _managerService.ForwardTask(task.Id, dialog.SelectedDepartment.Id, _currentUser.Id);
                        MessageBox.Show("Задачу переслано."); LoadData();
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void DiscussTask(object obj)
        {
            if (obj is DepartmentTask task)
            {
                var dialog = new EditNameWindow(""); dialog.Title = "Коментар";
                if (dialog.ShowDialog() == true)
                {
                    _managerService.SetRequestToDiscussion(task.RequestId, _currentUser.Id, dialog.ResultName);
                    MessageBox.Show("Повернуто на обговорення."); LoadData();
                }
            }
        }

        private void Approve(object obj)
        {
            if (obj is Request req)
            {
                try { _managerService.ApproveRequest(req.Id, _currentUser.Id, req); MessageBox.Show("Погоджено!"); LoadData(); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        private void Reject(object obj)
        {
            if (obj is Request req)
            {
                if (MessageBox.Show("Відхилити?", "Увага", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _managerService.RejectRequest(req.Id, _currentUser.Id, "Відхилено"); LoadData();
                }
            }
        }

        private void CreateRequest(object obj) { new CreateRequestWindow(_currentUser, _employeeService).ShowDialog(); LoadData(); }
        private void EditRequest(object obj)
        {
            if (obj is Request req)
            {
                var full = _employeeService.GetRequestDetails(req.Id);
                if (full != null) { new CreateRequestWindow(_currentUser, _employeeService, full).ShowDialog(); LoadData(); }
            }
        }
        private void DeleteRequest(object obj)
        {
            if (obj is Request req && MessageBox.Show("Видалити?", "Увага", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            { _employeeService.DeleteRequest(req.Id, _currentUser.Id); LoadData(); }
        }
        private void CancelRequest(object obj)
        {
            if (obj is Request req && MessageBox.Show("Скасувати?", "Увага", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            { _employeeService.CancelRequest(req.Id, _currentUser.Id); LoadData(); }
        }
    }
}