import { RouterModule } from '@angular/router';
import { CommonModule } from "@angular/common";
import { Header } from './layout/header/header';
import { Footer } from './layout/footer/footer';
import { ToastComponent } from './components/toast/toast';
import { Component, signal } from '@angular/core';

@Component({
  selector: 'app-root',
  imports: [CommonModule, RouterModule, Header, Footer, ToastComponent],
  templateUrl: './app.html',
  standalone: true,
  styleUrl: './app.scss'
})
export class App {
  //State signals
  protected readonly title = signal('angularBitSmith');
}
