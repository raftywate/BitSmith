import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { Header } from './layout/header/header';
import { Footer } from './layout/footer/footer';
import { ToastComponent } from './components/toast/toast';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';

@Component({
  selector: 'app-root',
  imports: [CommonModule, RouterOutlet, Header, Footer, ToastComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private readonly router = inject(Router);

  protected readonly isWorkspaceRoute = signal(this.matchesWorkspaceRoute(this.router.url));

  constructor() {
    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(event => {
        this.isWorkspaceRoute.set(this.matchesWorkspaceRoute(event.urlAfterRedirects));
      });
  }

  private matchesWorkspaceRoute(url: string) {
    return /^\/problems\/[^/?#]+/.test(url);
  }
}
