using Npgsql;
using System.Configuration;
using System.Windows;
using System.Windows.Input;

namespace WarehouseMaster
{
    public partial class LoginWindow : Window
    {
        private readonly string _connectionString;

        public LoginWindow()
        {
            _connectionString = "Host=localhost;Port=5432;Username=postgres;Password=sa;Database=WarehouseMaster;";
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Пожалуйста, введите имя пользователя и пароль.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT user_id, full_name, password_hash, role_id
                        FROM app_user 
                        WHERE username = @username AND status = 'active'";

                    using (var cmd = new NpgsqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("username", username);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string storedHash = reader["password_hash"].ToString();

                                if (BCrypt.Net.BCrypt.Verify(password, storedHash))
                                {
                                    string fullName = reader["full_name"].ToString();
                                    MessageBox.Show($"Добро пожаловать, {fullName}!", "Успех",
                                        MessageBoxButton.OK, MessageBoxImage.Information);

                                    var connectionString = ConfigurationManager.ConnectionStrings["PostgreSQL"]?.ConnectionString
                               ?? "Host=localhost;Port=5432;Username=postgres;Password=sa;Database=WarehouseMaster;";
                                    var tableRepository = new TableRepository();
                                    WorkWindow workWindow = new WorkWindow(tableRepository);
                                    workWindow.Show();
                                    this.Close();
                                }
                                else
                                {
                                    MessageBox.Show("Неверный пароль.", "Ошибка",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                            else
                            {
                                MessageBox.Show("Пользователь не найден или отключён.", "Ошибка",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка входа: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavigateToRegister_Click(object sender, MouseButtonEventArgs e)
        {
            RegisterWindow registerWindow = new RegisterWindow();
            registerWindow.Show();
            this.Close();
        }
    }
}