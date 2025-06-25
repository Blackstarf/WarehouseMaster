using System.Data;
using System.Windows;

namespace WarehouseMaster
{
    public partial class EditWindow : Window
    {
        public EditWindow(DataRowView row, string tableName)
        {
            InitializeComponent();
            DataContext = new EditWindowViewModel(row, tableName, this);
        }
    }
}
