using Npgsql;
using System;
using System.Windows;
using System.Windows.Controls;
using BCrypt.Net;
using Org.BouncyCastle.Crypto.Generators; // Подключи пакет BCrypt.Net-Next через NuGet

namespace WarehouseMaster
{
    public partial class RegisterWindow : Window
    {
        public RegisterWindow()
        {
            InitializeComponent();
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string fullName = FullNameTextBox.Text.Trim();
            string phone = PhoneTextBox.Text.Trim();
            string email = EmailTextBox.Text.Trim();
            string username = UsernameTextBox.Text.Trim();
            string password = PasswordBox.Password;
            string confirmPassword = ConfirmPasswordBox.Password;
            var selectedRoleItem = RoleComboBox.SelectedItem as ComboBoxItem;
            if (selectedRoleItem == null || !int.TryParse(selectedRoleItem.Tag?.ToString(), out int roleId))
            {
                MessageBox.Show("Пожалуйста, выберите роль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Поля 'Полное имя', 'Имя пользователя' и 'Пароль' обязательны для заполнения.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (password != confirmPassword)
            {
                MessageBox.Show("Пароли не совпадают!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Хешируем пароль
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(password); // Исправлено: использование правильного метода из подключенного пространства имен

            string connString = "Host=localhost;Port=5432;Username=postgres;Password=sa;Database=WarehouseMaster;";

            try
            {
                using (var conn = new NpgsqlConnection(connString))
                {
                    conn.Open();
                    string query = @"
                            INSERT INTO app_user 
                            (full_name, role_id, username, password_hash, phone, email) 
                            VALUES 
                            (@full_name, @role_id, @username, @password_hash, @phone, @email)";

                    using (var cmd = new NpgsqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("full_name", fullName);
                        cmd.Parameters.AddWithValue("role_id", roleId);
                        cmd.Parameters.AddWithValue("username", username);
                        cmd.Parameters.AddWithValue("password_hash", passwordHash);
                        cmd.Parameters.AddWithValue("phone", string.IsNullOrEmpty(phone) ? (object)DBNull.Value : phone);
                        cmd.Parameters.AddWithValue("email", string.IsNullOrEmpty(email) ? (object)DBNull.Value : email);

                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Пользователь успешно зарегистрирован!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoginWindow workWindow = new LoginWindow();
                workWindow.Show();
                this.Close(); 
            }
            catch (PostgresException pgEx)
            {
                if (pgEx.SqlState == "23505")
                {
                    MessageBox.Show("Пользователь с таким именем или email уже существует.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show($"Ошибка PostgreSQL: {pgEx.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void NavigateToLogin_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            LoginWindow login = new LoginWindow();
            login.Show();
            this.Close();
        }
    }
}
