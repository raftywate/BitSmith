import { Routes } from '@angular/router';
import { Login } from './auth/login/login';
import { Register } from './auth/register/register';
import { ProblemList } from './problems/problem-list/problem-list';

export const routes: Routes = [
  { path: '', redirectTo: '/problems', pathMatch: 'full' }, // Redirect root to problems
  {
    path: 'problems',
    component: ProblemList,
    title: 'Problems - BitSmith'
  },
  {
    path: 'login',
    component: Login,
    title: 'Login - BitSmith'
  },
  {
    path: 'register',
    component: Register,
    title: 'Register - BitSmith'
  },
  { path: '**', redirectTo: '/problems' } // Wildcard route
];
