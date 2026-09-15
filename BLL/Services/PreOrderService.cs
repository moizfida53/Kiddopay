using Kiddopay.Controllers;
using Kiddopay.BLL.DTOs;
using KiddoPay.BLL.Interfaces;
using Microsoft.Identity.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using Kiddopay.BLL.Interfaces;

namespace KiddoPay.BLL.Services
{
    public class PreOrderService(IOrganizationService _org, IConfiguration config) : IPreOrderService
    {

        // ──────────────────────────────────────────────────────────────────────
        public async Task<PreOrderDTO> GetActivePreOrderForStudent(Guid studentId)
        {
            // Mirrors StudentService.GetTodaysActivePreOrder()'s date-range/status
            // filtering: without it, this fetch matched EVERY pre-order the student
            // has ever had (any status, any date) and, with no <order> clause either,
            // Dataverse's default ordering kept returning the OLDEST one forever once
            // a second pre-order existed for the same student -- the dashboard was
            // stuck showing "preorder" even after "preorder_2" was created and made
            // Active for today. Restrict to today (Kuwait, UTC+3, no DST) and
            // Active/PartiallyFulfilled, and order by scheduled date descending so the
            // most relevant match wins even if more than one somehow still qualifies.
            var kuwaitOffset    = TimeSpan.FromHours(3);
            var todayKuwaitDate = (DateTime.UtcNow + kuwaitOffset).Date;
            var rangeStartUtc   = (todayKuwaitDate - kuwaitOffset).ToString("s");
            var rangeEndUtc     = (todayKuwaitDate.AddDays(1) - kuwaitOffset).ToString("s");

            var fetch = $@"
<fetch top='1'>
  <entity name='blser_preorder'>
    <attribute name='blser_preorderid'     />
    <attribute name='blser_preorder1'           />
    <attribute name='blser_scheduleddate'  />
    <attribute name='blser_preorderstatus' />
    <attribute name='blser_totalpaid'      />
    <attribute name='blser_totalfulfilled' />
    <attribute name='blser_student'        />
    <filter>
      <condition attribute='blser_student'       operator='eq' value='{studentId}'     />
      <condition attribute='blser_scheduleddate' operator='ge' value='{rangeStartUtc}' />
      <condition attribute='blser_scheduleddate' operator='lt' value='{rangeEndUtc}'   />
      <condition attribute='statecode'           operator='eq' value='0'               />
      <filter type='or'>
        <condition attribute='blser_preorderstatus' operator='eq' value='550220000' />
        <condition attribute='blser_preorderstatus' operator='eq' value='550220001' />
      </filter>
    </filter>
    <order attribute='blser_scheduleddate' descending='true' />
    <link-entity name='contact' from='contactid' to='blser_student'
                 alias='stu' link-type='outer'>
      <attribute name='fullname' />
    </link-entity>
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));
            if (!result.Entities.Any())
                return null;

            var preOrderEntity = result.Entities[0];
            var preOrderId = preOrderEntity.Id;

