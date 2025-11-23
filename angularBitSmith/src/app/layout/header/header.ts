import { Component, inject } from '@angular/core';
import { CommonModule, NgClass, AsyncPipe } from "@angular/common";
import { RouterModule } from '@angular/router';
import { ThemeService } from '../../services/theme';
import { AuthServiceContract } from '../../services/auth.contract';

@Component({
    selector: 'app-header',
    standalone: true,
    imports: [CommonModule, NgClass, RouterModule],
    templateUrl: './header.html',
    styleUrl: './header.scss'
})
export class Header {
    public themeService = inject(ThemeService);
    public authService = inject(AuthServiceContract);
}
