import { Component } from '@angular/core';
import { RouterOutlet, RouterModule } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterModule],
  template: `
    <nav class="navbar navbar-expand-lg navbar-dark bg-success">
      <div class="container">
        <a class="navbar-brand fw-bold" routerLink="/birds">
          🐦 Birds Atlas
        </a>
      </div>
    </nav>
    <router-outlet></router-outlet>
  `
})
export class AppComponent {}
