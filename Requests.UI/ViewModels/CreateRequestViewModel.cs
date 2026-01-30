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
        
        public DateTime? Deadline 
        { 
            get => _deadline; 
            set { _deadline = value; OnPropertyChanged(); } 
        }

        public RequestPriority SelectedPriority { get => _selectedPriority; set { _selectedPriority = value; OnPropertyChanged(); } }
        public RequestCategory SelectedCategory { get => _selectedCategory; set { _selectedCategory = value; OnPropertyChanged(); } }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand AttachFileCommand { get; }
        public ICommand RemoveFileCommand { get; }

        public CreateRequestViewModel(User currentUser, EmployeeService employeeService, Action<bool> closeAction)
        {
            _currentUser = currentUser;
            _employeeService = employeeService;
            _closeAction = closeAction;

            Deadline = DateTime.Today.AddDays(7);

            using (var context = new AppDbContext())
            {
                var depts = context.Departments.Where(d => d.Id != _currentUser.DepartmentId).ToList();
                Departments = new ObservableCollection<SelectableDepartment>(depts.Select(d => new SelectableDepartment { Id = d.Id, Name = d.Name }));

                var prioList = context.RequestPriorities.ToList();
                
                bool isStrategicUser = _currentUser.Position.Name == ServiceConstants.PositionDirector || 
                                     _currentUser.Position.Name == ServiceConstants.PositionDeputyDirector;

                if (!isStrategicUser)
                {
                    prioList = prioList.Where(p => p.Name != ServiceConstants.PriorityCritical).ToList();
                }
                
                Priorities = new ObservableCollection<RequestPriority>(prioList);
                SelectedPriority = Priorities.FirstOrDefault(p => p.Name == ServiceConstants.PriorityNormal) ?? Priorities.FirstOrDefault();

                Categories = new ObservableCollection<RequestCategory>(context.RequestCategories.ToList());
                SelectedCategory = Categories.FirstOrDefault();
            }

            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
            AttachFileCommand = new RelayCommand(AttachFile);
            RemoveFileCommand = new RelayCommand(RemoveFile);
        }

        private void AttachFile(object obj)
        {
            var dialog = new OpenFileDialog { Title = "Оберіть файл" };
            if (dialog.ShowDialog() == true)
            {
                ProcessFile(dialog.FileName);
            }
        }

        // Цей метод викликається з Code-behind при Drag & Drop
        public void SetAttachment(string filePath)
        {
            ProcessFile(filePath);
        }

        private void ProcessFile(string filePath)
        {
            try
            {
                var info = new FileInfo(filePath);
                if (info.Length > 10 * 1024 * 1024) 
                {
                    MessageBox.Show("Файл завеликий (макс 10 МБ).", "Помилка");
                    return;
                }
                
                AttachmentName = info.Name;
                _attachmentData = File.ReadAllBytes(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка читання файлу: {ex.Message}");
            }
        }

        private void RemoveFile(object obj)
        {
            AttachmentName = null;
            _attachmentData = null;
        }

        private void Save(object obj)
        {
            if (string.IsNullOrWhiteSpace(Title)) { MessageBox.Show("Вкажіть тему!"); return; }
            if (SelectedCategory == null) { MessageBox.Show("Оберіть категорію!"); return; }

            var targets = Departments.Where(d => d.IsSelected).Select(d => d.Id).ToList();
            if (!targets.Any()) { MessageBox.Show("Оберіть хоча б один відділ-виконавець!"); return; }

            try
            {
                var req = new Request
                {
                    Title = Title,
                    Description = Description,
                    PriorityId = SelectedPriority.Id,
                    CategoryId = SelectedCategory.Id,
                    Deadline = Deadline
                };

                // Сервіс сам визначить початковий статус (Новий або На погодження) в залежності від ролі
                _employeeService.CreateRequest(req, _currentUser.Id, targets);

                if (HasAttachment)
                {
                    _employeeService.AddAttachment(req.Id, AttachmentName, _attachmentData, _currentUser.Id);
                }

                MessageBox.Show("Запит створено!");
                _closeAction(true);
            }
            catch (Exception ex) { MessageBox.Show($"Помилка: {ex.Message}"); }
        }

        private void Cancel(object obj) => _closeAction(false);
    }
}