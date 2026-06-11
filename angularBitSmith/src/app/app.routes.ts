import { Routes } from '@angular/router';
import { Login } from './auth/login/login';
import { Register } from './auth/register/register';
import { ProblemList } from './problems/problem-list/problem-list';
import { ProblemDetailComponent } from './problems/problem-detail/problem-detail';
import { authGuard, guestGuard, adminGuard } from './services/auth.guards';
import { ProfileComponent } from './profile/profile';
import { SettingsComponent } from './settings/settings';
import { AdminPanelComponent } from './admin/admin-panel/admin-panel';
import { AdminDashboardComponent } from './admin/admin-dashboard/admin-dashboard';
import { AdminPodComponent } from './admin/admin-pod/admin-pod';

export const routes: Routes = [
  { path: '', redirectTo: '/problems', pathMatch: 'full' },
  {
    path: 'problems',
    component: ProblemList,
    title: 'Problems - Compylr'
  },
  {
    path: 'problems/:id',
    component: ProblemDetailComponent,
    title: 'Problems - Compylr'
  },
  {
    path: 'login',
    component: Login,
    canActivate: [guestGuard],
    title: 'Login - Compylr'
  },
  {
    path: 'register',
    component: Register,
    canActivate: [guestGuard],
    title: 'Register - Compylr'
  },
  {
    path: 'profile',
    component: ProfileComponent,
    canActivate: [authGuard],
    title: 'Profile - Compylr'
  },
  {
    path: 'settings',
    component: SettingsComponent,
    canActivate: [authGuard],
    title: 'Settings - Compylr'
  },
  {
    path: 'admin',
    component: AdminDashboardComponent,
    canActivate: [adminGuard],
    title: 'Admin Dashboard - Compylr'
  },
  {
    path: 'admin/pod',
    component: AdminPodComponent,
    canActivate: [adminGuard],
    title: 'Set Problem of the Day - Compylr'
  },
  {
    path: 'admin/problem',
    component: AdminPanelComponent,
    canActivate: [adminGuard],
    title: 'Add Problem - Compylr'
  },
  {
    path: 'admin/problem/edit',
    component: AdminPanelComponent,
    canActivate: [adminGuard],
    title: 'Edit Problem - Compylr'
  },
  { path: '**', redirectTo: '/problems' }
];

