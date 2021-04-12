using VirtoCommerce.Storefront.Model.Omninet;

namespace VirtoCommerce.Storefront.Model.Cart
{
    public partial class LineItem
    {
        public string LocalizedName() => Product?.Properties?.GetDisplayName() ?? Name;
    }
}
