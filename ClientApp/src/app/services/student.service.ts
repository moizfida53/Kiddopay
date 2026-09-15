import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { BaseApiService } from './baseApi.service';
import { StudentProfile } from './kiddopay.models';

@Injectable({ providedIn: 'root' })
export class StudentService {
  private api = inject(BaseApiService);

  /** The student currently at the cashier counter. */
  currentStudent = signal<StudentProfile | null>(null);

  /**
   * Called when the NFC reader picks up a bracelet UID.
   * Matches: GET /api/Students?nfcUid=...
   * (StudentsController uses [Route("api/[controller]")] + [HttpGet])
   */
  scanBracelet(nfcUid: string): Observable<StudentProfile> {
    return this.api
      .get<StudentProfile>(`/api/Students?nfcUid=${nfcUid}`)
      .pipe(tap(student => this.currentStudent.set(student)));
  }

  /**
   * Refreshes the current student's profile after an order completes.
   * Matches: GET /api/Students/{studentId}
   */
  refreshStudent(studentId: string): Observable<StudentProfile> {
    return this.api
      .get<StudentProfile>(`/api/Students/${studentId}`)
      .pipe(tap(student => this.currentStudent.set(student)));
  }

  /** Clears the session — call after "Finish Order" to return to Ready-to-Scan. */
  clearStudent(): void {
    this.currentStudent.set(null);
  }
}
