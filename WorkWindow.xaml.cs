using System.Configuration;
using System.Windows;

namespace WarehouseMaster
{
    public partial class WorkWindow : Window
    {
        private readonly string _connectionString;
        private WorkWindowViewModel _viewModel;
        public WorkWindow()
        {
            InitializeComponent();

            _connectionString = ConfigurationManager.ConnectionStrings["PostgreSQL"]?.ConnectionString
                               ?? "Host=localhost;Port=5432;Username=postgres;Password=sa;Database=WarehouseMaster;";

            _viewModel = new WorkWindowViewModel(_connectionString);
            DataContext = _viewModel;
        }
    }
}
