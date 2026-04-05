using D365_Shared_Library.Repository;
using Kiddopay.BLL.DTOs;
using Kiddopay.BLL.Interfaces;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace KiddoPay.BLL.Services
{
    public class StudentService(IOrganizationService organizationService) : IStudentService
    {
        private readonly IOrganizationService _org = organizationService;

        // ──────────────────────────────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────────────────────────────

        public StudentProfileDTO GetStudentByNfcUid(string nfcUid)
        {
            if (string.IsNullOrWhiteSpace(nfcUid))
                throw new ArgumentException("NFC UID is required.");

            // Step 1 – resolve the bracelet → student link
            var braceletFetch = $@"
<fetch top='1'>
  <entity name='blser_nfcbracelet'>
    <attribute name='blser_nfcbraceletid' />
    <attribute name='blser_student' />
    <filter>
      <condition attribute='blser_nfcuid' operator='eq' value='{nfcUid}' />
      <condition attribute='statecode'     operator='eq' value='0'       />
    </filter>
  </entity>
</fetch>";

            var braceletResult = _org.RetrieveMultiple(new FetchExpression(braceletFetch));
            if (!braceletResult.Entities.Any())
                return null;  // unknown bracelet – UI shows "Not Found"

            var studentRef = braceletResult.Entities[0]
                .GetAttributeValue<EntityReference>("blser_student");

            if (studentRef == null)
                return null;

            return GetStudentById(studentRef.Id);
        }

        public StudentProfileDTO GetStudentById(Guid studentId)
        {
            var fetch = $@"
<fetch top='1'>
  <entity name='contact'>
    <attribute name='contactid'              />
    <attribute name='fullname'               />
    <attribute name='blser_grade'            />
    <attribute name='blser_avatarurl'        />
    <attribute name='blser_dailyspendlimit'  />
    <attribute name='blser_dailyspenttoday'  />
    <filter>
      <condition attribute='contactid'  operator='eq' value='{studentId}' />
      <condition attribute='statecode'  operator='eq' value='0'           />
    </filter>

    <!-- join wallet for live balance -->
    <link-entity name='blser_wallet' from='blser_student' to='contactid'
                 alias='w' link-type='outer'>
      <attribute name='blser_balance' />
    </link-entity>
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));
            if (!result.Entities.Any())
                return null;

            var e = result.Entities[0];

            var balance = (e.GetAttributeValue<AliasedValue>("w.blser_balance")?.Value
                           as Money)?.Value ?? 0m;

            var profile = new StudentProfileDTO
            {
                StudentId      = e.Id,
                FullName       = e.GetAttributeValue<string>("fullname") ?? "Unknown",
                Grade          = e.GetAttributeValue<string>("blser_grade") ?? "",
                AvatarUrl      = e.GetAttributeValue<string>("blser_avatarurl") ?? "",
                DailySpendLimit = (e.GetAttributeValue<Money>("blser_dailyspendlimit")?.Value ?? 0m),
                DailySpentToday = (e.GetAttributeValue<Money>("blser_dailyspenttoday")?.Value ?? 0m),
                WalletBalance  = balance
            };

            // Check for an active pre-order today
            var preOrder = GetTodaysActivePreOrder(studentId);
            profile.HasActivePreOrder = preOrder != null;
            profile.ActivePreOrderId  = preOrder?.Id;

            return profile;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Private helpers
        // ──────────────────────────────────────────────────────────────────────

        private Entity GetTodaysActivePreOrder(Guid studentId)
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            var fetch = $@"
<fetch top='1'>
  <entity name='blser_preorder'>
    <attribute name='blser_preorderid' />
    <filter>
      <condition attribute='blser_student'        operator='eq'  value='{studentId}' />
      <condition attribute='blser_preorderstatus' operator='eq'  value='1'           />
      <condition attribute='blser_scheduleddate'  operator='eq'  value='{today}'     />
      <condition attribute='statecode'            operator='eq'  value='0'           />
    </filter>
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));
            return result.Entities.FirstOrDefault();
        }
    }
}
