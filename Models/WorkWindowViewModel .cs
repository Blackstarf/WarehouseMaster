using System;
using System.Data;
using Npgsql;
using System.Windows.Input;
using System.Windows;
using WarehouseMaster.Core;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.IO;
using System.ComponentModel;

namespace WarehouseMaster
{
    public class WorkWindowViewModel : BaseViewModel
    {
        private readonly string _connectionString;
        private DataTable _currentTable;
        private string _currentViewName;
        private string _currentTableName;
        private DataRowView _selectedRow;
        public ICommand ImportFromJsonCommand { get; }

        public DataTable CurrentTable
        {
            get => _currentTable;
            set
            {
                _currentTable = value;
                OnPropertyChanged();
            }
        }

        public string CurrentViewName
        {
            get => _currentViewName;
            set
            {
                _currentViewName = value;
                OnPropertyChanged();
            }
        }

        public DataRowView SelectedRow
        {
            get => _selectedRow;
            set
            {
                _selectedRow = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsRowSelected));
            }
        }

        public bool IsRowSelected => SelectedRow != null;

        public ICommand NavigateCommand { get; }
        public ICommand ExportToJsonCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SaveCommand { get; }

        public WorkWindowViewModel(string connectionString)
        {
            _connectionString = connectionString;

            NavigateCommand = new RelayCommand<string>(LoadTableData);
            ExportToJsonCommand = new RelayCommand(ExportToJson);
            ImportFromJsonCommand = new RelayCommand(ImportFromJson);
            RefreshCommand = new RelayCommand(RefreshData);
            AddCommand = new RelayCommand(AddRecord);
            EditCommand = new RelayCommand(EditRecord, () => IsRowSelected);
            DeleteCommand = new RelayCommand(DeleteRecord, () => IsRowSelected);
            SaveCommand = new RelayCommand(SaveChanges);
            DeleteCommand = new RelayCommand(DeleteRecord, () => IsRowSelected);

            // Подписываемся на изменение свойства SelectedRow
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectedRow))
                {
                    ((RelayCommand)DeleteCommand).RaiseCanExecuteChanged();
                }
            };
        }

        private void ImportFromJson()
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "JSON files (*.json)|*.json",
                    Title = "Выберите файл для импорта"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var json = File.ReadAllText(openFileDialog.FileName);
                    var dataTable = JsonConvert.DeserializeObject<DataTable>(json);

                    if (dataTable != null && dataTable.Rows.Count > 0)
                    {
                        // Проверяем соответствие структуры таблицы
                        if (IsTableStructureCompatible(dataTable))
                        {
                            ImportDataToDatabase(dataTable);
                            RefreshData();
                            MessageBox.Show("Импорт данных завершен успешно", "Успех",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("Структура импортируемых данных не соответствует текущей таблице",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Файл не содержит данных для импорта",
                            "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при импорте: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool IsTableStructureCompatible(DataTable importedTable)
        {
            if (CurrentTable == null || string.IsNullOrEmpty(_currentTableName))
                return false;

            // Сравниваем количество столбцов
            if (importedTable.Columns.Count != CurrentTable.Columns.Count)
                return false;

            // Сравниваем имена столбцов
            foreach (DataColumn column in CurrentTable.Columns)
            {
                if (!importedTable.Columns.Contains(column.ColumnName))
                    return false;
            }

            return true;
        }

        private void ImportDataToDatabase(DataTable dataTable)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                // Получаем имя первичного ключа
                string primaryKey = GetPrimaryKeyColumn(_currentTableName);

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        foreach (DataRow row in dataTable.Rows)
                        {
                            // Создаем команду для вставки
                            var columnNames = new System.Text.StringBuilder();
                            var paramNames = new System.Text.StringBuilder();

                            foreach (DataColumn column in dataTable.Columns)
                            {
                                // Пропускаем первичный ключ для автоинкрементных полей
                                if (column.ColumnName.Equals(primaryKey, StringComparison.OrdinalIgnoreCase))
                                    continue;

                                if (columnNames.Length > 0)
                                {
                                    columnNames.Append(", ");
                                    paramNames.Append(", ");
                                }

                                columnNames.Append(column.ColumnName);
                                paramNames.Append($"@{column.ColumnName}");
                            }

                            var query = $"INSERT INTO {_currentTableName} ({columnNames}) VALUES ({paramNames})";

                            using (var cmd = new NpgsqlCommand(query, connection, transaction))
                            {
                                foreach (DataColumn column in dataTable.Columns)
                                {
                                    // Пропускаем первичный ключ
                                    if (column.ColumnName.Equals(primaryKey, StringComparison.OrdinalIgnoreCase))
                                        continue;

                                    cmd.Parameters.AddWithValue($"@{column.ColumnName}", row[column]);
                                }

                                cmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private void LoadTableData(string tableName)
        {
            try
            {
                _currentTableName = GetTableQuery(tableName);
                RefreshData();
                CurrentViewName = GetViewDisplayName(tableName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshData()
        {
            if (string.IsNullOrEmpty(_currentTableName))
                return;

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    var query = $"SELECT * FROM {_currentTableName} LIMIT 100";
                    var adapter = new NpgsqlDataAdapter(query, connection);

                    var dataTable = new DataTable();
                    adapter.Fill(dataTable);
                    CurrentTable = dataTable;
                    SelectedRow = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddRecord()
        {
            if (CurrentTable == null) return;

            var newRow = CurrentTable.NewRow();
            CurrentTable.Rows.Add(newRow);
            SelectedRow = (DataRowView)CurrentTable.DefaultView[CurrentTable.Rows.Count - 1];
        }

        private void EditRecord()
        {
            if (SelectedRow == null) return;

            var editWindow = new EditWindow(SelectedRow, _currentTableName);
            if (editWindow.ShowDialog() == true)
            {
                RefreshData(); // Обновляем данные после редактирования
            }
        }

        private void DeleteRecord()
        {
            if (SelectedRow == null) return;

            try
            {
                var result = MessageBox.Show("Вы уверены, что хотите удалить эту запись?",
                    "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.No) return;

                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    // Получаем имя первичного ключа
                    string primaryKey = GetPrimaryKeyColumn(_currentTableName);

                    if (string.IsNullOrEmpty(primaryKey))
                    {
                        MessageBox.Show("Не удалось определить первичный ключ таблицы", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var id = SelectedRow[primaryKey];
                    var query = $"DELETE FROM {_currentTableName} WHERE {primaryKey} = @id";

                    using (var cmd = new NpgsqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("id", id);
                        cmd.ExecuteNonQuery();
                    }
                }

                RefreshData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении записи: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveChanges()
        {
            if (CurrentTable == null || string.IsNullOrEmpty(_currentTableName)) return;

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    // Получаем имя первичного ключа
                    string primaryKey = GetPrimaryKeyColumn(_currentTableName);

                    if (string.IsNullOrEmpty(primaryKey))
                    {
                        MessageBox.Show("Не удалось определить первичный ключ таблицы", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Создаем адаптер с командами для вставки, обновления и удаления
                    var adapter = new NpgsqlDataAdapter($"SELECT * FROM {_currentTableName}", connection);

                    var builder = new NpgsqlCommandBuilder(adapter);
                    adapter.InsertCommand = builder.GetInsertCommand();
                    adapter.UpdateCommand = builder.GetUpdateCommand();
                    adapter.DeleteCommand = builder.GetDeleteCommand();

                    // Применяем изменения
                    adapter.Update(CurrentTable);

                    MessageBox.Show("Изменения успешно сохранены", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    RefreshData();
                }
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
        public class RelayCommand : ICommand
        {
            private readonly Action _execute;
            private readonly Func<bool> _canExecute;

            public event EventHandler CanExecuteChanged;

            public RelayCommand(Action execute, Func<bool> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public bool CanExecute(object parameter) => _canExecute == null || _canExecute();

            public void Execute(object parameter) => _execute();

            public void RaiseCanExecuteChanged()
            {
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private string GetTableQuery(string viewName)
        {
            return viewName switch
            {
                "ProductsView" => "product",
                "WarehousesView" => "warehouse",
                "ReportsView" => "action_log",
                "InventoryView" => "inventory_check",
                "SuppliesView" => "goods_receipt",
                "ShipmentsView" => "goods_issue",
                "UsersView" => "app_user",
                _ => "product" // default
            };
        }

        private string GetViewDisplayName(string viewName)
        {
            return viewName switch
            {
                "ProductsView" => "Товары",
                "WarehousesView" => "Склады",
                "ReportsView" => "Отчёты",
                "InventoryView" => "Инвентаризация",
                "SuppliesView" => "Поставки",
                "ShipmentsView" => "Отгрузки",
                "UsersView" => "Пользователи",
                _ => "Данные"
            };
        }

        private void ExportToJson()
        {
            if (CurrentTable == null || CurrentTable.Rows.Count == 0)
            {
                MessageBox.Show("Нет данных для экспорта", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json",
                    FileName = $"{CurrentViewName}_{DateTime.Now:yyyyMMddHHmmss}.json"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var json = JsonConvert.SerializeObject(CurrentTable, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(saveFileDialog.FileName, json);
                    MessageBox.Show("Экспорт завершен успешно", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}