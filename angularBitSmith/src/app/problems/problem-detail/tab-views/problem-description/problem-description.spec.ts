import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ProblemDescription } from './problem-description';

describe('ProblemDescription', () => {
  let component: ProblemDescription;
  let fixture: ComponentFixture<ProblemDescription>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProblemDescription]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ProblemDescription);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
