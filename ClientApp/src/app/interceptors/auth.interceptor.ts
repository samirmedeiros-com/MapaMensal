import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  if (auth.token() && req.url.startsWith('/api')) {
    req = req.clone({ setHeaders: { Authorization: `Bearer ${auth.token()}` } });
  }
  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 && req.url.startsWith('/api')) {
        auth.logout();
      }
      return throwError(() => err);
    })
  );
};
