import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { BirdService } from '../../core/services/bird.service';
import { WatchedService } from '../../core/services/watched.service';
import { FilterPanelComponent } from '../../shared/filter-panel/filter-panel.component';
import { IucnClassPipe, IucnLabelPipe } from '../../core/pipes/iucn.pipe';
import { ContinentLabelPipe } from '../../core/pipes/continent-label.pipe';
import { BirdSearchRequest, BirdSummary, FilterMeta, PagedResult } from '../../core/models/bird.model';

@Component({
  selector: 'app-bird-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FilterPanelComponent, IucnClassPipe, IucnLabelPipe, ContinentLabelPipe],
  styles: [`
    :host { display: block; }
    .layout { display: grid; grid-template-columns: 260px 1fr; gap: 1.5rem; align-items: start; }
    @media(max-width:900px) { .layout { grid-template-columns: 1fr; } .sidebar-col { display: none; } }
    .result-meta { display: flex; align-items: center; gap: .75rem; margin-bottom: 1rem; flex-wrap: wrap; }
    .result-count { font-size: var(--text-xs); color: var(--color-text-muted); font-variant-numeric: tabular-nums; }
    .loading-badge { display: flex; align-items: center; gap: .35rem; font-size: var(--text-xs); background: var(--color-accent-light); color: var(--color-accent); padding: .2rem .65rem; border-radius: var(--radius-full); }
  `],
  template: `
    <div class="page-header">
      <div>
        <h1 class="page-title">Vogelsoorten</h1>
        <p class="page-subtitle">Live data — GBIF · iNaturalist · Xeno-canto</p>
      </div>
    </div>

    <!-- Stats -->
    <div class="stats-row">
      <div class="stat-card">
        <div class="stat-value">{{ (result()?.total ?? 0) | number:'1.0-0':'nl' }}</div>
        <div class="stat-label">Soorten gevonden</div>
      </div>
      <div class="stat-card">
        <div class="stat-value" style="color:var(--color-accent)">{{ filterMeta()?.continents?.length ?? 7 }}</div>
        <div class="stat-label">Continenten</div>
      </div>
      <div class="stat-card">
        <div class="stat-value" style="color:var(--color-nt)">{{ filterMeta()?.orders?.length ?? 0 }}</div>
        <div class="stat-label">Ordes</div>
      </div>
      <div class="stat-card">
        <div class="stat-value">{{ watched.count() }}</div>
        <div class="stat-label">Mijn waarnemingen</div>
      </div>
    </div>

    <!-- Layout -->
    <div class="layout">
      <!-- Filter sidebar -->
      <div class="sidebar-col">
        <app-filter-panel
          [meta]="filterMeta()"
          [initial]="activeReq()"
          (filterChange)="onFilter($event)">
        </app-filter-panel>
      </div>

      <!-- Bird grid -->
      <div>
        <!-- Mobile filter chips -->
        <div class="filter-bar" style="margin-bottom:1rem">
          <span class="filter-chip" [class.active]="!activeReq().continent" (click)="onFilter({offset:0})">Alles</span>
          <span class="filter-chip" [class.active]="activeReq().continent === 'EUROPE'"   (click)="setCont('EUROPE')">🇪🇺 Europa</span>
          <span class="filter-chip" [class.active]="activeReq().continent === 'AFRICA'"   (click)="setCont('AFRICA')">🌍 Afrika</span>
          <span class="filter-chip" [class.active]="activeReq().continent === 'ASIA'"     (click)="setCont('ASIA')">🌏 Azië</span>
          <span class="filter-chip" [class.active]="activeReq().continent === 'OCEANIA'"  (click)="setCont('OCEANIA')">🌊 Oceanië</span>
        </div>

        <div class="result-meta">
          <span class="result-count" *ngIf="result()">
            {{ result()!.offset + 1 }}–{{ result()!.offset + result()!.items.length }}
            van {{ result()!.total | number:'1.0-0':'nl' }} soorten
          </span>
          <span class="loading-badge" *ngIf="loading()">
            <div class="spinner" style="width:12px;height:12px;border-width:2px"></div>
            Ophalen...
          </span>
        </div>

        <!-- Grid -->
        <div class="bird-grid" *ngIf="result()?.items?.length">
          <div class="bird-card"
            *ngFor="let bird of result()!.items; trackBy: trackByKey"
            [routerLink]="['/birds', bird.gbifKey]"
            role="link" tabindex="0"
            [attr.aria-label]="bird.commonName">
            <div class="bird-card-img">
              <img *ngIf="bird.thumbnailUrl"
                [src]="bird.thumbnailUrl" [alt]="bird.commonName"
                width="200" height="150" loading="lazy"
                style="width:100%;height:150px;object-fit:cover"
                (error)="onImgError($event)">
              <span *ngIf="!bird.thumbnailUrl" role="img" [attr.aria-label]="bird.commonName">🐦</span>
            </div>
            <div class="bird-card-body">
              <div class="bird-card-name">{{ bird.commonName }}</div>
              <div class="bird-card-sci">{{ bird.scientificName }}</div>
              <div class="bird-card-fam">{{ bird.family }}</div>
            </div>
            <div class="bird-card-footer">
              <span class="status-badge" [ngClass]="bird.conservationStatus | iucnClass">
                {{ bird.conservationStatus ?? 'NE' }}
              </span>
              <span class="cont-flags" *ngIf="bird.continents?.length">
                <span *ngFor="let c of bird.continents.slice(0,3)">{{ c | continentLabel:'flag' }}</span>
              </span>
              <button class="btn-icon" style="width:28px;height:28px"
                (click)="toggleWatch($event, bird)"
                [attr.aria-label]="watched.isWatched(bird.gbifKey) ? 'Verwijder uit lijst' : 'Voeg toe aan lijst'">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                  [attr.fill]="watched.isWatched(bird.gbifKey) ? 'var(--color-primary)' : 'none'"
                  [style.stroke]="watched.isWatched(bird.gbifKey) ? 'var(--color-primary)' : 'currentColor'">
                  <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
                </svg>
              </button>
            </div>
          </div>
        </div>

        <!-- Skeleton loader -->
        <div class="bird-grid" *ngIf="loading() && !result()?.items?.length">
          <div *ngFor="let _ of skeletons" class="bird-card" style="pointer-events:none">
            <div class="skeleton" style="height:150px"></div>
            <div style="padding:.75rem">
              <div class="skeleton skeleton-text" style="height:14px;width:70%"></div>
              <div class="skeleton skeleton-text" style="height:11px;width:50%;margin-top:.35rem"></div>
            </div>
          </div>
        </div>

        <!-- Empty -->
        <div class="empty-state" *ngIf="!loading() && result()?.items?.length === 0">
          <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
            <circle cx="11" cy="11" r="8"/><path d="M21 21l-4.35-4.35"/>
            <line x1="8" y1="11" x2="14" y2="11"/>
          </svg>
          <h3>Geen vogels gevonden</h3>
          <p>Probeer een andere zoekopdracht of filter.</p>
          <button class="btn btn-outline" (click)="onFilter({offset:0})">Filters wissen</button>
        </div>

        <!-- Pagination -->
        <div class="pagination" *ngIf="result() && result()!.total > result()!.limit">
          <button class="btn btn-outline" [disabled]="result()!.offset === 0" (click)="prev()">‹ Vorige</button>
          <span class="page-info">
            Pagina {{ currentPage() }} van {{ totalPages() }}
          </span>
          <button class="btn btn-outline"
            [disabled]="result()!.offset + result()!.limit >= result()!.total"
            (click)="next()">Volgende ›</button>
        </div>
      </div>
    </div>
  `
})
export class BirdListComponent implements OnInit {
  private svc   = inject(BirdService);
  private route = inject(ActivatedRoute);
  watched = inject(WatchedService);

