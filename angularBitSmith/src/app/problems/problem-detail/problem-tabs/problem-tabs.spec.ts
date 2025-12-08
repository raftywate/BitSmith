import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ProblemTabs } from './problem-tabs';

describe('ProblemTabs', () => {
  let component: ProblemTabs;
  let fixture: ComponentFixture<ProblemTabs>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProblemTabs]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ProblemTabs);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
