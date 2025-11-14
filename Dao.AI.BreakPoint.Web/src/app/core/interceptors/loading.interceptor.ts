import { HttpInterceptorFn } from '@angular/common/http';

export const loadingInterceptor: HttpInterceptorFn = (req, next) => {
  // TODO: dao on every request start, show loading spinner
  return next(req);
};
