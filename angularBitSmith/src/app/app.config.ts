import { ApplicationConfig, importProvidersFrom } from '@angular/core';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { MonacoEditorModule, NgxMonacoEditorConfig } from 'ngx-monaco-editor-v2';

import { routes } from './app.routes';
import { authInterceptor } from './services/auth.interceptor';
import { AuthServiceContract } from './services/auth.contract';
import { AuthService } from './services/auth';

const monacoConfig: NgxMonacoEditorConfig = {
  baseUrl: '/assets/monaco/vs'
};

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(
      routes,
      withComponentInputBinding(),
      withInMemoryScrolling({
        scrollPositionRestoration: 'enabled',
        anchorScrolling: 'enabled'
      })
    ),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    importProvidersFrom(MonacoEditorModule.forRoot(monacoConfig)),
    {
      provide: AuthServiceContract,
      useExisting: AuthService
    }
  ]
};
