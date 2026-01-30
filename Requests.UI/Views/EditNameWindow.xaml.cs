using System.Windows;

namespace Requests.UI.Views
{
    public partial class EditNameWindow : Window
    {
        public string ResultName { get; private set; }

        public EditNameWindow(string currentName)
        {
            InitializeComponent();
            TxtName.Text = currentName;
            TxtName.Focus();
            TxtName.SelectAll();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                MessageBox.Show("Назва не може бути порожньою!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ResultName = TxtName.Text;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}