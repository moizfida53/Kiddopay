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

            var orderTotal = request.Lines.Sum(l => l.UnitPrice * l.Quantity);

            if (balanceBefore < orderTotal)
                throw new InvalidOperationException(
                    $"Insufficient balance. Required: {orderTotal:F3} KWD, Available: {balanceBefore:F3} KWD.");

            // ── 2. Create the blser_order header ─────────────────────────────
            var orderReference = GenerateOrderReference();
            var now = DateTime.UtcNow;

            var orderEntity = new Entity("blser_order")
            {
                ["blser_name"]                = orderReference,
                ["blser_Student"]             = new EntityReference("contact", request.StudentId),
                ["blser_Cashier"]             = new EntityReference("blser_cashier", request.CashierId),
                ["blser_Store"]               = new EntityReference("blser_store", request.StoreId),
                ["blser_ordertotal"]          = new Money(orderTotal),
                ["blser_orderstatus"]         = new OptionSetValue(2),   // Completed
                ["blser_ordertype"]           = new OptionSetValue(MapOrderType(request.OrderType)),
                ["blser_orderdatetime"]       = now,
                ["blser_walletbalancebefore"] = new Money(balanceBefore),
                ["blser_walletbalanceafter"]  = new Money(balanceBefore - orderTotal),
                ["blser_dailyspenddate"]      = now.Date
            };

            if (request.PreOrderId.HasValue)
                orderEntity["blser_PreOrder"] =
                    new EntityReference("blser_preorder", request.PreOrderId.Value);

            var orderId = _org.Create(orderEntity);

            // ── 3. Create blser_orderline for each item ───────────────────────
            var lineIndex = 1;
            foreach (var line in request.Lines)
            {
                var lineEntity = new Entity("blser_orderline")
                {
                    ["blser_name"]           = $"{orderReference}-LINE-{lineIndex}",
                    ["blser_Order"]          = new EntityReference("blser_order", orderId),
                    ["blser_Product"]        = new EntityReference("blser_product", line.ProductId),
                    ["blser_quantity"]       = line.Quantity,
                    ["blser_unitprice"]      = new Money(line.UnitPrice),
                    ["blser_linetotal"]      = new Money(line.UnitPrice * line.Quantity),
                    ["blser_linestatus"]     = new OptionSetValue(1),   // Included
                    ["blser_isfrompreorder"] = line.IsFromPreOrder
                };

                if (line.IsFromPreOrder && line.PreOrderLineId.HasValue)
                    lineEntity["blser_PreOrderLine"] =
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

            // ── 5. Update daily spend on student contact ──────────────────────
            UpdateStudentDailySpend(request.StudentId, orderTotal);

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
                    ["blser_orderstatus"] = new OptionSetValue(3)   // Cancelled
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

        private (Guid walletId, decimal balance) GetStudentWallet(Guid studentId)
        {
            var fetch = $@"
<fetch top='1'>
  <entity name='blser_wallet'>
    <attribute name='blser_walletid' />
    <attribute name='blser_balance'  />
    <filter>
      <condition attribute='blser_Student' operator='eq' value='{studentId}' />
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

        private void UpdateStudentDailySpend(Guid studentId, decimal amount)
        {
            // Fetch current daily spent value first
            var fetch = $@"
<fetch top='1'>
  <entity name='contact'>
    <attribute name='blser_dailyspenttoday' />
    <filter>
      <condition attribute='contactid' operator='eq' value='{studentId}' />
    </filter>
  </entity>
</fetch>";

            var result = _org.RetrieveMultiple(new FetchExpression(fetch));
            if (!result.Entities.Any()) return;

            var currentSpent = result.Entities[0]
                .GetAttributeValue<Money>("blser_dailyspenttoday")?.Value ?? 0m;

            var contactUpdate = new Entity("contact", studentId)
            {
                ["blser_dailyspenttoday"] = new Money(currentSpent + amount)
            };
            _org.Update(contactUpdate);
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

                int newLineStatus = newFulfilled >= ordered ? 3   // Fulfilled
                                  : newFulfilled > 0        ? 2   // PartiallyFulfilled
                                  :                           1;  // Pending

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
      <condition attribute='blser_PreOrder' operator='eq' value='{preOrderId}' />
      <condition attribute='statecode'      operator='eq' value='0'            />
    </filter>
  </entity>
</fetch>";

            var allLines = _org.RetrieveMultiple(new FetchExpression(preOrderLineFetchAll)).Entities;
            bool allFulfilled = allLines.All(l =>
                l.GetAttributeValue<OptionSetValue>("blser_linestatus")?.Value == 3);
            bool anyFulfilled = allLines.Any(l =>
                l.GetAttributeValue<int>("blser_quantityfulfilled") > 0);

            int newPreOrderStatus = allFulfilled ? 3   // FullyFulfilled
                                  : anyFulfilled ? 2   // PartiallyFulfilled
                                  :                1;  // Active

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
                ["blser_FulfillingOrder"] = new EntityReference("blser_order", fulfillingOrderId)
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
    <attribute name='blser_name' />
    <filter>
      <condition attribute='blser_name' operator='like' value='{prefix}%' />
    </filter>
    <order attribute='blser_name' descending='true' />
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
            "directscan"           => 1,
            "preorderfulfillment"  => 2,
            "mixed"                => 3,
            _                      => 1
        };
    }
}
