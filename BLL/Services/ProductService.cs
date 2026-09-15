using Kiddopay.BLL.DTOs;
using KiddoPay.BLL.Interfaces;
using Microsoft.Identity.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;

namespace KiddoPay.BLL.Services
{
    public class ProductService(IOrganizationService organizationService, IConfiguration config) : IProductService
    {
        private readonly IOrganizationService _org = organizationService;
        private readonly IConfiguration _config = config;

        // ──────────────────────────────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────────────────────────────

        public ProductDTO GetProductByBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                throw new ArgumentException("Barcode is required.");

            var fetch = $@"
<fetch top='1'>
  <entity name='blser_product'>
    <attribute name='blser_productid'       />
    <attribute name='blser_name'            />
    <attribute name='blser_price'           />
    <attribute name='blser_imageurl'        />
    <attribute name='blser_barcode'         />
    <attribute name='blser_isavailable'     />
    <attribute name='blser_category' />
    <filter>
      <condition attribute='blser_barcode'     operator='eq' value='{barcode}' />
      <condition attribute='blser_isavailable' operator='eq' value='1'         />
      <condition attribute='statecode'         operator='eq' value='0'         />
    </filter>
    <link-entity name='blser_productcategory' from='blser_productcategoryid'
                 to='blser_category' alias='cat' link-type='outer'>
      <attribute name='blser_name'             />
      <attribute name='blser_productcategoryid'/>
    </link-entity>
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));
            if (!result.Entities.Any())
                return null;

            var product = MapProductEntity(result.Entities[0]);
            BackfillImageIfMissing(product);
            return product;
        }

        public ScannedProductResultDTO ValidateProductForStudent(
            string barcode, Guid studentId, decimal currentCartTotal)
        {
            var product = GetProductByBarcode(barcode);
            if (product == null)
                return new ScannedProductResultDTO
                {
                    CanAdd = false,
                    WarningType = "ProductNotFound",
                    WarningMessage = "This product was not found or is currently unavailable."
                };

            // 1 ─ Check forbidden categories
            if (IsCategoryForbiddenForStudent(product.CategoryId, studentId))
                return new ScannedProductResultDTO
                {
                    Product = product,
                    CanAdd = false,
                    WarningType = "ForbiddenCategory",
                    WarningMessage = $"This product category ({product.CategoryName}) is restricted for this student."
                };

            // 2 ─ Check allergy conflicts
            var allergyConflicts = GetAllergyConflictsForProduct(product.ProductId, studentId);
            if (allergyConflicts.Any())
                return new ScannedProductResultDTO
                {
                    Product = product,
                    CanAdd = false,
                    WarningType = "AllergyAlert",
                    WarningMessage = $"Allergy alert — remove this item, {GetStudentFirstName(studentId)} can't have it.",
                    SafeAlternatives = GetSafeAlternatives(product.CategoryId, studentId)
                };

            // 3 ─ Check wallet balance (current cart total + this new item)
            var studentBalance = GetStudentWalletBalance(studentId);
            if (studentBalance < currentCartTotal + product.Price)
                return new ScannedProductResultDTO
                {
                    Product = product,
                    CanAdd = false,
                    WarningType = "LowBalance",
                    WarningMessage = $"Low balance — remove this item, {GetStudentFirstName(studentId)}'s balance isn't enough."
                };

            // All checks passed
            return new ScannedProductResultDTO
            {
                Product = product,
                CanAdd = true,
                WarningType = "None"
            };
        }

        public List<ProductDTO> GetSafeAlternatives(Guid productCategoryId, Guid studentId)
        {
            var fetch = $@"
<fetch>
  <entity name='blser_product'>
    <attribute name='blser_productid'       />
    <attribute name='blser_name'            />
    <attribute name='blser_price'           />
    <attribute name='blser_imageurl'        />
    <attribute name='blser_barcode'         />
    <attribute name='blser_isavailable'     />
    <attribute name='blser_category' />
    <filter>
      <condition attribute='blser_category'        operator='eq' value='{productCategoryId}' />
      <condition attribute='blser_isavailable'     operator='eq' value='1'                   />
      <condition attribute='statecode'             operator='eq' value='0'                   />
    </filter>
    <link-entity name='blser_productcategory' from='blser_productcategoryid'
                 to='blser_category' alias='cat' link-type='outer'>
      <attribute name='blser_name'             />
      <attribute name='blser_productcategoryid'/>
    </link-entity>
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));

            var alternatives = result.Entities
                .Select(MapProductEntity)
                .Where(p => !GetAllergyConflictsForProduct(p.ProductId, studentId).Any()
                         && !IsCategoryForbiddenForStudent(p.CategoryId, studentId))
                .ToList();

            foreach (var alt in alternatives)
                BackfillImageIfMissing(alt);

            return alternatives;
        }

        /// <summary>
        /// Returns all active product categories, ordered alphabetically.
        /// Used to populate the category sidebar in the catalog modal.
        /// </summary>
        public List<ProductCategoryDTO> GetCategories()
        {
            var fetch = @"
<fetch>
  <entity name='blser_productcategory'>
    <attribute name='blser_productcategoryid' />
    <attribute name='blser_name'              />
    <filter>
      <condition attribute='statecode' operator='eq' value='0' />
    </filter>
    <order attribute='blser_name' descending='false' />
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));

            return result.Entities
                .Select(MapCategoryEntity)
                .ToList();
        }

        /// <summary>
        /// Returns all available products in a given category.
        /// Used to populate the product grid when the cashier selects a category
        /// in the catalog modal.
        /// </summary>
        public List<ProductDTO> GetProductsByCategory(Guid categoryId)
        {
            if (categoryId == Guid.Empty)
                throw new ArgumentException("CategoryId is required.");

            var fetch = $@"
<fetch>
  <entity name='blser_product'>
    <attribute name='blser_productid'       />
    <attribute name='blser_name'            />
    <attribute name='blser_price'           />
    <attribute name='blser_imageurl'        />
    <attribute name='blser_barcode'         />
    <attribute name='blser_isavailable'     />
    <attribute name='blser_category' />
    <filter>
      <condition attribute='blser_category' operator='eq' value='{categoryId}' />
      <condition attribute='statecode'             operator='eq' value='0'            />
    </filter>
    <link-entity name='blser_productcategory' from='blser_productcategoryid'
                 to='blser_category' alias='cat' link-type='outer'>
      <attribute name='blser_name'             />
      <attribute name='blser_productcategoryid'/>
    </link-entity>
    <order attribute='blser_name' descending='false' />
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));

            var products = result.Entities
                .Select(MapProductEntity)
                .ToList();

            foreach (var product in products)
                BackfillImageIfMissing(product);

            return products;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Private helpers
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// blser_imageurl (a plain hosted-URL field) is the fast path — a bare
        /// &lt;img src&gt;, no backend round-trip. When it's empty, fall back to the
        /// native Dataverse Image column (blser_image), which the maker-portal
        /// "Image" field control actually uploads to. That column isn't reachable
        /// directly from the browser (it needs an authenticated Web API call), so
        /// we fetch it here and hand back a base64 data URI instead — same pattern
        /// PreOrderService already uses for pre-order line images.
        /// </summary>
        private void BackfillImageIfMissing(ProductDTO product)
        {
            if (!string.IsNullOrWhiteSpace(product.ImageUrl))
                return;

            try
            {
                product.ImageUrl = GetProductImageAsBase64(product.ProductId)
                    .GetAwaiter().GetResult() ?? "";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get image for product {product.ProductId}: {ex.Message}");
                product.ImageUrl = "";
            }
        }

        private async Task<string?> GetProductImageAsBase64(Guid productId)
        {
            var dataverseUrl = "https://initiumsolutionsdefault.api.crm4.dynamics.com"; // or from config

            var imageUrl = $"{dataverseUrl.TrimEnd('/')}/api/data/v9.2/blser_products({productId})/blser_image/$value";

            using var httpClient = new HttpClient();
            var token = await GetAccessToken();

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await httpClient.GetAsync(imageUrl);
            if (!response.IsSuccessStatusCode)
                return null;

            var imageBytes = await response.Content.ReadAsByteArrayAsync();
            return $"data:image/png;base64,{Convert.ToBase64String(imageBytes)}";
        }

        private async Task<string> GetAccessToken()
        {
            var tenantId = _config["AzureAd:TenantId"];
            var clientId = _config["AzureAd:ClientId"];
            var clientSecret = _config["AzureAd:ClientSecret"];

            var app = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
                .Build();

            string[] scopes = new string[] { "https://initiumsolutionsdefault.api.crm4.dynamics.com/.default" };

            var result = await app.AcquireTokenForClient(scopes).ExecuteAsync();
            return result.AccessToken;
        }

        private bool IsCategoryForbiddenForStudent(Guid categoryId, Guid studentId)
        {
            var fetch = $@"
<fetch count='1'>
  <entity name='blser_forbiddenproduct'>
    <filter>
      <condition attribute='blser_Student'         operator='eq' value='{studentId}' />
      <condition attribute='blser_ProductCategory' operator='eq' value='{categoryId}' />
      <condition attribute='blser_isactive'        operator='eq' value='1'            />
      <condition attribute='statecode'             operator='eq' value='0'            />
    </filter>
  </entity>
</fetch>";

            return _org.RetrieveMultiple(new FetchExpression(fetch)).Entities.Any();
        }

        /// <summary>
        /// Returns allergy names that conflict with a product's ingredients.
        /// Joins: product → product ingredients → ingredients → student allergy.
        /// </summary>
        private List<string> GetAllergyConflictsForProduct(Guid productId, Guid studentId)
        {
            // Step 1 — ingredient(s) this student is allergic to, with the allergy display name.
            // blser_allergies has a direct EntityReference lookup to blser_ingredient
            // (one ingredient per allergy record), so no further join is needed here.
            var allergyFetch = $@"
<fetch distinct='true'>
  <entity name='blser_studentallergy'>
    <filter>
      <condition attribute='blser_Student'  operator='eq' value='{studentId}' />
      <condition attribute='blser_isactive' operator='eq' value='1'           />
      <condition attribute='statecode'      operator='eq' value='0'           />
    </filter>
    <link-entity name='blser_allergies' from='blser_allergiesid' to='blser_Allergy'
                 alias='al' link-type='inner'>
      <attribute name='blser_ingredient'  />
      <attribute name='blser_displayname' />
      <filter>
        <condition attribute='statecode' operator='eq' value='0' />
      </filter>
    </link-entity>
  </entity>
</fetch>";

            var allergyRows = _org.RetrieveMultiple(new FetchExpression(allergyFetch)).Entities;

            var allergenNameByIngredientId = allergyRows
                .Select(e => new
                {
                    IngredientRef = e.GetAttributeValue<AliasedValue>("al.blser_ingredient")?.Value as EntityReference,
                    AllergyName   = e.GetAttributeValue<AliasedValue>("al.blser_displayname")?.Value as string,
                })
                .Where(x => x.IngredientRef != null)
                .GroupBy(x => x.IngredientRef.Id)
                .ToDictionary(g => g.Key, g => g.First().AllergyName);

            if (!allergenNameByIngredientId.Any())
                return new List<string>();

            // Step 2 — this product's ingredient-composition rows (blser_productingredient).
            var productIngredientFetch = $@"
<fetch>
  <entity name='blser_productingredient'>
    <attribute name='blser_productingredientid' />
    <filter>
      <condition attribute='blser_product' operator='eq' value='{productId}' />
      <condition attribute='statecode'     operator='eq' value='0'           />
    </filter>
  </entity>
</fetch>";

            var productIngredientIds = _org
                .RetrieveMultiple(new FetchExpression(productIngredientFetch))
                .Entities.Select(e => e.Id).ToList();

            if (!productIngredientIds.Any())
                return new List<string>();

            // Step 3 — which ingredients those composition rows are linked to, via the native
            // N:N intersect entity blser_productingredient_blser_ingredient. Queried directly
            // rather than through a FetchXML intersect="true" link, since that syntax couldn't
            // be verified against a live environment — this is the lower-risk equivalent.
            var idFilter = string.Join("", productIngredientIds
                .Select(id => $"<value>{id}</value>"));
            var intersectFetch = $@"
<fetch distinct='true'>
  <entity name='blser_productingredient_blser_ingredient'>
    <attribute name='blser_ingredientid' />
    <filter>
      <condition attribute='blser_productingredientid' operator='in'>
        {idFilter}
      </condition>
    </filter>
  </entity>
</fetch>";

            var linkedIngredientIds = _org
                .RetrieveMultiple(new FetchExpression(intersectFetch))
                .Entities
                .Select(e => e.GetAttributeValue<Guid?>("blser_ingredientid"))
                .Where(id => id.HasValue)
                .Select(id => id!.Value);

            // Step 4 — intersect this product's ingredients with the student's allergens.
            return linkedIngredientIds
                .Where(allergenNameByIngredientId.ContainsKey)
                .Select(id => allergenNameByIngredientId[id])
                .Distinct()
                .ToList();
        }

        private decimal GetStudentWalletBalance(Guid studentId)
        {
            var fetch = $@"
<fetch top='1'>
  <entity name='blser_wallet'>
    <attribute name='blser_balance' />
    <filter>
      <condition attribute='blser_student' operator='eq' value='{studentId}' />
      <condition attribute='statecode'     operator='eq' value='0'           />
    </filter>
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));
            if (!result.Entities.Any()) return 0m;
            return result.Entities[0].GetAttributeValue<Money>("blser_balance")?.Value ?? 0m;
        }

        private string GetStudentFirstName(Guid studentId)
        {
            var fetch = $@"
<fetch top='1'>
  <entity name='contact'>
    <attribute name='firstname' />
    <filter>
      <condition attribute='contactid' operator='eq' value='{studentId}' />
    </filter>
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));
            return result.Entities.FirstOrDefault()?.GetAttributeValue<string>("firstname") ?? "the student";
        }

        // ──────────────────────────────────────────────────────────────────────
        // Mappers
        // ──────────────────────────────────────────────────────────────────────

        private static ProductDTO MapProductEntity(Entity e)
        {
            // CategoryId is returned as an AliasedValue from the link-entity.
            // The alias attribute is 'cat.blser_productcategoryid'.
            var categoryIdRaw = e.GetAttributeValue<AliasedValue>("cat.blser_productcategoryid");
            var categoryId = categoryIdRaw?.Value is Guid g ? g : Guid.Empty;

            return new ProductDTO
            {
                ProductId = e.Id,
                Name = e.GetAttributeValue<string>("blser_name") ?? "",
                Price = e.GetAttributeValue<decimal?>("blser_price") ?? 0m,
                ImageUrl = e.GetAttributeValue<string>("blser_imageurl") ?? "",
                Barcode = e.GetAttributeValue<string>("blser_barcode") ?? "",
                IsAvailable = e.GetAttributeValue<bool>("blser_isavailable"),
                CategoryId = categoryId,
                CategoryName = e.GetAttributeValue<AliasedValue>("cat.blser_name")?.Value as string ?? ""
            };
        }

        private static ProductCategoryDTO MapCategoryEntity(Entity e)
        {
            return new ProductCategoryDTO
            {
                CategoryId = e.Id,
                CategoryName = e.GetAttributeValue<string>("blser_name") ?? "",
                ImageUrl = e.GetAttributeValue<string>("blser_imageurl") ?? ""
            };
        }
    }
}