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

                    // получаем SQL с JOIN-ами, если есть
                    string query = GetJoinedQuery(tableName);

                    using (var cmd = new NpgsqlCommand(query, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        dataTable.Load(reader);
                    }
                }
            }
            catch (Npgsql.PostgresException ex)
            {
                MessageBox.Show($"Ошибка доступа к таблице '{tableName}':\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Общая ошибка при загрузке таблицы '{tableName}':\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
        public DataTable GetRaw(string tableName)
        {
            var dataTable = new DataTable();

            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    string query = $"SELECT * FROM {tableName}";
                    using (var cmd = new NpgsqlCommand(query, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        dataTable.Load(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении данных для сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return dataTable;
        }

        private string GetJoinedQuery(string tableName)
        {
            return tableName.ToLower() switch
            {
                "product" => @"
            SELECT p.product_id, p.product_name, 
                   pc.category_name, 
                   m.manufacturer_name, 
                   uom.unit_name,
                   p.sku, p.description, p.price, p.expiration_date, 
                   p.weight, p.dimensions, p.min_stock_level, p.max_stock_level
            FROM product p
            LEFT JOIN product_category pc ON p.category_id = pc.category_id
            LEFT JOIN manufacturer m ON p.manufacturer_id = m.manufacturer_id
            LEFT JOIN unit_of_measure uom ON p.unit_id = uom.unit_id
        ",

                "goods_receipt" => @"
            SELECT gr.receipt_id, p.product_name, s.company_name AS supplier, 
                   w.warehouse_name, i.invoice_number, gr.quantity, gr.purchase_price, 
                   gr.receipt_date, gr.batch_expiration_date, gr.serial_number, 
                   gr.status, au.full_name AS user
            FROM goods_receipt gr
            LEFT JOIN product p ON gr.product_id = p.product_id
            LEFT JOIN supplier s ON gr.supplier_id = s.supplier_id
            LEFT JOIN warehouse w ON gr.warehouse_id = w.warehouse_id
            LEFT JOIN invoice i ON gr.invoice_id = i.invoice_id
            LEFT JOIN app_user au ON gr.user_id = au.user_id
        ",

                "goods_issue" => @"
            SELECT gi.issue_id, p.product_name, c.company_name AS customer, 
                   w.warehouse_name, i.invoice_number, gi.quantity, gi.selling_price, 
                   gi.issue_date, gi.delivery_method, gi.status, au.full_name AS user
            FROM goods_issue gi
            LEFT JOIN product p ON gi.product_id = p.product_id
            LEFT JOIN customer c ON gi.customer_id = c.customer_id
            LEFT JOIN warehouse w ON gi.warehouse_id = w.warehouse_id
            LEFT JOIN invoice i ON gi.invoice_id = i.invoice_id
            LEFT JOIN app_user au ON gi.user_id = au.user_id
        ",

                "stock_transfer" => @"
            SELECT st.transfer_id, p.product_name, 
                   sl_from.location_code AS from_location, 
                   sl_to.location_code AS to_location, 
                   st.quantity, st.transfer_date, st.reason, 
                   au.full_name AS user, st.status
            FROM stock_transfer st
            LEFT JOIN product p ON st.product_id = p.product_id
            LEFT JOIN storage_location sl_from ON st.from_location_id = sl_from.location_id
            LEFT JOIN storage_location sl_to ON st.to_location_id = sl_to.location_id
            LEFT JOIN app_user au ON st.user_id = au.user_id
        ",

                "inventory_discrepancy" => @"
    SELECT id.discrepancy_id, ic.check_id, p.product_name, 
           id.system_quantity, id.actual_quantity, id.difference, 
           id.reason, id.resolution, au.full_name AS user, id.approval_date
    FROM inventory_discrepancy id
    LEFT JOIN inventory_check ic ON id.check_id = ic.check_id
    LEFT JOIN product p ON id.product_id = p.product_id
    LEFT JOIN app_user au ON id.user_id = au.user_id
",
                "inventory_check" => @"
    SELECT ic.check_id, 
           w.warehouse_name AS warehouse,   -- 👈 Название склада вместо ID
           ic.check_type, 
           ic.start_date, 
           ic.end_date, 
           ic.status, 
           ic.notes
    FROM inventory_check ic
    LEFT JOIN warehouse w ON ic.warehouse_id = w.warehouse_id
",

                "invoice" => @"
            SELECT i.invoice_id, i.invoice_number, i.invoice_type, 
                   au.full_name AS user, i.status, i.notes
            FROM invoice i
            LEFT JOIN app_user au ON i.user_id = au.user_id
        ",

                "warehouse" => @"
            SELECT w.warehouse_id, w.warehouse_name, w.address, 
                   w.area, w.status
            FROM warehouse w
        ",

                "storage_location" => @"
            SELECT sl.location_id, w.warehouse_name, slt.type_name, sl.location_code,
                   sl.floor, sl.row, sl.section, sl.capacity, sl.current_load, sl.status
            FROM storage_location sl
            LEFT JOIN warehouse w ON sl.warehouse_id = w.warehouse_id
            LEFT JOIN storage_location_type slt ON sl.location_type_id = slt.location_type_id
        ",

                "defective_goods" => @"
            SELECT dg.defect_id, p.product_name, gr.receipt_id, 
                   dg.quantity, dg.detection_date, dg.reason, dg.status, 
                   au.full_name AS user
            FROM defective_goods dg
            LEFT JOIN product p ON dg.product_id = p.product_id
            LEFT JOIN goods_receipt gr ON dg.receipt_id = gr.receipt_id
            LEFT JOIN app_user au ON dg.user_id = au.user_id
        ",

                "action_log" => @"
            SELECT al.log_id, au.full_name AS user, al.action_time, 
                   al.action_description, al.object_type, al.old_values, al.new_values
            FROM action_log al
            LEFT JOIN app_user au ON al.user_id = au.user_id
        ",

                "app_user" => @"
    SELECT au.user_id, au.full_name, au.username, r.role_name AS role, au.status
    FROM app_user au
    LEFT JOIN user_role r ON au.role_id = r.role_id
",




                _ => $"SELECT * FROM {tableName}"
            };
        }



    }
}