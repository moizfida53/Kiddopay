import { Component, inject, OnInit } from '@angular/core';
import { StudentService } from 'src/app/services/student.service';

@Component({
  selector: 'app-student-card',
  imports: [],
  templateUrl: './student-card.html',
  styleUrl: './student-card.scss',
})
export class StudentCard implements OnInit{
  public readonly studentService = inject(StudentService);
  
  ngOnInit(): void {
    
    console.log(this.studentService.currentStudent);
  }

  
}