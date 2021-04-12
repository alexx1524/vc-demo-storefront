using System.Linq;
using VirtoCommerce.Storefront.Model.Omninet;

namespace VirtoCommerce.Storefront.Model.Order
{
    public partial class LineItem
    {
        public string LocalizedName => Product?.Properties?.GetDisplayName() ?? Name;
    }
}