  result     = signal<PagedResult<BirdSummary> | null>(null);
  filterMeta = signal<FilterMeta | null>(null);
  loading    = signal(false);
  activeReq  = signal<BirdSearchRequest>({ limit: 24, offset: 0 });
  skeletons  = Array(12);

  ngOnInit(): void {
    this.svc.getFilters().subscribe(m => this.filterMeta.set(m));
    this.route.queryParams.subscribe(params => {
      const req: BirdSearchRequest = { limit: 24, offset: 0 };
      if (params['continent']) req.continent = params['continent'];
      if (params['search'])    req.search    = params['search'];
      if (params['order'])     req.order     = params['order'];
      this.activeReq.set(req);
      this.load();
    });
  }

  load(): void {
    this.loading.set(true);
    this.svc.search(this.activeReq()).subscribe({
      next: r => { this.result.set(r); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  onFilter(req: BirdSearchRequest): void {
    this.activeReq.set({ ...this.activeReq(), ...req, limit: 24, offset: 0 });
    this.load();
  }

  setCont(cont: string): void {
    const cur = this.activeReq().continent;
    this.onFilter({ continent: cur === cont ? '' : cont, offset: 0 });
  }

  next(): void {
    const r = this.result()!;
    this.activeReq.set({ ...this.activeReq(), offset: r.offset + r.limit });
    this.load();
  }

  prev(): void {
    const r = this.result()!;
    this.activeReq.set({ ...this.activeReq(), offset: Math.max(0, r.offset - r.limit) });
    this.load();
  }

  currentPage = () => this.result() ? Math.floor(this.result()!.offset / this.result()!.limit) + 1 : 1;
  totalPages  = () => this.result() ? Math.ceil(this.result()!.total / this.result()!.limit) : 1;

  trackByKey = (_: number, b: BirdSummary) => b.gbifKey;

  toggleWatch(e: Event, bird: BirdSummary): void {
    e.preventDefault(); e.stopPropagation();
    this.watched.toggle(bird);
  }

  onImgError(e: Event): void {
    (e.target as HTMLImageElement).style.display = 'none';
  }
}
