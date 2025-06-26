using System.Data;
using Npgsql;
using System.Windows.Input;
using System.Windows;
using WarehouseMaster.Core;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.IO;

namespace WarehouseMaster
{
    public class WorkWindowViewModel : BaseViewModel
    {
        private readonly ITableRepository _repository;
        private DataTable _currentTable;
        private string _currentTableName;
        private readonly string _connectionString;
        private string _currentViewName;
        private DataRowView _selectedRow;
        public ICommand ImportFromJsonCommand { get; }
        public ICommand NavigateCommand { get; }
        public ICommand ExportToJsonCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SaveCommand { get; }
        public bool IsRowSelected => SelectedRow != null;
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

        public WorkWindowViewModel(ITableRepository repository)
        {
            _repository = repository;

            NavigateCommand = new RelayCommand<string>(LoadTableData);
            ExportToJsonCommand = new RelayCommand(ExportToJson);
            ImportFromJsonCommand = new RelayCommand(ImportFromJson);
            RefreshCommand = new RelayCommand(RefreshData);
            AddCommand = new RelayCommand(AddRecord);
            EditCommand = new RelayCommand(EditRecord, () => IsRowSelected);
            DeleteCommand = new RelayCommand(DeleteRecord, () => IsRowSelected);
            SaveCommand = new RelayCommand(SaveChanges);
            DeleteCommand = new RelayCommand(DeleteRecord, () => IsRowSelected);

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

            if (importedTable.Columns.Count != CurrentTable.Columns.Count)
                return false;

            foreach (DataColumn column in CurrentTable.Columns)
            {
                if (!importedTable.Columns.Contains(column.ColumnName))
                    return false;
            }

            return true;
        }

        private void ImportDataToDatabase(DataTable dataTable)
        {
            try
            {
                string primaryKey = _repository.GetPrimaryKeyColumn(_currentTableName);

                foreach (DataRow row in dataTable.Rows)
                {
                    var newRow = CurrentTable.NewRow();

                    foreach (DataColumn column in dataTable.Columns)
                    {
                        if (!column.ColumnName.Equals(primaryKey, StringComparison.OrdinalIgnoreCase))
                        {
                            newRow[column.ColumnName] = row[column];
                        }
                    }

                    CurrentTable.Rows.Add(newRow);
                }

                _repository.Update(CurrentTable, _currentTableName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при импорте данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
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
                CurrentTable = _repository.GetAll(_currentTableName);
                SelectedRow = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
                RefreshData();
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

                string primaryKey = _repository.GetPrimaryKeyColumn(_currentTableName);
                if (string.IsNullOrEmpty(primaryKey))
                {
                    MessageBox.Show("Не удалось определить первичный ключ таблицы", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var id = SelectedRow[primaryKey];
                _repository.Delete((int)id, _currentTableName);
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
            if (CurrentTable == null || string.IsNullOrEmpty(_currentTableName))
                return;

            try
            {
                var realTable = _repository.GetRaw(_currentTableName);
                var pkColumn = _repository.GetPrimaryKeyColumn(_currentTableName);

                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                foreach (DataRow row in CurrentTable.Rows)
                {
                    if (row.RowState != DataRowState.Modified)
                        continue;

                    var setClauses = new List<string>();
                    var command = new NpgsqlCommand();
                    command.Connection = connection;

                    foreach (DataColumn column in realTable.Columns)
                    {
                        if (column.ColumnName.Equals(pkColumn, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!CurrentTable.Columns.Contains(column.ColumnName))
                            continue;

                        // только если поле действительно изменено
                        if (row[column.ColumnName, DataRowVersion.Current]?.ToString() !=
                            row[column.ColumnName, DataRowVersion.Original]?.ToString())
                        {
                            var paramName = $"@{column.ColumnName}";
                            setClauses.Add($"{column.ColumnName} = {paramName}");
                            command.Parameters.AddWithValue(paramName, row[column.ColumnName]);
                        }
                    }

                    if (setClauses.Count == 0)
                        continue;

                    command.CommandText = $@"
                UPDATE {_currentTableName}
                SET {string.Join(", ", setClauses)}
                WHERE {pkColumn} = @id";
                    command.Parameters.AddWithValue("@id", row[pkColumn]);

                    command.ExecuteNonQuery();
                }

                CurrentTable.AcceptChanges();
                MessageBox.Show("Изменения успешно сохранены", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                _ => "product" 
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
    }

}