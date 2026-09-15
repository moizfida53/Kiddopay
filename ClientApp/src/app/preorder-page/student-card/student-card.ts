import { Component, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { StudentService } from 'src/app/services/student.service';

@Component({
  selector: 'app-student-card',
  imports: [DecimalPipe],
  templateUrl: './student-card.html',
  styleUrl: './student-card.scss',
})
export class StudentCard {
  public readonly studentService = inject(StudentService);
}
