using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.Storefront.Model.Omninet;

namespace VirtoCommerce.Storefront.Model.Catalog
{
    public partial class Product
    {
        public ICollection<Product> ConfigurableProducts { get; set; } = new List<Product>();

        public string LocalizedName() => Properties?.GetDisplayName() ?? Title;
    }
}
