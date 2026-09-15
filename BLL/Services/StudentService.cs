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
    <attribute name='blser_dailyallowance'  />
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
                DailySpendLimit = (e.GetAttributeValue<Money>("blser_dailyallowance")?.Value ?? 0m),
                // Computed live from today's completed orders rather than trusting the
                // old blser_dailyspenttoday counter, which was written once by OrderService
                // and never reset — see OrderService.GetTodaysSpend for the same fix there.
                DailySpentToday = GetTodaysSpend(e.Id),
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

        private decimal GetTodaysSpend(Guid studentId)
        {
            var fetch = $@"
<fetch aggregate='true'>
  <entity name='blser_order'>
    <attribute name='blser_ordertotal' alias='total_spent' aggregate='sum' />
    <filter>
      <condition attribute='blser_student'       operator='eq'    value='{studentId}' />
      <condition attribute='blser_orderstatus'   operator='eq'    value='550220001'   />
      <condition attribute='blser_orderdatetime' operator='today'                     />
      <condition attribute='statecode'           operator='eq'    value='0'           />
    </filter>
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));
            var row = result.Entities.FirstOrDefault();
            if (row == null) return 0m;

            // An aliased attribute (alias='total_spent') always comes back wrapped in
            // AliasedValue, even with no link-entity — GetAttributeValue<Money> throws
            // an InvalidCastException here instead of returning null. Unwrap it first.
            var aliased = row.GetAttributeValue<AliasedValue>("total_spent")?.Value;
            if (aliased is Money aliasedMoney) return aliasedMoney.Value;
            if (aliased is decimal aliasedDecimal) return aliasedDecimal;

            return 0m;
        }

        private Entity GetTodaysActivePreOrder(Guid studentId)
        {
            // blser_scheduleddate is a User-Local DateTime field: Dataverse stores it
            // in UTC and converts on read using whatever Dataverse timezone the CALLING
            // identity has configured. Verified live: for a pre-order the maker portal
            // (interactive user) shows scheduled "03/09/26", this API's own service
            // identity reads the exact same record back as 2026-09-02 — a full day off,
            // because the service account's Dataverse timezone isn't Kuwait's. Both
            // 'eq' and the canned 'on' operator apply that same per-user conversion to
            // whatever literal date we pass, so neither is trustworthy here no matter
            // what "today" string we compute — either would just move where the
            // mismatch shows up, not remove it. The reliable fix is to stop asking
            // Dataverse to interpret the date at all: compare the raw UTC-stored value
            // directly against an explicit UTC range for "today in Kuwait" (Kuwait is
            // UTC+3 year-round, no DST) computed here in code.
            var kuwaitOffset    = TimeSpan.FromHours(3);
            var todayKuwaitDate = (DateTime.UtcNow + kuwaitOffset).Date;
            var rangeStartUtc   = (todayKuwaitDate - kuwaitOffset).ToString("s");
            var rangeEndUtc     = (todayKuwaitDate.AddDays(1) - kuwaitOffset).ToString("s");

            var fetch = $@"
<fetch top='1'>
  <entity name='blser_preorder'>
    <attribute name='blser_preorderid' />
    <filter>
      <condition attribute='blser_student'        operator='eq'  value='{studentId}'     />
      <condition attribute='blser_scheduleddate'  operator='ge'  value='{rangeStartUtc}' />
      <condition attribute='blser_scheduleddate'  operator='lt'  value='{rangeEndUtc}'   />
      <condition attribute='statecode'            operator='eq'  value='0'               />
      <filter type='or'>
        <!-- Dataverse's real blser_preorderstatus values are 550220000+ (see
             XRM/OptionSets.cs blser_PreOrder_blser_PreOrderStatus) — a bare
             value='1' here never matched anything, which is why the cashier
             dashboard never showed a pre-order for any student. -->
        <condition attribute='blser_preorderstatus' operator='eq' value='550220000' />
        <condition attribute='blser_preorderstatus' operator='eq' value='550220001' />
      </filter>
    </filter>
    <order attribute='blser_scheduleddate' descending='true' />
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));
            return result.Entities.FirstOrDefault();
        }
    }
}
