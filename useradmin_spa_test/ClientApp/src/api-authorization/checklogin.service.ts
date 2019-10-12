import { Injectable } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, RouterStateSnapshot, Router } from '@angular/router';
import { Observable } from 'rxjs';
import { AuthorizeService } from './authorize.service';
import { tap } from 'rxjs/operators';
import { ApplicationPaths, QueryParameterNames } from './api-authorization.constants';

@Injectable({
    providedIn: 'root'
})

export class CheckLoginService {
    first: boolean;

    constructor(private authorize: AuthorizeService) {
        this.first = true;
    }

    public Check(
        router: Router,
        _next: ActivatedRouteSnapshot,
        state: RouterStateSnapshot): boolean {

        if (!this.first) {
            return true;
        }
        this.first = false;
        router.navigate(['/home']);
        
       /* if (!isAuthenticated) {
            this.router.navigate(ApplicationPaths.LoginPathComponents, {
                queryParams: {
                    [QueryParameterNames.ReturnUrl]: state.url
                }
            });
        }*/
    }
}
