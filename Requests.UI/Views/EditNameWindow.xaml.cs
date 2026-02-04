using System.Windows;
using System.Windows.Controls;

namespace Requests.UI.Views
{
    public partial class EditNameWindow : Window
    {
        public string ResultName { get; private set; }

        /// <summary>
        /// Універсальне вікно для введення тексту.
        /// </summary>
        /// <param name="initialValue">Початкове значення (для редагування)</param>
        /// <param name="title">Заголовок вікна (не видно в WindowStyle=None, але корисно)</param>
        /// <param name="prompt">Текст підказки над полем</param>
        /// <param name="isMultiline">Чи потрібне велике поле для коментарів</param>
        public EditNameWindow(string initialValue = "", string title = "Редагування", string prompt = "Введіть значення:", bool isMultiline = false)
        {
            InitializeComponent();

            this.Title = title;
            PromptTextBlock.Text = prompt;
            InputTextBox.Text = initialValue;

            if (isMultiline)
            {
                InputTextBox.Height = 100;
                InputTextBox.TextWrapping = TextWrapping.Wrap;
                InputTextBox.AcceptsReturn = true;
                InputTextBox.VerticalContentAlignment = VerticalAlignment.Top;
            }
            else
            {
                InputTextBox.Height = Double.NaN; // Auto
                InputTextBox.TextWrapping = TextWrapping.NoWrap;
                InputTextBox.AcceptsReturn = false;
                InputTextBox.VerticalContentAlignment = VerticalAlignment.Center;
            }

            InputTextBox.Focus();
            if (!string.IsNullOrEmpty(initialValue))
            {
                InputTextBox.SelectAll();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(InputTextBox.Text))
            {
                MessageBox.Show("Поле не може бути порожнім!", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ResultName = InputTextBox.Text;
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