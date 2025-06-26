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
                    IsReadOnly = false,
                    DataType = column.DataType,
                    ColumnName = column.ColumnName
                };

                Fields.Add(new KeyValuePair<string, FieldValue>(column.ColumnName, fieldValue));
            }
            foreach (var field in Fields)
            {
                if (field.Value.ColumnName.EndsWith("_id") && field.Value.ColumnName != _primaryKey)
                {
                    string refTable = field.Value.ColumnName.Replace("_id", "");
                    string idColumn = field.Value.ColumnName;
                    string nameColumn = refTable + "_name";

                    try
                    {
                        using var conn = new NpgsqlConnection(GetConnectionString());
                        conn.Open();

                        var cmd = new NpgsqlCommand($"SELECT {idColumn}, {nameColumn} FROM {refTable} ORDER BY {nameColumn}", conn);
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            var id = reader[0].ToString();
                            var name = reader[1].ToString();
                            field.Value.LookupItems.TryAdd(id, name);
                        }
                    }
                    catch
                    {
                        // Не страшно, если таблицы нет — просто не будет комбобокса
                    }
                }
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

                using var connection = new NpgsqlConnection(GetConnectionString());
                connection.Open();

                // Получаем реальные поля таблицы
                var realColumns = new HashSet<string>();
                using (var schemaCmd = new NpgsqlCommand($"SELECT * FROM {_tableName} LIMIT 0", connection))
                using (var reader = schemaCmd.ExecuteReader(CommandBehavior.SchemaOnly))
                {
                    var schemaTable = reader.GetSchemaTable();
                    foreach (DataRow row in schemaTable.Rows)
                    {
                        realColumns.Add(row["ColumnName"].ToString());
                    }
                }

                var changedFields = new List<KeyValuePair<string, FieldValue>>();
                foreach (var field in Fields)
                {
                    if (!realColumns.Contains(field.Key) || field.Value.IsReadOnly)
                        continue;

                    var original = _row[field.Key]?.ToString();
                    var current = field.Value.Value?.ToString();

                    if (original != current)
                        changedFields.Add(field);
                }

                if (!changedFields.Any())
                {
                    MessageBox.Show("Нет изменений для сохранения", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var setClause = string.Join(", ", changedFields.Select(f => $"{f.Key} = @{f.Key}"));
                var command = new NpgsqlCommand($"UPDATE {_tableName} SET {setClause} WHERE {_primaryKey} = @id", connection);

                foreach (var field in changedFields)
                {
                    command.Parameters.AddWithValue(field.Key, ConvertFieldValue(field.Value));
                }

                command.Parameters.AddWithValue("id", _row[_primaryKey]);
                command.ExecuteNonQuery();

                UpdateOriginalRow();
                _window.DialogResult = true;
            }
            catch (Exception ex)
            {
                
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

        public Dictionary<string, string> LookupItems { get; set; } = new(); // key = id, value = name
        public bool IsLookup => LookupItems?.Any() == true;
    }

}