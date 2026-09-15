using Kiddopay.BLL.DTOs;
using Kiddopay.BLL.Interfaces;
using KiddoPay.BLL.Interfaces;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace KiddoPay.BLL.Services
{
    public class OrderService(IOrganizationService organizationService) : IOrderService
    {
        private readonly IOrganizationService _org = organizationService;

        // ──────────────────────────────────────────────────────────────────────
        // Complete Order — the main cashier action
        // ──────────────────────────────────────────────────────────────────────

        public OrderResultDTO CompleteOrder(CompleteOrderRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (!request.Lines.Any())
                throw new InvalidOperationException("Cannot complete an order with no items.");

            // ── 1. Fetch current wallet balance (snapshot for audit) ──────────
            var (walletId, balanceBefore) = GetStudentWallet(request.StudentId);
            if (walletId == Guid.Empty)
                throw new InvalidOperationException("Student wallet not found.");

            // Pre-order lines were already paid for when the pre-order was placed
            // (see blser_preorder.blser_totalpaid) — fulfilling one here is just the
            // cashier handing the items over, not a new purchase. Only the freshly
            // scanned/added lines represent money changing hands right now, so only
            // those count toward the balance check, the daily limit, and the wallet
            // deduction below. Summing every line (including pre-order ones) into
            // "orderTotal" here would silently re-charge the student's wallet for
            // items already paid for, and would double-count that value against the
            // daily limit on every subsequent order today via GetTodaysSpend().
            var orderTotal = request.Lines
                .Where(l => !l.IsFromPreOrder)
                .Sum(l => l.UnitPrice * l.Quantity);

            if (orderTotal > 0)
            {
                if (balanceBefore < orderTotal)
                    throw new InvalidOperationException(
                        $"Insufficient balance. Required: {orderTotal:F3} KWD, Available: {balanceBefore:F3} KWD.");

                // ── 1b. Enforce the student's daily spend limit, if one is configured ─
                // A null or zero blser_dailyallowance is treated as "no limit set" — confirm
                // this matches intent before relying on it. Skipped entirely when nothing
                // is actually being charged (orderTotal == 0, e.g. a pure pre-order pickup)
                // — a same-day limit that's already been hit shouldn't block handing over
                // items that cost nothing new.
                var (dailyAllowance, dailySpentToday) = GetStudentDailyLimit(request.StudentId);
                if (dailyAllowance.HasValue && dailyAllowance.Value > 0
                    && dailySpentToday + orderTotal > dailyAllowance.Value)
                    throw new InvalidOperationException(
                        $"Daily spending limit exceeded. Limit: {dailyAllowance.Value:F3} KWD, " +
                        $"already spent today: {dailySpentToday:F3} KWD, this order: {orderTotal:F3} KWD.");
            }

            // ── 2. Create the blser_order header ─────────────────────────────
            var orderReference = GenerateOrderReference();
            var now = DateTime.UtcNow;

            var orderEntity = new Entity("blser_order")
            {
                ["blser_orderreference"]                = orderReference,
                ["blser_student"]             = new EntityReference("contact", request.StudentId),
                ["blser_cashier"]             = new EntityReference("blser_cashier", request.CashierId),
                ["blser_store"]               = new EntityReference("blser_store", request.StoreId),
                ["blser_ordertotal"]          = new Money(orderTotal),
                ["blser_orderstatus"]         = new OptionSetValue(550220001),   // Completed
                ["blser_ordertype"]           = new OptionSetValue(MapOrderType(request.OrderType)),
                ["blser_orderdatetime"]       = now,
                ["blser_walletbalancebefore"] = new Money(balanceBefore),
                ["blser_walletbalanceafter"]  = new Money(balanceBefore - orderTotal),
                ["blser_dailyspenddate"]      = now.Date
            };

            if (request.PreOrderId.HasValue)
                orderEntity["blser_preorder"] =
                    new EntityReference("blser_preorder", request.PreOrderId.Value);

            var orderId = _org.Create(orderEntity);

            // ── 3. Create blser_orderline for each item ───────────────────────
            var lineIndex = 1;
            foreach (var line in request.Lines)
            {
                var lineEntity = new Entity("blser_orderline")
                {
                    ["blser_orderlinename"] = $"{orderReference}-LINE-{lineIndex}",
                    // Requires blser_order and blser_product lookup fields on blser_orderline
                    // in Dataverse — see "Database changes" step 1. Do NOT run this against
                    // an environment that doesn't have those fields yet: CompleteOrder() will
                    // fail on every order until they exist.
                    ["blser_order"] = new EntityReference("blser_order", orderId),
                    ["blser_product"] = new EntityReference("blser_product", line.ProductId),
                    ["blser_quantity"] = line.Quantity,
                    ["blser_unitprice"] = line.UnitPrice,
                    ["blser_linetotal"] = line.UnitPrice * line.Quantity,
                    ["blser_linestatus"] = new OptionSetValue(550220000),
                    ["blser_isfrompreorder"] = line.IsFromPreOrder
                };

                if (line.IsFromPreOrder && line.PreOrderLineId.HasValue)
                    // Dataverse attribute lookups are case-sensitive — the logical name is
                    // all-lowercase "blser_preorderline" (confirmed in XRM/Entities.cs), not
                    // the PascalCase "blser_PreOrderLine" the early-bound C# property is named.
                    lineEntity["blser_preorderline"] =
                        new EntityReference("blser_preorderline", line.PreOrderLineId.Value);

                _org.Create(lineEntity);
                lineIndex++;
            }

            // ── 4. Deduct wallet balance ──────────────────────────────────────
            var walletUpdate = new Entity("blser_wallet", walletId)
            {
                ["blser_balance"] = new Money(balanceBefore - orderTotal)
            };
            _org.Update(walletUpdate);

            // ── 5. Daily spend is now computed live from today's completed orders
            // (see GetTodaysSpend) — no stored counter to update here.

            // ── 6. Update pre-order lines & header status (if applicable) ─────
            if (request.PreOrderId.HasValue)
                UpdatePreOrderFulfillment(request.PreOrderId.Value, request.Lines, orderId);

            // ── 7. Fetch student name for the response ────────────────────────
            var studentName = GetStudentName(request.StudentId);

            return new OrderResultDTO
            {
                OrderId              = orderId,
                OrderReference       = orderReference,
                StudentName          = studentName,
                OrderTotal           = orderTotal,
                WalletBalanceBefore  = balanceBefore,
                WalletBalanceAfter   = balanceBefore - orderTotal,
                Status               = "Completed",
                OrderType            = request.OrderType,
                CompletedAt          = now
            };
        }

        // ──────────────────────────────────────────────────────────────────────
        // Cancel Order
        // ──────────────────────────────────────────────────────────────────────

        public bool CancelOrder(Guid orderId)
        {
            try
            {
                var updateEntity = new Entity("blser_order", orderId)
                {
                    ["blser_orderstatus"] = new OptionSetValue(550220002)   // Cancelled
                };
                _org.Update(updateEntity);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CancelOrder failed: {ex.Message}");
                return false;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Private helpers
        // ──────────────────────────────────────────────────────────────────────

        private (decimal? dailyAllowance, decimal dailySpentToday) GetStudentDailyLimit(Guid studentId)
        {
            var fetch = $@"
<fetch top='1'>
  <entity name='contact'>
    <attribute name='blser_dailyallowance'  />
    <filter>
      <condition attribute='contactid' operator='eq' value='{studentId}' />
    </filter>
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));
            if (!result.Entities.Any())
                return (null, 0m);

            var allowance = result.Entities[0].GetAttributeValue<Money>("blser_dailyallowance")?.Value;
            return (allowance, GetTodaysSpend(studentId));
        }

        // "Spent today" used to be a running counter (blser_dailyspenttoday) that was
        // incremented on every completed order and never reset — it just grew forever
        // across days, eventually producing a negative "remaining budget" once it
        // exceeded blser_dailyallowance. Computing it live from today's completed
        // orders instead is self-resetting (no schema change needed) and always correct.
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

        private (Guid walletId, decimal balance) GetStudentWallet(Guid studentId)
        {
            var fetch = $@"
<fetch top='1'>
  <entity name='blser_wallet'>
    <attribute name='blser_walletid' />
    <attribute name='blser_balance'  />
    <filter>
      <condition attribute='blser_student' operator='eq' value='{studentId}' />
      <condition attribute='statecode'     operator='eq' value='0'           />
    </filter>
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));
            if (!result.Entities.Any())
                return (Guid.Empty, 0m);

            var wallet = result.Entities[0];
            return (wallet.Id, wallet.GetAttributeValue<Money>("blser_balance")?.Value ?? 0m);
        }


        private void UpdatePreOrderFulfillment(
            Guid preOrderId,
            System.Collections.Generic.List<OrderLineRequest> fulfilledLines,
            Guid fulfillingOrderId)
        {
            decimal totalFulfilledAmount = 0m;

            foreach (var line in fulfilledLines.Where(l => l.IsFromPreOrder && l.PreOrderLineId.HasValue))
            {
                // Fetch current pre-order line state
                var preOrderLineFetch = $@"
<fetch top='1'>
  <entity name='blser_preorderline'>
    <attribute name='blser_quantityordered'   />
    <attribute name='blser_quantityfulfilled' />
    <attribute name='blser_unitprice'         />
    <filter>
      <condition attribute='blser_preorderlineid' operator='eq' value='{line.PreOrderLineId}' />
    </filter>
  </entity>
</fetch>";
                var lineResult = _org.RetrieveMultiple(new FetchExpression(preOrderLineFetch));
                if (!lineResult.Entities.Any()) continue;

                var lineEntity = lineResult.Entities[0];
                var ordered    = lineEntity.GetAttributeValue<int>("blser_quantityordered");
                var prevFulfilled = lineEntity.GetAttributeValue<int>("blser_quantityfulfilled");
                var unitPrice  = lineEntity.GetAttributeValue<Money>("blser_unitprice")?.Value ?? 0m;
                var newFulfilled = prevFulfilled + line.Quantity;

                int newLineStatus = newFulfilled >= ordered ? 550220002   // Fulfilled
                                  : newFulfilled > 0        ? 550220001   // PartiallyFulfilled
                                  : 550220000;  // Pending

                var lineUpdate = new Entity("blser_preorderline", line.PreOrderLineId!.Value)
                {
                    ["blser_quantityfulfilled"] = newFulfilled,
                    ["blser_linestatus"]        = new OptionSetValue(newLineStatus)
                };
                _org.Update(lineUpdate);

                totalFulfilledAmount += unitPrice * line.Quantity;
            }

            // Re-evaluate overall pre-order status
            var preOrderLineFetchAll = $@"
<fetch>
  <entity name='blser_preorderline'>
    <attribute name='blser_linestatus'       />
    <attribute name='blser_lineamount'       />
    <attribute name='blser_quantityfulfilled'/>
    <filter>
      <condition attribute='blser_preorder' operator='eq' value='{preOrderId}' />
      <condition attribute='statecode'      operator='eq' value='0'            />
    </filter>
  </entity>
</fetch>";

            var allLines = _org.RetrieveMultiple(new FetchExpression(preOrderLineFetchAll)).Entities;
            bool allFulfilled = allLines.All(l =>
                l.GetAttributeValue<OptionSetValue>("blser_linestatus")?.Value == 550220002);
            // NOTE: this used to compare against 550220000 (a pre-order *status*
            // option-set value) instead of 0 — quantityfulfilled is a plain item
            // count, so that comparison was never true and the header status could
            // never land on PartiallyFulfilled.
            bool anyFulfilled = allLines.Any(l =>
                l.GetAttributeValue<int>("blser_quantityfulfilled") > 0);

            int newPreOrderStatus = allFulfilled ? 550220002   // FullyFulfilled
                                  : anyFulfilled ? 550220001   // PartiallyFulfilled
                                  : 550220000;  // Active

            // Fetch current totalFulfilled to accumulate
            var preOrderHeaderFetch = $@"
<fetch top='1'>
  <entity name='blser_preorder'>
    <attribute name='blser_totalfulfilled' />
    <filter>
      <condition attribute='blser_preorderid' operator='eq' value='{preOrderId}' />
    </filter>
  </entity>
</fetch>";
            var headerResult = _org.RetrieveMultiple(new FetchExpression(preOrderHeaderFetch));
            var existingFulfilled = headerResult.Entities.FirstOrDefault()
                ?.GetAttributeValue<Money>("blser_totalfulfilled")?.Value ?? 0m;

            var preOrderUpdate = new Entity("blser_preorder", preOrderId)
            {
                ["blser_preorderstatus"]  = new OptionSetValue(newPreOrderStatus),
                ["blser_totalfulfilled"]  = new Money(existingFulfilled + totalFulfilledAmount),
                // Same case-sensitivity pitfall as blser_preorderline above.
                ["blser_fulfillingorder"] = new EntityReference("blser_order", fulfillingOrderId)
            };
            _org.Update(preOrderUpdate);
        }

        private string GetStudentName(Guid studentId)
        {
            var fetch = $@"
<fetch top='1'>
  <entity name='contact'>
    <attribute name='fullname' />
    <filter>
      <condition attribute='contactid' operator='eq' value='{studentId}' />
    </filter>
  </entity>
</fetch>";
            var result = _org.RetrieveMultiple(new FetchExpression(fetch));
            return result.Entities.FirstOrDefault()?.GetAttributeValue<string>("fullname") ?? "";
        }

        private string GenerateOrderReference()
        {
            var today = DateTime.UtcNow.ToString("yyyyMMdd");
            var prefix = $"ORD-{today}-";

            var fetch = $@"
<fetch top='1'>
  <entity name='blser_order'>
    <attribute name='blser_orderreference' />
    <filter>
      <condition attribute='blser_orderreference' operator='like' value='{prefix}%' />
    </filter>
    <order attribute='blser_orderreference' descending='true' />
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));
            int nextSeq = 1;

            if (result.Entities.Any())
            {
                var lastName = result.Entities[0].GetAttributeValue<string>("blser_name") ?? "";
                if (lastName.StartsWith(prefix) &&
                    int.TryParse(lastName.Substring(prefix.Length), out int seq))
                    nextSeq = seq + 1;
            }

            return $"{prefix}{nextSeq:D4}";
        }

        private static int MapOrderType(string orderType) => orderType?.ToLower() switch
        {
            "directscan"           => 550220000,
            "preorderfulfillment"  => 550220001,
            "mixed"                => 550220002,
            _                      => 550220003
        };
    }
}
