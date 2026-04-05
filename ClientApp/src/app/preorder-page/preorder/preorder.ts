import {
  Component,
  Output,
  EventEmitter,
  ViewChild,
  ElementRef,
  AfterViewInit,
  AfterViewChecked,
  inject,
  signal,
} from '@angular/core';
import { ItemsService } from 'src/app/services/items.service';
import { PreOrderService } from 'src/app/services/preorder.service';

@Component({
  selector: 'app-preorder',
  imports: [],
  templateUrl: './preorder.html',
  styleUrl: './preorder.scss',
})
export class Preorder implements AfterViewInit, AfterViewChecked {
  @Output() showItemsChange = new EventEmitter<string>();
  @ViewChild('carousel') carousel!: ElementRef<HTMLDivElement>;

  public readonly itemsService = inject(ItemsService);
  public readonly preOrderService = inject(PreOrderService);

  showLeftArrow = signal(false);
  showRightArrow = signal(false);

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
    this.showRightArrow.set(!isAtEnd && this.items.length > 0);
  }

  get items() {
    return this.itemsService.getAllItems();
  }

  get availableItems() {
  return this.items.filter(i => i.remainingQty > 0);
}

get allItemsSelected() {
  return this.items.length > 0 && this.availableItems.length === 0;
}

  onItemClick(preOrderLineId: string) {
    this.itemsService.toggleItem(preOrderLineId);
    this.showItemsChange.emit(preOrderLineId);
  }
}