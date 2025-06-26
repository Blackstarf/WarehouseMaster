using System.Windows.Controls;
using System.Windows;

namespace WarehouseMaster
{
    public class FieldTemplateSelector : DataTemplateSelector
    {
        public DataTemplate DefaultTemplate { get; set; }
        public DataTemplate LookupTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is KeyValuePair<string, FieldValue> field && field.Value.IsLookup)
            {
                return LookupTemplate;
            }

            return DefaultTemplate;
        }
    }
}