            var dto = MapPreOrderHeader(preOrderEntity);
            dto.Lines = await GetPreOrderLines(preOrderId);
            return dto;
        }

        public async Task<PreOrderDTO> GetPreOrderById(Guid preOrderId)
        {
            var fetch = $@"
<fetch top='1'>
  <entity name='blser_preorder'>
    <attribute name='blser_preorderid'     />
    <attribute name='blser_preorder1'           />
    <attribute name='blser_scheduleddate'  />
    <attribute name='blser_preorderstatus' />
    <attribute name='blser_totalpaid'      />
    <attribute name='blser_totalfulfilled' />
    <attribute name='blser_student'        />
    <filter>
      <condition attribute='blser_preorderid' operator='eq' value='{preOrderId}' />
    </filter>
    <link-entity name='contact' from='contactid' to='blser_student'
                 alias='stu' link-type='outer'>
      <attribute name='fullname' />
    </link-entity>
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));
            if (!result.Entities.Any())
                return null;

            var dto = MapPreOrderHeader(result.Entities[0]);
            dto.Lines = await GetPreOrderLines(preOrderId);
            return dto;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Private helpers
        // ──────────────────────────────────────────────────────────────────────

        private async Task<List<PreOrderLineDTO>> GetPreOrderLines(Guid preOrderId)
        {
            var fetch = $@"
<fetch>
  <entity name='blser_preorderline'>
    <attribute name='blser_preorderlineid'     />
    <attribute name='blser_preorderlinename'   />
    <attribute name='blser_product'            />
    <attribute name='blser_quantityordered'    />
    <attribute name='blser_quantityfulfilled'  />
    <attribute name='blser_unitprice'          />
    <attribute name='blser_lineamount'         />
    <attribute name='blser_linestatus'         />
    <filter>
      <condition attribute='blser_preorder' operator='eq' value='{preOrderId}' />
      <condition attribute='statecode'      operator='eq' value='0'            />
    </filter>
    <link-entity name='blser_product' from='blser_productid' to='blser_product'
                 alias='prod' link-type='outer'>
      <attribute name='blser_name'     />
    </link-entity>
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));

            var lines = new List<PreOrderLineDTO>();

            foreach (var e in result.Entities)
            {
                var productRef = e.GetAttributeValue<EntityReference>("blser_product");

                var line = new PreOrderLineDTO
                {
                    PreOrderLineId = e.Id,
                    ProductId = productRef?.Id ?? Guid.Empty,
                    ProductName = e.GetAttributeValue<AliasedValue>("prod.blser_name")?.Value as string ?? "",
                    QuantityOrdered = e.GetAttributeValue<int>("blser_quantityordered"),
                    QuantityFulfilled = e.GetAttributeValue<int>("blser_quantityfulfilled"),
                    UnitPrice = e.GetAttributeValue<Money>("blser_unitprice")?.Value ?? 0m,
                    LineAmount = e.GetAttributeValue<Money>("blser_lineamount")?.Value ?? 0m,
                    LineStatus = MapLineStatus(e.GetAttributeValue<OptionSetValue>("blser_linestatus")?.Value),
                    ImageBase64 = null
                };

                if (productRef != null)
                {
                    line.ImageBase64 = await GetProductImageAsBase64(productRef.Id);
                }

                lines.Add(line);
            }

            return lines;
        }

        // New helper method
        private async Task<string?> GetProductImageAsBase64(Guid productId)
        {
            try
            {
                var dataverseUrl = "https://initiumsolutionsdefault.api.crm4.dynamics.com"; // or from config

                var imageUrl = $"{dataverseUrl.TrimEnd('/')}/api/data/v9.2/blser_products({productId})/blser_image/$value";

                using var httpClient = new HttpClient();
                var token = await GetAccessToken();

                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await httpClient.GetAsync(imageUrl);

                if (!response.IsSuccessStatusCode)
                {
                    // Log warning if needed
                    return null;
                }

                var imageBytes = await response.Content.ReadAsByteArrayAsync();

                // Convert to base64 with data URI prefix (best for <img src>)
                return $"data:image/png;base64,{Convert.ToBase64String(imageBytes)}";
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Failed to get image for product {productId}: {ex.Message}");
                return null;
            }
        }
        private async Task<string> GetAccessToken()
        {
            var tenantId = config["AzureAd:TenantId"];
            var clientId = config["AzureAd:ClientId"];
            var clientSecret = config["AzureAd:ClientSecret"];

            var app = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
                .Build();

            string[] scopes = new string[] { "https://initiumsolutionsdefault.api.crm4.dynamics.com/.default" };

            var result = await app.AcquireTokenForClient(scopes).ExecuteAsync();
            return result.AccessToken;
        }

        private static PreOrderDTO MapPreOrderHeader(Entity e)
        {
            return new PreOrderDTO
            {
                PreOrderId     = e.Id,
                Reference      = e.GetAttributeValue<string>("blser_preorder1") ?? "",
                StudentId      = e.GetAttributeValue<EntityReference>("blser_student")?.Id ?? Guid.Empty,
                StudentName    = e.GetAttributeValue<AliasedValue>("stu.fullname")?.Value as string ?? "",
                ScheduledDate  = e.GetAttributeValue<DateTime?>("blser_scheduleddate")?.ToString("yyyy-MM-dd") ?? "",
                Status         = MapPreOrderStatus(e.GetAttributeValue<OptionSetValue>("blser_preorderstatus")?.Value),
                TotalPaid      = e.GetAttributeValue<Money>("blser_totalpaid")?.Value ?? 0m,
                TotalFulfilled = e.GetAttributeValue<Money>("blser_totalfulfilled")?.Value ?? 0m
            };
        }

        private static string MapPreOrderStatus(int? value) => value switch
        {
            550220000 => "Active",
            550220001 => "PartiallyFulfilled",
            550220002 => "FullyFulfilled",
            550220003 => "Cancelled",
            550220004 => "Unknown"
        };

        private static string MapLineStatus(int? value) => value switch
        {
            550220000 => "Pending",
            550220001 => "PartiallyFulfilled",
            550220002 => "Fulfilled",
            550220003 => "Cancelled",
            550220004 => "Unknown"
        };
    }
}
