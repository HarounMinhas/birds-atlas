import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { WatchedService } from '../../core/services/watched.service';
import { IucnClassPipe } from '../../core/pipes/iucn.pipe';
import { ContinentLabelPipe } from '../../core/pipes/continent-label.pipe';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, RouterModule, IucnClassPipe, ContinentLabelPipe],
  styles: [`
    :host { display: block; }
    .empty-list { text-align: center; padding: var(--space-16) var(--space-8); color: var(--color-text-faint); }
    .empty-list p { margin-top: var(--space-3); font-size: var(--text-sm); }
    .section-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: var(--space-4); }
    .section-title { font-size: var(--text-lg); font-weight: 600; }
  `],
  template: `
    <!-- Hero -->
    <div class="profile-hero">
      <div class="profile-avatar">🦅</div>
      <div>
        <h1 style="font-family:var(--font-display);font-size:var(--text-xl);line-height:1.1">Mijn Vogellijst</h1>
        <p style="opacity:.85;font-size:var(--text-sm);margin-top:.25rem">Persoonlijke waarnemingen</p>
        <div class="profile-stats">
          <div class="profile-stat">
            <div class="num">{{ watched.count() }}</div>
            <div class="lbl">Soorten gezien</div>
          </div>
          <div class="profile-stat">
            <div class="num">{{ uniqueContinents() }}</div>
            <div class="lbl">Continenten</div>
          </div>
          <div class="profile-stat">
            <div class="num">{{ uniqueOrders() }}</div>
            <div class="lbl">Ordes</div>
          </div>
        </div>
      </div>
    </div>

    <!-- List -->
    <div class="section-header">
      <h2 class="section-title">Waargenomen vogels</h2>
      <button class="btn btn-outline" *ngIf="watched.count() > 0" (click)="clearAll()"
        style="font-size:var(--text-xs)">
        Alles wissen
      </button>
    </div>

    <div class="seen-grid" *ngIf="watched.count() > 0">
      <div class="seen-item" *ngFor="let bird of watched.list()" [routerLink]="['/birds', bird.gbifKey]">
        <div class="seen-thumb">
          <img *ngIf="bird.thumbnailUrl" [src]="bird.thumbnailUrl" [alt]="bird.commonName"
            width="48" height="48" loading="lazy" style="width:100%;height:100%;object-fit:cover">
          <span *ngIf="!bird.thumbnailUrl" role="img">🐦</span>
        </div>
        <div style="flex:1;min-width:0">
          <div style="font-size:var(--text-sm);font-weight:600;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">{{ bird.commonName }}</div>
          <div style="font-size:var(--text-xs);font-style:italic;color:var(--color-text-muted);overflow:hidden;text-overflow:ellipsis">{{ bird.scientificName }}</div>
        </div>
        <div style="display:flex;align-items:center;gap:.3rem">
          <span class="status-badge" style="font-size:.6rem" [ngClass]="bird.conservationStatus | iucnClass">
            {{ bird.conservationStatus ?? 'NE' }}
          </span>
          <button class="btn-icon" style="width:28px;height:28px;flex-shrink:0"
            (click)="remove($event, bird.gbifKey)"
            aria-label="Verwijder uit lijst">
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>
            </svg>
          </button>
        </div>
      </div>
    </div>

    <!-- Empty -->
    <div class="empty-list" *ngIf="watched.count() === 0">
      <svg width="56" height="56" viewBox="0 0 24 24" fill="none" stroke="var(--color-primary-highlight)" stroke-width="1.5">
        <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
      </svg>
      <p>Nog geen vogels gemarkeerd als gezien.</p>
      <a routerLink="/birds" class="btn btn-primary" style="margin-top:var(--space-4)">Verken vogels</a>
    </div>
  `
})
export class ProfileComponent {
  watched = inject(WatchedService);

  uniqueContinents = () => new Set(this.watched.list().flatMap(b => b.continents)).size;
  uniqueOrders     = () => new Set(this.watched.list().map(b => b.order)).size;

  remove(e: Event, key: number): void {
    e.preventDefault(); e.stopPropagation();
    this.watched.remove(key);
  }

  clearAll(): void { this.watched.list().forEach(b => this.watched.remove(b.gbifKey)); }
}
