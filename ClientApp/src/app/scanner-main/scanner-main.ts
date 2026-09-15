import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { StudentService } from '../services/student.service';
import { ItemsService } from '../services/items.service';

@Component({
  selector: 'app-scanner-main',
  imports: [],
  templateUrl: './scanner-main.html',
  styleUrl: './scanner-main.scss',
})
export class ScannerMain {
  private router = inject(Router);
  private studentService = inject(StudentService);
  private itemsService = inject(ItemsService);

  isScanning = signal(false);
  errorMessage = signal<string | null>(null);

  /**
   * Simulates an NFC bracelet scan.
   * In production, replace the hardcoded UID with the value emitted by
   * the NFC hardware reader (e.g. via a WebSocket or USB HID event).
   */
  onScanClick(): void {
    if (this.isScanning()) return;

    // TODO: Replace with real NFC UID from hardware reader
    const nfcUid = '1234';

    this.isScanning.set(true);
    this.errorMessage.set(null);
    this.itemsService.resetSession();   // clear any previous student's session

    this.studentService.scanBracelet(nfcUid).subscribe({
      next: student => {
        this.isScanning.set(false);
        // Route to the pre-order page regardless — PreorderPage handles both
        // pre-order fulfillment and direct-scan flows.
        this.router.navigate(['/preorder']);
      },
      error: err => {
        this.isScanning.set(false);
        this.errorMessage.set(
          err?.error?.message ?? 'Student not found. Please try again.',
        );
      },
    });
  }
}
