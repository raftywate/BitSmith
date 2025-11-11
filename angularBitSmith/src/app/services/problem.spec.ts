import { TestBed } from '@angular/core/testing';

import { Problem } from './problem';

describe('Problem', () => {
  let service: Problem;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(Problem);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
