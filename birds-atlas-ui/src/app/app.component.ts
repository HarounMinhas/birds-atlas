import { Component, inject, OnInit, signal, HostListener } from '@angular/core';
import { CommonModule }  from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <!-- Skip link -->
    <a href="#main-content" class="sr-only">Ga naar inhoud</a>

    <!-- ── Top bar ── -->
    <header class="app-topbar">
      <a class="app-logo" routerLink="/birds" aria-label="Birds Atlas home">
        <svg width="32" height="32" viewBox="0 0 32 32" fill="none" aria-hidden="true">
          <circle cx="16" cy="16" r="14" fill="var(--color-primary-light)"/>
          <path d="M8 20C8 20 12 13 16 12C20 11 25 14 26 10C27 6 21 5 18 6C15 7 13 9 11 12C9 15 8 20 8 20Z"
            fill="var(--color-primary)"/>
          <path d="M16 12C16 12 15 16 13 20" stroke="var(--color-primary)" stroke-width="1.5"
            stroke-linecap="round" opacity="0.5"/>
          <circle cx="22" cy="9" r="2" fill="var(--color-accent)"/>
        </svg>
        <span class="logo-name">Birds Atlas</span>
      </a>

      <div class="topbar-search" role="search">
        <svg class="search-icon" width="14" height="14" viewBox="0 0 24 24" fill="none"
          stroke="currentColor" stroke-width="2" aria-hidden="true">
          <circle cx="11" cy="11" r="8"/><path d="M21 21l-4.35-4.35"/>
        </svg>
        <input type="search" placeholder="Zoek vogel, orde, familie..."
          [value]="searchQuery()"
          (input)="onSearchInput($event)"
          (keydown.enter)="onSearchEnter()"
          aria-label="Zoek vogels">
      </div>

      <div class="topbar-spacer"></div>

      <button class="btn-icon" (click)="toggleTheme()" [attr.aria-label]="'Schakel naar ' + (isDark() ? 'licht' : 'donker') + ' thema'">
        <svg *ngIf="isDark()" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <circle cx="12" cy="12" r="5"/>
          <path d="M12 1v2M12 21v2M4.22 4.22l1.42 1.42M18.36 18.36l1.42 1.42M1 12h2M21 12h2M4.22 19.78l1.42-1.42M18.36 5.64l1.42-1.42"/>
        </svg>
        <svg *ngIf="!isDark()" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/>
        </svg>
      </button>
    </header>

    <!-- ── Sidebar (desktop/tablet) ── -->
    <nav class="app-sidebar" aria-label="Hoofdnavigatie">
      <span class="sidebar-section-label">Verkennen</span>
      <a class="nav-item" routerLink="/birds" routerLinkActive="active"
        [routerLinkActiveOptions]="{exact:true}">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
          <rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/>
          <rect x="14" y="14" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/>
        </svg>
        Alle Vogels
      </a>
      <a class="nav-item" routerLink="/map" routerLinkActive="active">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
          <polygon points="1 6 1 22 8 18 16 22 23 18 23 2 16 6 8 2 1 6"/>
          <line x1="8" y1="2" x2="8" y2="18"/><line x1="16" y1="6" x2="16" y2="22"/>
        </svg>
        Verspreidingskaart
      </a>
      <a class="nav-item" routerLink="/profile" routerLinkActive="active">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
          <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
        </svg>
        Mijn Waarnemingen
      </a>

      <span class="sidebar-section-label">Continenten</span>
      <a class="nav-item" routerLink="/birds" [queryParams]="{continent:'EUROPE'}">
        🇪🇺 Europa
      </a>
      <a class="nav-item" routerLink="/birds" [queryParams]="{continent:'AFRICA'}">
        🌍 Afrika
      </a>
      <a class="nav-item" routerLink="/birds" [queryParams]="{continent:'ASIA'}">
        🌏 Azië
      </a>
      <a class="nav-item" routerLink="/birds" [queryParams]="{continent:'NORTH_AMERICA'}">
        🌎 N-Amerika
      </a>
      <a class="nav-item" routerLink="/birds" [queryParams]="{continent:'SOUTH_AMERICA'}">
        🌎 Z-Amerika
      </a>
      <a class="nav-item" routerLink="/birds" [queryParams]="{continent:'OCEANIA'}">
        🌊 Oceanië
      </a>

      <span class="sidebar-section-label">Account</span>
      <a class="nav-item" routerLink="/profile" routerLinkActive="active">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
          <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>
        </svg>
        Profiel
      </a>
    </nav>

    <!-- ── Main content ── -->
    <main id="main-content" class="app-content">
      <router-outlet></router-outlet>
    </main>

    <!-- ── Bottom nav (mobile) ── -->
    <nav class="app-bottom-nav" aria-label="Mobiele navigatie">
      <div class="bottom-nav-items">
        <a class="bottom-nav-item" routerLink="/birds" routerLinkActive="active"
          [routerLinkActiveOptions]="{exact:true}">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/>
            <rect x="14" y="14" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/>
          </svg>
          Vogels
        </a>
        <a class="bottom-nav-item" routerLink="/map" routerLinkActive="active">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <polygon points="1 6 1 22 8 18 16 22 23 18 23 2 16 6 8 2 1 6"/>
          </svg>
          Kaart
        </a>
        <a class="bottom-nav-item" routerLink="/profile" routerLinkActive="active">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
          </svg>
          Mijn lijst
        </a>
        <a class="bottom-nav-item" routerLink="/profile" routerLinkActive="active">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>
          </svg>
          Profiel
        </a>
      </div>
    </nav>
  `
})
export class AppComponent implements OnInit {
  private router = inject(Router);
  isDark   = signal(false);
  searchQuery = signal('');

  ngOnInit(): void {
    const pref = matchMedia('(prefers-color-scheme: dark)').matches;
    this.isDark.set(pref);
    document.documentElement.setAttribute('data-theme', pref ? 'dark' : 'light');
  }

  toggleTheme(): void {
    const next = !this.isDark();
    this.isDark.set(next);
    document.documentElement.setAttribute('data-theme', next ? 'dark' : 'light');
  }

  onSearchInput(e: Event): void {
    this.searchQuery.set((e.target as HTMLInputElement).value);
  }

  onSearchEnter(): void {
    const q = this.searchQuery().trim();
    if (q) this.router.navigate(['/birds'], { queryParams: { search: q } });
  }
}
