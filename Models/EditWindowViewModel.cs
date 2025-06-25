using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Npgsql;
using WarehouseMaster.Core;
using System.Configuration;

namespace WarehouseMaster
{
    public class EditWindowViewModel : BaseViewModel
    {
        private readonly DataRowView _row;
        private readonly string _tableName;
        private readonly Window _window;
        private string _primaryKey;

        // Изменяем тип на List<KeyValuePair<string, FieldValue>>
        public List<KeyValuePair<string, FieldValue>> Fields { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public EditWindowViewModel(DataRowView row, string tableName, Window window)
        {
            _row = row;
            _tableName = tableName;
            _window = window;

            // Получаем первичный ключ
            _primaryKey = GetPrimaryKeyColumn(tableName);

            // Заполняем поля для редактирования
            Fields = new List<KeyValuePair<string, FieldValue>>(); // Изменяем тип списка
            for (int i = 0; i < row.Row.Table.Columns.Count; i++)
            {
                var column = row.Row.Table.Columns[i];
                bool isReadOnly = column.ColumnName == _primaryKey; // Первичный ключ нельзя редактировать

                // Создаем FieldValue явно
                var fieldValue = new FieldValue
                {
                    Value = row[i]?.ToString(),
                    IsReadOnly = isReadOnly
                };

                Fields.Add(new KeyValuePair<string, FieldValue>(column.ColumnName, fieldValue));
            }

            SaveCommand = new RelayCommand(SaveChanges);
            CancelCommand = new RelayCommand(() => _window.DialogResult = false);
        }

        private void SaveChanges()
        {
            try
            {
                using (var connection = new NpgsqlConnection(ConfigurationManager.ConnectionStrings["PostgreSQL"].ConnectionString))
                {
                    connection.Open();

                    // Формируем SQL запрос для обновления
                    var setValues = string.Join(", ", Fields
                        .Where(f => !f.Value.IsReadOnly)
                        .Select(f => $"{f.Key} = @{f.Key}"));

                    var query = $"UPDATE {_tableName} SET {setValues} WHERE {_primaryKey} = @id";

                    using (var cmd = new NpgsqlCommand(query, connection))
                    {
                        // Добавляем параметры
                        foreach (var field in Fields.Where(f => !f.Value.IsReadOnly))
                        {
                            // Исправляем обработку NULL значений
                            object value = string.IsNullOrEmpty(field.Value.Value) ? (object)DBNull.Value : field.Value.Value;
                            cmd.Parameters.AddWithValue(field.Key, value);
                        }

                        // Добавляем ID для условия WHERE
                        cmd.Parameters.AddWithValue("id", _row[_primaryKey]);

                        cmd.ExecuteNonQuery();
                    }
                }

                // Обновляем исходную строку
                for (int i = 0; i < _row.Row.Table.Columns.Count; i++)
                {
                    var column = _row.Row.Table.Columns[i];
                    var field = Fields.FirstOrDefault(f => f.Key == column.ColumnName);
                    if (field.Key != null && !field.Value.IsReadOnly)
                    {
                        _row[i] = field.Value.Value ?? DBNull.Value.ToString();
                    }
                }

                _window.DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении изменений: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetPrimaryKeyColumn(string tableName)
        {
            try
            {
                using (var connection = new NpgsqlConnection(ConfigurationManager.ConnectionStrings["PostgreSQL"].ConnectionString))
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
    }

    public class FieldValue
    {
        public string Value { get; set; }
        public bool IsReadOnly { get; set; }
    }
}