using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PagedList.Core;
using VirtoCommerce.Storefront.Common;
using VirtoCommerce.Storefront.Infrastructure;
using VirtoCommerce.Storefront.Model;
using VirtoCommerce.Storefront.Model.Catalog;
using VirtoCommerce.Storefront.Model.Common;
using VirtoCommerce.Storefront.Model.Order;
using VirtoCommerce.Storefront.Model.Order.Services;
using VirtoCommerce.Storefront.Model.Services;

namespace VirtoCommerce.Storefront.Controllers
{
    [StorefrontRoute]
    public class CatalogSearchController : StorefrontControllerBase
    {
        private readonly ICatalogService _searchService;
        private readonly ICustomerOrderService _customerOrderService;

        public CatalogSearchController(
            IWorkContextAccessor workContextAccessor,
            IStorefrontUrlBuilder urlBuilder,
            ICatalogService searchService,
            ICustomerOrderService customerOrderService
            )
            : base(workContextAccessor, urlBuilder)
        {
            _searchService = searchService;
            _customerOrderService = customerOrderService;
        }

        /// GET search
        /// This method used for search products by given criteria 
        /// <returns></returns>
        [HttpGet("search")]
        public ActionResult SearchProducts()
        {
            //All resulting categories, products and aggregations will be lazy evaluated when view will be rendered. (workContext.Products, workContext.Categories etc) 
            //All data will loaded using by current search criteria taken from query string
            return View("search", WorkContext);
        }

        /// <summary>
        /// GET search/{categoryId}?view=...
        /// This method called from SeoRoute when url contains slug for category
        /// </summary>
        /// <param name="categoryId"></param>
        /// <param name="view"></param>
        /// <returns></returns>
        [HttpGet("category/{categoryId}")]
        [Route("search/{categoryId}")]
        public async Task<ActionResult> CategoryBrowsing(string categoryId, string view)
        {
            var category = (await _searchService.GetCategoriesAsync(new[] { categoryId }, CategoryResponseGroup.Full)).FirstOrDefault();
            if (category == null)
            {
                return NotFound($"Category {categoryId} not found.");
            }

            WorkContext.CurrentCategory = category;
            WorkContext.CurrentPageSeo = category.SeoInfo.JsonClone();
            WorkContext.CurrentPageSeo.Slug = category.Url;

            var criteria = (ProductSearchCriteria) WorkContext.CurrentProductSearchCriteria.Clone();
            criteria.Outline = category.Outline; // should we simply take it from current category?
            
            if (criteria.IsSelectOnlyPurchasedProducts && WorkContext.CurrentUser.IsRegisteredUser)
            {
                var customerOrders = await _customerOrderService.SearchOrdersAsync(
                    new OrderSearchCriteria
                    {
                        CustomerId = WorkContext.CurrentUser.Id,
                        StoreIds = new [] { WorkContext.CurrentStore.Id },
                        Sort = "CreatedDate:DESC",
                        PageSize = 5, // Actually this field is Take field
                    });

                criteria.ObjectIds = customerOrders
                    .SelectMany(x =>
                    {
                        var result = x.Items.Select(i => i.ProductId).ToList();
                        result.AddRange(x.ConfiguredGroups.Select(cg => cg.ProductId));

                        return result;
                    })
                    .ToArray();
            }

            category.Products = new MutablePagedList<Product>((pageNumber, pageSize, sortInfos, @params) =>
            {
                criteria.PageNumber = pageNumber;
                criteria.PageSize = pageSize;
                criteria.ResponseGroup = ItemResponseGroup.Default | ItemResponseGroup.ItemProperties;
                if (string.IsNullOrEmpty(criteria.SortBy) && !sortInfos.IsNullOrEmpty())
                {
                    criteria.SortBy = SortInfo.ToString(sortInfos);
                }
                if (@params != null)
                {
                    criteria.CopyFrom(@params);
                }
                var result = _searchService.SearchProducts(criteria);
                //Need change ProductSearchResult with preserve reference because Scriban engine keeps this reference and use new operator will create the new
                //object that doesn't tracked by Scriban
                WorkContext.ProductSearchResult.Aggregations = result.Aggregations;
                WorkContext.ProductSearchResult.Products = result.Products;

                return result.Products;
            }, 1, ProductSearchCriteria.DefaultPageSize);

            WorkContext.ProductSearchResult = new CatalogSearchResult(criteria)
            {
                Products = category.Products,
                Category = category
            };
              

            // make sure title is set
            if (string.IsNullOrEmpty(WorkContext.CurrentPageSeo.Title))
            {
                WorkContext.CurrentPageSeo.Title = category.Name;
            }
            //Lazy initialize category breadcrumbs
            WorkContext.Breadcrumbs = new MutablePagedList<Breadcrumb>((pageNumber, pageSize, sortInfos, @params) =>
            {
                var breadcrumbs = WorkContext.ProductSearchResult.GetBreadcrumbs().ToList();
                return new StaticPagedList<Breadcrumb>(breadcrumbs, pageNumber, pageSize, breadcrumbs.Count);
            }, 1, int.MaxValue);

            if (string.IsNullOrEmpty(view))
            {
                view = "grid";
            }

            if (view.Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                return View("collection.list", WorkContext);
            }

            return View("collection", WorkContext);
        }
    }
}
