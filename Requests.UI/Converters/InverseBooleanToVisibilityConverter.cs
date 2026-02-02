using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Requests.UI.Converters
{
    // Цей клас перетворює:
    // true -> Collapsed (Сховати)
    // false -> Visible (Показати)
    // Також працює з числами: 
    // > 0 -> Collapsed
    // 0 -> Visible
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }

            // Додаткова логіка для Count (кількості)
            if (value is int intValue)
            {
                // Якщо є елементи (>0), то ховаємо текст "Немає елементів"
                return intValue > 0 ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}