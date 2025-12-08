import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ProblemSolutions } from './problem-solutions';

describe('ProblemSolutions', () => {
  let component: ProblemSolutions;
  let fixture: ComponentFixture<ProblemSolutions>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProblemSolutions]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ProblemSolutions);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
