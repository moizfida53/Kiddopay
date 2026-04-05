import { Component, inject, OnInit } from '@angular/core';
import { StudentCard } from './student-card/student-card';
import { ScannedItems } from './scanned-items/scanned-items';
import { NewOrderItems } from './new-items/new-items';
import { OrderSummary } from './order-summary/order-summary';
import { Preorder } from './preorder/preorder';
import { ItemsService } from 'src/app/services/items.service';
import { StudentService } from 'src/app/services/student.service';
import { PreOrderService } from 'src/app/services/preorder.service';

@Component({
  selector: 'app-preorder-page',
  imports: [StudentCard, ScannedItems, NewOrderItems, OrderSummary, Preorder],
  templateUrl: './preorder-page.html',
  styleUrl: './preorder-page.scss',
})
export class PreorderPage implements OnInit {
  public readonly itemsService = inject(ItemsService);
  public readonly studentService = inject(StudentService);
  public readonly preOrderService = inject(PreOrderService);

  // Replace with your real student-ID lookup (e.g. from auth service)
  private readonly STUDENT_ID = 'eff63907-2816-f111-8341-000d3a6793f9';

  ngOnInit(): void {
    /**
     * loadActivePreOrder internally calls restoreSession first.
     * If a session exists for this student it keeps the cart intact;
     * otherwise it pre-populates the cart from the API response.
     */
    this.itemsService.loadActivePreOrder(this.STUDENT_ID);
  }

  /** True once there is at least one visible card in either carousel */
  get hasSelectedItems(): boolean {
    return (
      this.itemsService.preOrderVisibleCards().size > 0 ||
      this.itemsService.newOrderVisibleCards().size > 0
    );
  }

  /** True when the current student has an active pre-order */
  get hasActivePreOrder(): boolean {
    return this.studentService.currentStudent()?.hasActivePreOrder ?? true;
  }
}