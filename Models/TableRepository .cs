using Npgsql;
using System.Configuration;
using System.Data;
using System.Windows;
using WarehouseMaster.Core;

namespace WarehouseMaster
{
    public class TableRepository : ITableRepository
    {
        private readonly string _connectionString;

        public TableRepository()
        {
            var connString = ConfigurationManager.ConnectionStrings["PostgreSQL"]?.ConnectionString;

            if (string.IsNullOrWhiteSpace(connString))
            {
                MessageBox.Show("Ошибка: Не найдена строка подключения в конфигурации. Используется резервная строка.");
                connString = "Host=localhost;Port=5432;Username=postgres;Password=sa;Database=WarehouseMaster;";
            }

            _connectionString = connString;
        }

        public void Update(DataTable dataTable, string tableName)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException("Строка подключения не инициализирована");
            }

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var adapter = new NpgsqlDataAdapter($"SELECT * FROM {tableName}", connection))
                    {
                        var builder = new NpgsqlCommandBuilder(adapter);
                        adapter.Update(dataTable);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении: {ex.Message}");
                throw;
            }
        }


        public DataTable GetAll(string tableName)
        {
            tableName = tableName.ToLower();

            var dataTable = new DataTable();
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var cmd = new NpgsqlCommand($"SELECT * FROM {tableName} LIMIT 100", connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        dataTable.Load(reader);
                    }
                }
            }
            catch (Npgsql.PostgresException ex)
            {
                MessageBox.Show($"Ошибка доступа к таблице {tableName}: {ex.Message}");
            }
            return dataTable;
        }

        public string GetPrimaryKeyColumn(string tableName)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    var query = @"
                        SELECT a.attname
                        FROM pg_index i
                        JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = ANY(i.indkey)
                        WHERE i.indrelid = @tableName::regclass
                        AND i.indisprimary";

                    using (var cmd = new NpgsqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("tableName", tableName);
                        return cmd.ExecuteScalar()?.ToString();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public void Delete(int id, string tableName)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                string primaryKey = GetPrimaryKeyColumn(tableName);
                var query = $"DELETE FROM {tableName} WHERE {primaryKey} = @id";

                using (var cmd = new NpgsqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

    }
}