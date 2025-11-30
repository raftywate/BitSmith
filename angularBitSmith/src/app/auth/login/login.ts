import { Component, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { AuthServiceContract } from '../../services/auth.contract';
import { Router, RouterModule } from '@angular/router';
import { AuthResponse } from '../../models/auth-response';
import { HttpErrorResponse } from '@angular/common/http';

@Component({
  selector: 'app-login',
  imports: [],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  //Dependency Injection
  private fb = inject(FormBuilder);
  private authService = inject(AuthServiceContract);
  private router = inject(Router);

  //State Management with Signals
  isLoading = signal(false);
  errorMessage = signal<string | null>(null);

  //Reactive Form definition
  loginForm : FormGroup = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required]
  });

  get email() {return this.loginForm.get('email');}
  get password() {return this.loginForm.get('password');}

  constructor() {
    //if the user is already logged in, redirect them to the home page
    this.authService.isLoggedIn$.subscribe(isLoggedIn => {
      if(isLoggedIn) {
        this.router.navigate(['/problems']);
      }
    });
  }

  onSubmit() : void {
    this.errorMessage.set(null);

    if(this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    const {email, password} = this.loginForm.value;

    this.authService.login({email, password}).subscribe({
      next : (response : AuthResponse) => {
        this.router.navigate(['/problems']);
      },
      error : (err : HttpErrorResponse) => {
        this.isLoading.set(false);
        if(err.status == 401) {
          this.errorMessage.set('Invalid email or password. Please try again.');
        } else if(err.status == 429) {
          this.errorMessage.set('Too many login attemots. Please try again later.');
        } else {
          this.errorMessage.set('An unexpected error occurred during login.');
        }
        console.error('Login Error: ', err);
      },
      complete: () => {
        this.isLoading.set(false);
      }
    });
  }
}