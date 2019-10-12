import { Injectable } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, RouterStateSnapshot, Router } from '@angular/router';
import { Observable } from 'rxjs';
import { AuthorizeService } from './authorize.service';
import { tap } from 'rxjs/operators';
import { ApplicationPaths, QueryParameterNames } from './api-authorization.constants';

@Injectable({
    providedIn: 'root'
})

export class CheckLoginGuard implements CanActivate {
    constructor(/*private checklogin: CheckLoginService,*/ private router: Router) {
  }
    canActivate(
        _next: ActivatedRouteSnapshot,
        state: RouterStateSnapshot): Observable<boolean> | Promise<boolean> | boolean{
        return false;//this.checklogin.Check(this.router, _next, state);
  }
}
