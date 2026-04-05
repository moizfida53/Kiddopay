import {
  Component,
  inject,
  signal,
  ViewChild,
  ElementRef,
  AfterViewInit,
  AfterViewChecked,
  effect,
} from '@angular/core';
import { TitleCasePipe } from '@angular/common';
import { ItemsService } from 'src/app/services/items.service';
import { ProductService } from 'src/app/services/product.service';
import { StudentService } from 'src/app/services/student.service';

@Component({
  selector: 'app-scanned-items',
  imports: [TitleCasePipe],
  templateUrl: './scanned-items.html',
  styleUrl: './scanned-items.scss',
})
export class ScannedItems implements AfterViewInit, AfterViewChecked {
  public readonly itemsService = inject(ItemsService);
  public readonly productService = inject(ProductService);
  public readonly studentService = inject(StudentService);

  @ViewChild('carousel') carousel!: ElementRef<HTMLDivElement>;

  showLeftArrow = signal(false);
  showRightArrow = signal(false);

  constructor() {
    effect(() => {
      // Re-check arrows whenever cart or visible cards change
      this.itemsService.preOrderCartItems();
      this.itemsService.preOrderVisibleCards();
      // Defer to next tick so the DOM has updated
      setTimeout(() => this.checkArrows(), 0);
    });
  }

  ngAfterViewInit() {
    this.checkArrows();
    this.carousel.nativeElement.addEventListener('scroll', () =>
      this.checkArrows(),
    );
  }

  ngAfterViewChecked() {
    this.checkArrows();
  }

  scrollLeft() {
    const container = this.carousel.nativeElement;
    const cardWidth =
      container.querySelector('.card-item')?.clientWidth || 280;
    container.scrollBy({ left: -(cardWidth + 16), behavior: 'smooth' });
  }

  scrollRight() {
    const container = this.carousel.nativeElement;
    const cardWidth =
      container.querySelector('.card-item')?.clientWidth || 280;
    container.scrollBy({ left: cardWidth + 16, behavior: 'smooth' });
  }

  checkArrows() {
    const container = this.carousel?.nativeElement;
    if (!container) return;
    this.showLeftArrow.set(container.scrollLeft > 10);
    const isAtEnd =
      container.scrollLeft + container.clientWidth >=
      container.scrollWidth - 10;
    this.showRightArrow.set(!isAtEnd && this.cards.length > 0);
  }

  get cards() {
    return this.itemsService.preOrderCartItems();
  }

  get visibleCards() {
    return this.itemsService.preOrderVisibleCards;
  }

  /** True when max qty reached for this pre-order line */
  isAtMaxQty(preOrderLineId: string | null): boolean {
    if (!preOrderLineId) return false;
    const item = this.itemsService.preOrderCartItems().find(
      (i) => i.preOrderLineId === preOrderLineId,
    );
    if (!item || item.maxQuantity == null) return false;
    return item.quantity >= item.maxQuantity;
  }

  hideCard(preOrderLineId: string): void {
    this.itemsService.hidePreOrderCard(preOrderLineId);
  }

  increaseQty(preOrderLineId: string): void {
    this.itemsService.increasePreOrderQty(preOrderLineId);
  }

  decreaseQty(preOrderLineId: string): void {
    this.itemsService.decreasePreOrderQty(preOrderLineId);
  }

  /** Loads safe alternatives when the cashier taps the allergy alert link */
  loadSafeAlternatives(): void {
    const result = this.itemsService.lastScanResult();
    const studentId = this.studentService.currentStudent()?.studentId;
    if (!result?.product || !studentId) return;
    this.productService
      .getSafeAlternatives(result.product.categoryId, studentId)
      .subscribe();
  }

  get studentFirstName(): string {
    return (
      this.studentService.currentStudent()?.fullName?.split(' ')[0] ??
      'the student'
    );
  }

  /** True when there is an active pre-order but nothing has been selected yet */
  get showSelectPrompt(): boolean {
    return this.itemsService.hasUnselectedPreOrder();
  }
}