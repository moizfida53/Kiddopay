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
import { AddItemModal } from 'src/app/preorder-page/add-item-modal/add-item-modal'

@Component({
  selector: 'app-new-order-items',
  imports: [TitleCasePipe, AddItemModal],
  templateUrl: './new-items.html',
  styleUrl: './new-items.scss',
})
export class NewOrderItems implements AfterViewInit, AfterViewChecked {
  public readonly itemsService = inject(ItemsService);
  public readonly productService = inject(ProductService);
  public readonly studentService = inject(StudentService);

  @ViewChild('carousel') carousel!: ElementRef<HTMLDivElement>;

  showLeftArrow = signal(false);
  showRightArrow = signal(false);

  /** Controls visibility of the catalog modal */
  showCatalogModal = signal(false);

  constructor() {
    effect(() => {
      this.itemsService.newOrderCartItems();
      this.itemsService.newOrderVisibleCards();
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
    const cardWidth = container.querySelector('.card-item')?.clientWidth || 280;
    container.scrollBy({ left: -(cardWidth + 16), behavior: 'smooth' });
  }

  scrollRight() {
    const container = this.carousel.nativeElement;
    const cardWidth = container.querySelector('.card-item')?.clientWidth || 280;
    container.scrollBy({ left: cardWidth + 16, behavior: 'smooth' });
  }

  checkArrows() {
    const container = this.carousel?.nativeElement;
    if (!container) return;
    this.showLeftArrow.set(container.scrollLeft > 10);
    const isAtEnd =
      container.scrollLeft + container.clientWidth >= container.scrollWidth - 10;
    this.showRightArrow.set(!isAtEnd && this.cards.length > 0);
  }

  get cards() {
    return this.itemsService.newOrderCartItems();
  }

  get visibleCards() {
    return this.itemsService.newOrderVisibleCards;
  }

  get hasCards(): boolean {
    return this.cards.length > 0;
  }

  openCatalog(): void {
    this.showCatalogModal.set(true);
  }

  closeCatalog(): void {
    this.showCatalogModal.set(false);
  }

  hideCard(productId: string): void {
    this.itemsService.hideNewOrderCard(productId);
  }

  increaseQty(productId: string): void {
    this.itemsService.increaseNewOrderQty(productId);
  }

  decreaseQty(productId: string): void {
    this.itemsService.decreaseNewOrderQty(productId);
  }

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
}