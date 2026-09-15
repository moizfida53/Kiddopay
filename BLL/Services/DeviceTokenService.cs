using Kiddopay.BLL.DTOs;
using Kiddopay.BLL.Interfaces;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace KiddoPay.BLL.Services
{
    public class DeviceTokenService(IOrganizationService organizationService) : IDeviceTokenService
    {
        private readonly IOrganizationService _org = organizationService;

        public void RegisterToken(Guid parentId, RegisterDeviceTokenRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Token))
                throw new ArgumentException("Device token is required.");

            if (parentId == Guid.Empty)
                throw new ArgumentException("A valid parent identity is required.");

            // Upsert by token value -- see IDeviceTokenService for why. This
            // requires a new blser_devicetoken table (blser_token, blser_parent
            // lookup to the Parent table, blser_platform, blser_lastregisteredon)
            // -- see the Firebase push-notification setup guide for the Dataverse
            // schema change this depends on.
            //
            // parentId comes from the caller's own JWT (see
            // DeviceTokensController), already resolved to a row in the Parent
            // table (blser_parent) -- Contact is used for Student records in
            // this system, parents have their own table.
            var existingFetch = $@"
<fetch top='1'>
  <entity name='blser_devicetoken'>
    <attribute name='blser_devicetokenid' />
    <filter>
      <condition attribute='blser_token' operator='eq' value='{request.Token}' />
    </filter>
  </entity>
</fetch>";

            var existing = _org.RetrieveMultiple(new FetchExpression(existingFetch)).Entities.FirstOrDefault();

            var entity = new Entity("blser_devicetoken", existing?.Id ?? Guid.Empty)
            {
                ["blser_token"] = request.Token,
                ["blser_parent"] = new EntityReference("blser_parent", parentId),
                // blser_platform is a Choice column (confirmed in the maker
                // portal: Android=550220000, iOS=550220001), not plain text --
                // a raw string here throws "Incorrect attribute value type
                // System.String". A single OptionSetValue also gets rejected
                // ("Incorrect attribute value type Microsoft.Xrm.Sdk.OptionSetValue")
                // -- that's the signature of a MULTI-select Choice ("Choices")
                // column, which Dataverse always stores/expects as an
                // OptionSetValueCollection even when only one value is set.
                // See: https://learn.microsoft.com/en-us/dynamics365/customerengagement/on-premises/developer/multi-select-picklist
                ["blser_platform"] = new OptionSetValueCollection { new OptionSetValue(MapPlatform(request.Platform)) },
                ["blser_lastregisteredon"] = DateTime.UtcNow,
            };

            if (existing != null)
                _org.Update(entity);
            else
                _org.Create(entity);
        }

        public List<string> GetTokensForParent(Guid parentId)
        {
            var fetch = $@"
<fetch>
  <entity name='blser_devicetoken'>
    <attribute name='blser_token' />
    <filter>
      <condition attribute='blser_parent' operator='eq' value='{parentId}' />
      <condition attribute='statecode'    operator='eq' value='0'          />
    </filter>
  </entity>
</fetch>";

            return _org.RetrieveMultiple(new FetchExpression(fetch)).Entities
                .Select(e => e.GetAttributeValue<string>("blser_token"))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();
        }

        public void DeactivateToken(string token)
        {
            var fetch = $@"
<fetch top='1'>
  <entity name='blser_devicetoken'>
    <attribute name='blser_devicetokenid' />
    <filter>
      <condition attribute='blser_token' operator='eq' value='{token}' />
    </filter>
  </entity>
</fetch>";

            var existing = _org.RetrieveMultiple(new FetchExpression(fetch)).Entities.FirstOrDefault();
            if (existing == null) return;

            // Assumes the platform-generated defaults for a new custom table:
            // statecode 1 (Inactive) / statuscode 2. Verify these against the
            // actual table once it's created in the maker portal -- if a
            // custom status reason was configured, this update will need the
            // matching statuscode value instead.
            _org.Update(new Entity("blser_devicetoken", existing.Id)
            {
                ["statecode"] = new OptionSetValue(1),
                ["statuscode"] = new OptionSetValue(2),
            });
        }

        private static int MapPlatform(string platform) => platform?.ToLower() switch
        {
            "android" => 550220000,
            "ios"     => 550220001,
            _         => throw new ArgumentException($"Unknown platform '{platform}'. Expected 'Android' or 'iOS'.")
        };
    }
}
