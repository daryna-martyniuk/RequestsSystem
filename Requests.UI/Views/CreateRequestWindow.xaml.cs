using Requests.Data.Models;
using Requests.Services;
using Requests.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;

namespace Requests.UI.Views
{
    public partial class CreateRequestWindow : Window
    {
        // Конструктор для створення
        public CreateRequestWindow(User currentUser, EmployeeService service)
            : this(currentUser, service, null) { }

        // Конструктор для редагування
        public CreateRequestWindow(User currentUser, EmployeeService service, Request requestToEdit)
        {
            try
            {
                InitializeComponent();

                var vm = new CreateRequestViewModel(currentUser, service, (result) =>
                {
                    this.DialogResult = result;
                    this.Close();
                }, requestToEdit); // Передаємо запит сюди

                this.DataContext = vm;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка ініціалізації вікна: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        // Обробка перетягування файлу
        private void Border_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    var vm = DataContext as CreateRequestViewModel;
                    vm?.SetAttachment(files[0]); // Беремо перший файл
                }
            }
        }

        // Клік на зону завантаження (емулюємо клік по кнопці Attach)
        private void Border_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var vm = DataContext as CreateRequestViewModel;
            if (vm != null && vm.AttachFileCommand.CanExecute(null))
            {
                vm.AttachFileCommand.Execute(null);
            }
        }
    }
}