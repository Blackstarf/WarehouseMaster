using System.ComponentModel;
using System.Data;
using System.Runtime.CompilerServices;

namespace WarehouseMaster.Core
{
    public class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    public interface IRepository<T>
    {
        IEnumerable<T> GetAll();
        T GetById(int id);
        void Add(T entity);
        void Update(T entity);
        void Delete(int id);
        DataTable GetAllAsDataTable();
    }
    public interface ITableRepository
    {
        DataTable GetAll(string tableName);
        DataTable GetRaw(string tableName);
        void Update(DataTable dataTable, string tableName);
        void Delete(int id, string tableName);
        string GetPrimaryKeyColumn(string tableName);
    }
}
