using System.Collections.Generic;

namespace VirtoCommerce.Storefront.Model.Catalog
{
    public partial class Product
    {
        public ICollection<Product> ConfigurableProducts { get; set; } = new List<Product>();
    }
}
