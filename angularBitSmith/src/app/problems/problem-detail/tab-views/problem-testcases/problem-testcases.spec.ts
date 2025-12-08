import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ProblemTestcases } from './problem-testcases';

describe('ProblemTestcases', () => {
  let component: ProblemTestcases;
  let fixture: ComponentFixture<ProblemTestcases>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProblemTestcases]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ProblemTestcases);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
