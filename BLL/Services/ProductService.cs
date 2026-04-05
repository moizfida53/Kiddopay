using Kiddopay.BLL.DTOs;
using KiddoPay.BLL.Interfaces;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KiddoPay.BLL.Services
{
    public class ProductService(IOrganizationService organizationService) : IProductService
    {
        private readonly IOrganizationService _org = organizationService;

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
    <attribute name='blser_ProductCategory' />
    <filter>
      <condition attribute='blser_barcode'     operator='eq' value='{barcode}' />
      <condition attribute='blser_isavailable' operator='eq' value='1'         />
      <condition attribute='statecode'         operator='eq' value='0'         />
    </filter>
    <link-entity name='blser_productcategory' from='blser_productcategoryid'
                 to='blser_ProductCategory' alias='cat' link-type='outer'>
      <attribute name='blser_name'             />
      <attribute name='blser_productcategoryid'/>
    </link-entity>
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));
            if (!result.Entities.Any())
                return null;

            return MapProductEntity(result.Entities[0]);
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
    <attribute name='blser_ProductCategory' />
    <filter>
      <condition attribute='blser_ProductCategory' operator='eq' value='{productCategoryId}' />
      <condition attribute='blser_isavailable'     operator='eq' value='1'                   />
      <condition attribute='statecode'             operator='eq' value='0'                   />
    </filter>
    <link-entity name='blser_productcategory' from='blser_productcategoryid'
                 to='blser_ProductCategory' alias='cat' link-type='outer'>
      <attribute name='blser_name'             />
      <attribute name='blser_productcategoryid'/>
    </link-entity>
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));

            return result.Entities
                .Select(MapProductEntity)
                .Where(p => !GetAllergyConflictsForProduct(p.ProductId, studentId).Any()
                         && !IsCategoryForbiddenForStudent(p.CategoryId, studentId))
                .ToList();
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
    <attribute name='blser_imageurl'          />
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
    <attribute name='blser_ProductCategory' />
    <filter>
      <condition attribute='blser_ProductCategory' operator='eq' value='{categoryId}' />
      <condition attribute='statecode'             operator='eq' value='0'            />
    </filter>
    <link-entity name='blser_productcategory' from='blser_productcategoryid'
                 to='blser_ProductCategory' alias='cat' link-type='outer'>
      <attribute name='blser_name'             />
      <attribute name='blser_productcategoryid'/>
    </link-entity>
    <order attribute='blser_name' descending='false' />
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));

            return result.Entities
                .Select(MapProductEntity)
                .ToList();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Private helpers
        // ──────────────────────────────────────────────────────────────────────

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
            var fetch = $@"
<fetch distinct='true'>
  <entity name='blser_studentallergy'>
    <attribute name='blser_studentallergyid' />
    <filter>
      <condition attribute='blser_Student'  operator='eq' value='{studentId}' />
      <condition attribute='blser_isactive' operator='eq' value='1'           />
      <condition attribute='statecode'      operator='eq' value='0'           />
    </filter>
    <link-entity name='blser_allergies' from='blser_allergiesid' to='blser_Allergy'
                 alias='al' link-type='inner'>
      <attribute name='blser_displayname' />
      <link-entity name='blser_ingredients' from='blser_allergies' to='blser_allergiesid'
                   alias='ing' link-type='inner'>
        <link-entity name='blser_productingredients' from='blser_Ingredient' to='blser_ingredientsid'
                     alias='pi' link-type='inner'>
          <filter>
            <condition attribute='blser_Product' operator='eq' value='{productId}' />
          </filter>
        </link-entity>
      </link-entity>
    </link-entity>
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));

            return result.Entities
                .Select(e => e.GetAttributeValue<AliasedValue>("al.blser_displayname")?.Value as string)
                .Where(name => !string.IsNullOrEmpty(name))
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
      <condition attribute='blser_Student' operator='eq' value='{studentId}' />
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
                Price = e.GetAttributeValue<Money>("blser_price")?.Value ?? 0m,
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