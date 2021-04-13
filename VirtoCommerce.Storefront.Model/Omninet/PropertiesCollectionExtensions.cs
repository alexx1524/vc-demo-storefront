using System.Linq;

namespace VirtoCommerce.Storefront.Model.Omninet
{
    public static class PropertiesCollectionExtensions
    {
        public static string GetDisplayName(this Common.IMutablePagedList<Catalog.CatalogProperty> properties)
        {
            var nameProperty =
                properties?.FirstOrDefault(x => x.Name == OmninetConstants.DisplayNamePropertyName);

            if (nameProperty != null && !string.IsNullOrWhiteSpace(nameProperty.Value))
            {
                return nameProperty.Value;
            }

            return null;
        }
    }
}
