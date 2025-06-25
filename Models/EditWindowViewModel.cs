using Npgsql;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WarehouseMaster.Core;

namespace WarehouseMaster
{
    public class EditWindowViewModel : BaseViewModel
    {
        private readonly DataRowView _row;
        private readonly string _tableName;
        private readonly Window _window;
        private string _primaryKey;

        public List<KeyValuePair<string, FieldValue>> Fields { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public EditWindowViewModel(DataRowView row, string tableName, Window window)
        {
            _row = row;
            _tableName = tableName;
            _window = window;

            _primaryKey = GetPrimaryKeyColumn(tableName);

            Fields = new List<KeyValuePair<string, FieldValue>>();
            for (int i = 0; i < row.Row.Table.Columns.Count; i++)
            {
                var column = row.Row.Table.Columns[i];
                bool isReadOnly = column.ColumnName == _primaryKey;

                var fieldValue = new FieldValue
                {
                    Value = row[i]?.ToString(),
                    IsReadOnly = isReadOnly,
                    DataType = column.DataType,
                    ColumnName = column.ColumnName
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
                if (!ValidateFields())
                    return;

                using (var connection = new NpgsqlConnection(GetConnectionString()))
                {
                    connection.Open();

                    var setValues = string.Join(", ", Fields
                        .Where(f => !f.Value.IsReadOnly)
                        .Select(f => $"{f.Key} = @{f.Key}"));

                    var query = $"UPDATE {_tableName} SET {setValues} WHERE {_primaryKey} = @id";

                    using (var cmd = new NpgsqlCommand(query, connection))
                    {
                        foreach (var field in Fields.Where(f => !f.Value.IsReadOnly))
                        {
                            cmd.Parameters.AddWithValue(field.Key, ConvertFieldValue(field.Value));
                        }

                        cmd.Parameters.AddWithValue("id", _row[_primaryKey]);
                        cmd.ExecuteNonQuery();
                    }
                }

                UpdateOriginalRow();
                _window.DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении изменений: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateFields()
        {
            foreach (var field in Fields.Where(f => !f.Value.IsReadOnly))
            {
                try
                {
                    if (field.Value.DataType == typeof(decimal))
                    {
                        if (!string.IsNullOrEmpty(field.Value.Value))
                        {
                            decimal.Parse(field.Value.Value, CultureInfo.InvariantCulture);
                        }
                    }
                }
                catch
                {
                    MessageBox.Show($"Некорректное значение в поле {field.Key}. Ожидается числовое значение.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            return true;
        }

        private object ConvertFieldValue(FieldValue field)
        {
            if (string.IsNullOrEmpty(field.Value))
                return DBNull.Value;

            try
            {
                return field.DataType.Name switch
                {
                    "Decimal" => decimal.Parse(field.Value, CultureInfo.InvariantCulture),
                    "Int32" => int.Parse(field.Value),
                    "DateTime" => DateTime.Parse(field.Value),
                    "Boolean" => bool.Parse(field.Value),
                    _ => field.Value
                };
            }
            catch
            {
                MessageBox.Show($"Невозможно преобразовать значение '{field.Value}' для поля {field.ColumnName}",
                    "Ошибка формата", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private void UpdateOriginalRow()
        {
            for (int i = 0; i < _row.Row.Table.Columns.Count; i++)
            {
                var column = _row.Row.Table.Columns[i];
                var field = Fields.FirstOrDefault(f => f.Key == column.ColumnName);

                if (field.Key != null && !field.Value.IsReadOnly)
                {
                    _row[i] = ConvertFieldValue(field.Value) ?? DBNull.Value;
                }
            }
        }

        private string GetPrimaryKeyColumn(string tableName)
        {
            try
            {
                using (var connection = new NpgsqlConnection(GetConnectionString()))
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

        private string GetConnectionString()
        {
            return ConfigurationManager.ConnectionStrings["PostgreSQL"]?.ConnectionString
                   ?? "Host=localhost;Port=5432;Username=postgres;Password=sa;Database=WarehouseMaster;";
        }
    }

    public class FieldValue
    {
        public string Value { get; set; }
        public bool IsReadOnly { get; set; }
        public Type DataType { get; set; }
        public string ColumnName { get; set; }
    }
}