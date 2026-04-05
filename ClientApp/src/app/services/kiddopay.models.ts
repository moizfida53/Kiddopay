// ─── Student ──────────────────────────────────────────────────────────────────

export interface StudentProfile {
  studentId: string;
  fullName: string;
  grade: string;
  avatarUrl: string;
  walletBalance: number;
  dailySpendLimit: number;
  dailySpentToday: number;
  remainingDailyBudget: number;
  hasActivePreOrder: boolean;
  activePreOrderId: string | null;
}

// ─── Product ──────────────────────────────────────────────────────────────────

export interface Product {
  productId: string;
  name: string;
  categoryName: string;
  categoryId: string;
  price: number;
  imageUrl: string;
  barcode: string;
  isAvailable: boolean;
}

export interface ScannedProductResult {
  product: Product | null;
  /** 'None' | 'AllergyAlert' | 'LowBalance' | 'ForbiddenCategory' | 'ProductNotFound' */
  warningType: string;
  warningMessage: string;
  canAdd: boolean;
  safeAlternatives: Product[];
}

// ─── Cart ─────────────────────────────────────────────────────────────────────

export interface CartItem {
  productId: string;
  productName: string;
  categoryName: string;
  imageUrl: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
  isFromPreOrder: boolean;
  preOrderLineId: string | null;
  /**
   * Maximum quantity allowed for this item.
   * For pre-order items: set to the quantityOrdered from the pre-order line.
   * For fresh/new-order items: null (unlimited).
   */
  maxQuantity: number | null;
}

// ─── Pre-Order ────────────────────────────────────────────────────────────────

export interface PreOrderLine {
  preOrderLineId: string;
  productId: string;
  productName: string;
  imageUrl: string;
  imageBase64?: string;
  quantityOrdered: number;
  quantityFulfilled: number;
  unitPrice: number;
  lineAmount: number;
  /** 'Pending' | 'PartiallyFulfilled' | 'Fulfilled' | 'Cancelled' */
  lineStatus: string;
  isFullyFulfilled: boolean;
}

export interface PreOrder {
  preOrderId: string;
  reference: string;
  studentId: string;
  studentName: string;
  scheduledDate: string;
  /** 'Active' | 'PartiallyFulfilled' | 'FullyFulfilled' | 'Cancelled' */
  status: string;
  totalPaid: number;
  totalFulfilled: number;
  lines: PreOrderLine[];
}

// ─── Complete Order Request / Response ───────────────────────────────────────

export interface OrderLineRequest {
  productId: string;
  quantity: number;
  unitPrice: number;
  isFromPreOrder: boolean;
  preOrderLineId: string | null;
}

export interface CompleteOrderRequest {
  studentId: string;
  cashierId: string;
  storeId: string;
  preOrderId: string | null;
  /** 'DirectScan' | 'PreOrderFulfillment' | 'Mixed' */
  orderType: string;
  lines: OrderLineRequest[];
}

export interface OrderResult {
  orderId: string;
  orderReference: string;
  studentName: string;
  orderTotal: number;
  walletBalanceBefore: number;
  walletBalanceAfter: number;
  status: string;
  orderType: string;
  completedAt: string;
}

// ─── Cashier ──────────────────────────────────────────────────────────────────

export interface CashierProfile {
  cashierId: string;
  displayName: string;
  jobTitle: string;
  email: string;
  storeId: string;
  storeName: string;
}

// ─── Session Persistence ──────────────────────────────────────────────────────

/**
 * Shape stored in sessionStorage to survive page reloads.
 * Keyed by studentId so sessions don't bleed across students.
 */
export interface PersistedSession {
  studentId: string;
  preOrderCartItems: CartItem[];
  newOrderCartItems: CartItem[];
  preOrderVisibleCards: string[];   // serialised Set<string>
  newOrderVisibleCards: string[];
}