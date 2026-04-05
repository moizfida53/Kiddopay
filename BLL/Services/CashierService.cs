using Kiddopay.BLL.DTOs;
using KiddoPay.BLL.Interfaces;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace KiddoPay.BLL.Services
{
    public class CashierService(IOrganizationService organizationService) : ICashierService
    {
        private readonly IOrganizationService _org = organizationService;

        public CashierProfileDTO GetCashierByMicrosoftOid(string microsoftOid)
        {
            if (string.IsNullOrWhiteSpace(microsoftOid))
                throw new ArgumentException("Microsoft OID is required.");

            var fetch = $@"
<fetch top='1'>
  <entity name='blser_cashier'>
    <attribute name='blser_cashierid'    />
    <attribute name='blser_displayname'  />
    <attribute name='blser_jobtitle'     />
    <attribute name='blser_email'        />
    <attribute name='blser_Store'        />
    <filter>
      <condition attribute='blser_microsoftoid' operator='eq' value='{microsoftOid}' />
      <condition attribute='blser_isactive'     operator='eq' value='1'             />
      <condition attribute='statecode'          operator='eq' value='0'             />
    </filter>
    <link-entity name='blser_store' from='blser_storeid' to='blser_Store'
                 alias='store' link-type='outer'>
      <attribute name='blser_name' />
    </link-entity>
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));

            if (!result.Entities.Any())
                throw new UnauthorizedAccessException(
                    $"No active cashier found for Microsoft OID: {microsoftOid}");

            var e = result.Entities[0];

            return new CashierProfileDTO
            {
                CashierId   = e.Id,
                DisplayName = e.GetAttributeValue<string>("blser_displayname") ?? "",
                JobTitle    = e.GetAttributeValue<string>("blser_jobtitle") ?? "",
                Email       = e.GetAttributeValue<string>("blser_email") ?? "",
                StoreId     = e.GetAttributeValue<EntityReference>("blser_Store")?.Id ?? Guid.Empty,
                StoreName   = e.GetAttributeValue<AliasedValue>("store.blser_name")?.Value as string ?? ""
            };
        }
    }
}
