using Microsoft.Win32;
using Requests.Data;
using Requests.Data.Models;
using Requests.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Requests.UI.ViewModels
{
    public class SelectableDepartment : ViewModelBase
    {
        public int Id { get; set; }
        public string Name { get; set; }
        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    }

    public class CreateRequestViewModel : ViewModelBase
    {
        private readonly EmployeeService _employeeService;
        private readonly User _currentUser;
        private readonly Action<bool> _closeAction;

        // Для режиму редагування
        private readonly Request _existingRequest;
        public bool IsEditMode => _existingRequest != null;
        public string WindowTitle => IsEditMode ? $"Редагування запиту #{_existingRequest.Id}" : "Новий запит";
        public string SaveButtonText => IsEditMode ? "Зберегти зміни" : "Створити запит";

        // Блокування полів, якщо запит вже в роботі
        public bool CanEditCoreFields { get; private set; } = true;

        private string _title;
        private string _description;
        private RequestPriority _selectedPriority;
        private RequestCategory _selectedCategory;
        private DateTime? _deadline;

        public ObservableCollection<SelectableDepartment> Departments { get; set; }
        public ObservableCollection<RequestPriority> Priorities { get; set; }
        public ObservableCollection<RequestCategory> Categories { get; set; }

        private string _attachmentName;
        private byte[] _attachmentData;
        public string AttachmentName
        {
            get => _attachmentName;
            set { _attachmentName = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasAttachment)); }
        }
        public bool HasAttachment => !string.IsNullOrEmpty(AttachmentName);

        public string Title { get => _title; set { _title = value; OnPropertyChanged(); } }
        public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }
        public DateTime? Deadline { get => _deadline; set { _deadline = value; OnPropertyChanged(); } }
        public RequestPriority SelectedPriority { get => _selectedPriority; set { _selectedPriority = value; OnPropertyChanged(); } }
        public RequestCategory SelectedCategory { get => _selectedCategory; set { _selectedCategory = value; OnPropertyChanged(); } }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand AttachFileCommand { get; }
        public ICommand RemoveFileCommand { get; }

        // Конструктор для СТВОРЕННЯ
        public CreateRequestViewModel(User currentUser, EmployeeService employeeService, Action<bool> closeAction)
            : this(currentUser, employeeService, closeAction, null) { }

        // Конструктор для РЕДАГУВАННЯ
        public CreateRequestViewModel(User currentUser, EmployeeService employeeService, Action<bool> closeAction, Request requestToEdit)
        {
            _currentUser = currentUser;
            _employeeService = employeeService;
            _closeAction = closeAction;
            _existingRequest = requestToEdit;

            // Перевіряємо, чи можна редагувати основні поля
            if (IsEditMode)
            {
                // Якщо статус НЕ "Очікує погодження" і НЕ "Новий" -> блокуємо тему і категорію
                bool isPending = _existingRequest.GlobalStatus.Name == ServiceConstants.StatusPendingApproval ||
                                 _existingRequest.GlobalStatus.Name == ServiceConstants.StatusNew;
                CanEditCoreFields = isPending;
            }

            using (var context = new AppDbContext())
            {
                // Відділи
                var depts = context.Departments.Where(d => d.Id != _currentUser.DepartmentId).ToList();
                Departments = new ObservableCollection<SelectableDepartment>(depts.Select(d => new SelectableDepartment { Id = d.Id, Name = d.Name }));

                // Пріоритети
                var prioList = context.RequestPriorities.ToList();
                if (_currentUser.Position.Name != ServiceConstants.PositionDirector && _currentUser.Position.Name != ServiceConstants.PositionDeputyDirector)
                    prioList = prioList.Where(p => p.Name != ServiceConstants.PriorityCritical).ToList();
                Priorities = new ObservableCollection<RequestPriority>(prioList);

                // Категорії
                Categories = new ObservableCollection<RequestCategory>(context.RequestCategories.ToList());

                // Заповнення полів
                if (IsEditMode)
                {
                    Title = _existingRequest.Title;
                    Description = _existingRequest.Description;
                    Deadline = _existingRequest.Deadline;
                    SelectedPriority = Priorities.FirstOrDefault(p => p.Id == _existingRequest.PriorityId);
                    SelectedCategory = Categories.FirstOrDefault(c => c.Id == _existingRequest.CategoryId);

                    // Відмічаємо відділи
                    foreach (var task in _existingRequest.DepartmentTasks)
                    {
                        var d = Departments.FirstOrDefault(x => x.Id == task.DepartmentId);
                        if (d != null) d.IsSelected = true;
                    }
                }
                else
                {
                    Deadline = DateTime.Today.AddDays(7);
                    SelectedPriority = Priorities.FirstOrDefault(p => p.Name == ServiceConstants.PriorityNormal) ?? Priorities.FirstOrDefault();
                    SelectedCategory = Categories.FirstOrDefault();
                }
            }

            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
            AttachFileCommand = new RelayCommand(AttachFile);
            RemoveFileCommand = new RelayCommand(RemoveFile);
        }

        public void SetAttachment(string filePath) => ProcessFile(filePath);

        private void AttachFile(object obj)
        {
            var dialog = new OpenFileDialog { Title = "Оберіть файл" };
            if (dialog.ShowDialog() == true) ProcessFile(dialog.FileName);
        }

        private void ProcessFile(string filePath)
        {
            try
            {
                var info = new FileInfo(filePath);
                if (info.Length > 10 * 1024 * 1024) { MessageBox.Show("Файл > 10 МБ"); return; }
                AttachmentName = info.Name;
                _attachmentData = File.ReadAllBytes(filePath);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void RemoveFile(object obj)
        {
            AttachmentName = null;
            _attachmentData = null;
        }

        private void Save(object obj)
        {
            // Валідація основних полів (спрощена, щоб дозволити додавання відділів навіть при CanEditCoreFields=false, якщо логіка дозволяє)
            // Але за твоєю логікою CanEditCoreFields блокує все. 
            // Якщо ми хочемо дозволити додавати виконавців завжди, треба змінити логіку UI (розблокувати список відділів).
            // Поки що вважаємо, що редагування доступне.

            if (CanEditCoreFields)
            {
                if (string.IsNullOrWhiteSpace(Title)) { MessageBox.Show("Вкажіть тему!"); return; }
                if (SelectedCategory == null) { MessageBox.Show("Оберіть категорію!"); return; }
            }

            // Перевірка, чи обрано відділи (для обох режимів важливо мати хоча б одного)
            if (!Departments.Any(d => d.IsSelected))
            {
                MessageBox.Show("Оберіть хоча б один відділ-виконавець!");
                return;
            }

            try
            {
                if (IsEditMode)
                {
                    // === ОНОВЛЕННЯ (ТЕПЕР З ДОДАВАННЯМ ВИКОНАВЦІВ) ===
                    var updateInfo = new Request
                    {
                        Title = Title,
                        Description = Description,
                        PriorityId = SelectedPriority?.Id ?? _existingRequest.PriorityId,
                        CategoryId = SelectedCategory?.Id ?? _existingRequest.CategoryId,
                        Deadline = Deadline
                    };

                    // Збираємо список ID вибраних відділів
                    var targetIds = Departments.Where(d => d.IsSelected).Select(d => d.Id).ToList();

                    // Передаємо в сервіс: (Оригінал, Інфо, Юзер, СПИСОК ВІДДІЛІВ)
                    _employeeService.UpdateRequest(_existingRequest, updateInfo, _currentUser.Id, targetIds);

                    if (HasAttachment)
                        _employeeService.AddAttachment(_existingRequest.Id, AttachmentName, _attachmentData, _currentUser.Id);

                    MessageBox.Show("Запит оновлено! Нові виконавці додані (якщо були).");
                }
                else
                {
                    // === СТВОРЕННЯ ===
                    var targetIds = Departments.Where(d => d.IsSelected).Select(d => d.Id).ToList();

                    var req = new Request
                    {
                        Title = Title,
                        Description = Description,
                        PriorityId = SelectedPriority.Id,
                        CategoryId = SelectedCategory.Id,
                        Deadline = Deadline
                    };

                    _employeeService.CreateRequest(req, _currentUser, targetIds);

                    if (HasAttachment)
                        _employeeService.AddAttachment(req.Id, AttachmentName, _attachmentData, _currentUser.Id);

                    MessageBox.Show("Запит створено!");
                }
                _closeAction(true);
            }
            catch (Exception ex) { MessageBox.Show($"Помилка: {ex.Message}"); }
        }

        private void InitializeFromRequest(Request req)
        {
            Title = req.Title;
            Description = req.Description;
            Deadline = req.Deadline ?? DateTime.Now;

            SelectedPriority = Priorities.FirstOrDefault(p => p.Id == req.PriorityId);
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == req.CategoryId);

            // Відмічаємо відділи, які вже мають задачі по цьому запиту
            if (req.DepartmentTasks != null)
            {
                var assignedDeptIds = req.DepartmentTasks.Select(t => t.DepartmentId).ToList();
                foreach (var dept in Departments)
                {
                    if (assignedDeptIds.Contains(dept.Id))
                    {
                        dept.IsSelected = true;
                    }
                }
            }

            if (req.GlobalStatus.Name != ServiceConstants.StatusPendingApproval)
            {
                CanEditCoreFields = false;
                OnPropertyChanged(nameof(CanEditCoreFields));
            }
        }
        private void Cancel(object obj) => _closeAction(false);
    }
}