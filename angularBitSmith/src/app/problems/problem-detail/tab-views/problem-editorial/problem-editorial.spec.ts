import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ProblemEditorial } from './problem-editorial';

describe('ProblemEditorial', () => {
  let component: ProblemEditorial;
  let fixture: ComponentFixture<ProblemEditorial>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProblemEditorial]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ProblemEditorial);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
