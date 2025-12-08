import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ProblemCode } from './problem-code';

describe('ProblemCode', () => {
  let component: ProblemCode;
  let fixture: ComponentFixture<ProblemCode>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProblemCode]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ProblemCode);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
