import { Component, inject, OnInit } from '@angular/core';
import { StudentCard } from './student-card/student-card';
import { ScannedItems } from './scanned-items/scanned-items';
import { NewOrderItems } from './new-items/new-items';
import { OrderSummary } from './order-summary/order-summary';
import { Preorder } from './preorder/preorder';
import { ItemsService } from 'src/app/services/items.service';
import { StudentService } from 'src/app/services/student.service';
import { PreOrderService } from 'src/app/services/preorder.service';
import { Router } from '@angular/router';

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
  private readonly router = inject(Router);

  ngOnInit(): void {
    const student = this.studentService.currentStudent();

    // Guard: if no student is loaded (e.g. direct URL navigation),
    // send the user back to the scanner screen.
    if (!student) {
      this.router.navigate(['/scanner']);
      return;
    }

    // loadActivePreOrder restores the session if one exists for this student,
    // otherwise pre-populates from the API and clears the cart.
    this.itemsService.loadActivePreOrder(student.studentId);
  }

  /** True once the cashier has selected at least one item from either panel. */
  get hasSelectedItems(): boolean {
    return (
      this.itemsService.preOrderVisibleCards().size > 0 ||
      this.itemsService.newOrderVisibleCards().size > 0
    );
  }

  /** True when the current student has an active pre-order. */
  get hasActivePreOrder(): boolean {
    return this.studentService.currentStudent()?.hasActivePreOrder ?? false;
  }
}
