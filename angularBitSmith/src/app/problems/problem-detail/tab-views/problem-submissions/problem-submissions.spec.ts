import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ProblemSubmissions } from './problem-submissions';

describe('ProblemSubmissions', () => {
  let component: ProblemSubmissions;
  let fixture: ComponentFixture<ProblemSubmissions>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProblemSubmissions]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ProblemSubmissions);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
