import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { BirdService } from '../../core/services/bird.service';
import { WatchedService } from '../../core/services/watched.service';
import { IucnClassPipe, IucnLabelPipe } from '../../core/pipes/iucn.pipe';
import { ContinentLabelPipe } from '../../core/pipes/continent-label.pipe';
import { BirdDetail } from '../../core/models/bird.model';

@Component({
  selector: 'app-bird-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, IucnClassPipe, IucnLabelPipe, ContinentLabelPipe],
  styles: [`
    :host { display: block; }
    .section-title { font-size: var(--text-sm); font-weight: 700; color: var(--color-text-muted); text-transform: uppercase; letter-spacing: .06em; margin-bottom: var(--space-3); }
  `],
  template: `
    <div *ngIf="bird(); else loading">
      <button class="back-btn" routerLink="/birds">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <polyline points="15 18 9 12 15 6"/>
        </svg>
        Terug naar overzicht
      </button>

      <div class="detail-hero">
        <!-- Left: image + sounds -->
        <div>
          <div class="detail-img">
            <img *ngIf="bird()!.imageUrl"
              [src]="bird()!.imageUrl" [alt]="bird()!.commonName"
              style="width:100%;height:100%;object-fit:cover;border-radius:var(--radius-2xl)"
              loading="lazy" (error)="onImgError($event)">
            <span *ngIf="!bird()!.imageUrl" role="img" style="font-size:5rem">🐦</span>
          </div>
          <p class="img-credit" *ngIf="bird()!.imageUrl">
            📷 iNaturalist — Creative Commons
          </p>

          <!-- Sounds -->
          <div class="sounds-card" *ngIf="bird()!.sounds?.length">
            <div class="sounds-title">
              🔊 Vogelgeluiden
              <span class="detail-tag" style="margin-left:.35rem">Xeno-canto</span>
            </div>
            <div class="sound-item" *ngFor="let s of bird()!.sounds">
              <button class="play-btn" [attr.aria-label]="'Speel geluid af'">
                <svg width="10" height="10" viewBox="0 0 24 24" fill="currentColor"><polygon points="5 3 19 12 5 21"/></svg>
              </button>
              <div class="audio-track">
                <div class="audio-bar"><div class="audio-fill" style="width:0"></div></div>
                <div class="audio-meta">{{ s.attribution ?? 'Onbekend' }} &middot; {{ s.license ?? 'CC' }}</div>
              </div>
              <audio [src]="s.url" preload="none" style="display:none"
                #audioEl
                (timeupdate)="onTimeUpdate(audioEl, s.url)"
                (ended)="onEnded(s.url)">
              </audio>
            </div>
          </div>
        </div>

        <!-- Right: info -->
        <div>
          <h1 class="detail-name">{{ bird()!.commonName }}</h1>
          <p class="detail-sci">{{ bird()!.scientificName }}</p>

          <div class="detail-badges">
            <span class="status-badge" style="font-size:.8rem;padding:.3rem .85rem"
              [ngClass]="bird()!.conservationStatus | iucnClass">
              {{ bird()!.conservationStatus ?? 'NE' }} — {{ bird()!.conservationStatus | iucnLabel }}
            </span>
            <span class="detail-tag">{{ bird()!.order }}</span>
            <span class="detail-tag">{{ bird()!.family }}</span>
          </div>

          <p class="detail-description" *ngIf="bird()!.description">
            {{ bird()!.description }}
          </p>
          <p class="detail-description" *ngIf="!bird()!.description" style="font-style:italic">
            Geen beschrijving beschikbaar. Bekijk Wikipedia of GBIF voor meer informatie.
          </p>

          <!-- Taxonomy -->
          <div class="section-title">Taxonomie</div>
          <div class="taxonomy-grid">
            <div class="taxon-box">
              <div class="taxon-label">Orde</div>
              <div class="taxon-value">{{ bird()!.order || '—' }}</div>
            </div>
            <div class="taxon-box">
              <div class="taxon-label">Familie</div>
              <div class="taxon-value">{{ bird()!.family || '—' }}</div>
            </div>
            <div class="taxon-box">
              <div class="taxon-label">Genus</div>
              <div class="taxon-value">{{ bird()!.genus || '—' }}</div>
            </div>
            <div class="taxon-box">
              <div class="taxon-label">IUCN</div>
              <div class="taxon-value">{{ bird()!.conservationStatus ?? 'NE' }}</div>
            </div>
            <div class="taxon-box">
              <div class="taxon-label">GBIF ID</div>
              <div class="taxon-value" style="font-family:monospace;font-size:.75rem">{{ bird()!.gbifKey }}</div>
            </div>
            <div class="taxon-box" *ngIf="bird()!.inatTaxonId">
              <div class="taxon-label">iNat ID</div>
              <div class="taxon-value" style="font-family:monospace;font-size:.75rem">{{ bird()!.inatTaxonId }}</div>
            </div>
          </div>

          <!-- Continents -->
          <div class="section-title">🗺️ Verspreiding</div>
          <div class="continent-pills" *ngIf="bird()!.continents?.length">
            <span class="continent-pill" *ngFor="let c of bird()!.continents">
              {{ c | continentLabel }}
            </span>
          </div>
          <p *ngIf="!bird()!.continents?.length" style="font-size:var(--text-sm);color:var(--color-text-faint)">Verspreiding niet beschikbaar</p>

          <!-- Actions -->
          <div style="display:flex;flex-wrap:wrap;gap:.5rem;margin-top:var(--space-5)">
            <button class="btn btn-primary" (click)="toggleWatch()">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                [attr.fill]="isWatched() ? '#fff' : 'none'">
                <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
              </svg>
              {{ isWatched() ? 'Verwijder uit lijst' : 'Markeer als gezien' }}
            </button>
            <a *ngIf="bird()!.wikipediaUrl" [href]="bird()!.wikipediaUrl" target="_blank" rel="noopener noreferrer"
              class="btn btn-outline">📖 Wikipedia</a>
            <a [href]="'https://www.gbif.org/species/' + bird()!.gbifKey" target="_blank" rel="noopener noreferrer"
              class="btn btn-outline">GBIF ↗</a>
            <a *ngIf="bird()!.inatTaxonId" [href]="'https://www.inaturalist.org/taxa/' + bird()!.inatTaxonId"
              target="_blank" rel="noopener noreferrer" class="btn btn-outline">iNaturalist ↗</a>
          </div>
        </div>
      </div>
    </div>

    <ng-template #loading>
      <div class="spinner-wrap"><div class="spinner"></div></div>
    </ng-template>
  `
})
export class BirdDetailComponent implements OnInit {
  private route   = inject(ActivatedRoute);
  private svc     = inject(BirdService);
  watched = inject(WatchedService);

  bird     = signal<BirdDetail | null>(null);
  progress: Record<string, number> = {};
  playing:  string | null = null;

  ngOnInit(): void {
    const key = Number(this.route.snapshot.paramMap.get('id'));
    this.svc.getDetail(key).subscribe(b => this.bird.set(b));
  }

  isWatched = () => this.bird() ? this.watched.isWatched(this.bird()!.gbifKey) : false;

  toggleWatch(): void {
    const b = this.bird();
    if (!b) return;
    this.watched.toggle({
      gbifKey: b.gbifKey, commonName: b.commonName, scientificName: b.scientificName,
      order: b.order, family: b.family, conservationStatus: b.conservationStatus,
      thumbnailUrl: b.thumbnailUrl, continents: b.continents
    });
  }

  onTimeUpdate(el: HTMLAudioElement, url: string): void {
    if (el.duration) this.progress[url] = (el.currentTime / el.duration) * 100;
  }
  onEnded(url: string): void { this.progress[url] = 0; }

  onImgError(e: Event): void {
    (e.target as HTMLImageElement).closest('.detail-img')!.innerHTML = '<span style="font-size:5rem">🐦</span>';
  }
}
