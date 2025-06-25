using System.Configuration;
using System.Windows;
using WarehouseMaster.Core;

namespace WarehouseMaster
{
    public partial class WorkWindow : Window
    {
        public WorkWindow(ITableRepository repository)
        {
            InitializeComponent();
            DataContext = new WorkWindowViewModel(repository);
        }
    }
}
