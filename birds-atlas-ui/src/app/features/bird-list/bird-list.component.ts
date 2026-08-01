import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { BirdService } from '../../core/services/bird.service';
import { FilterPanelComponent } from '../../shared/filter-panel/filter-panel.component';
import { BirdSearchRequest, BirdSummary, FilterMeta, PagedResult } from '../../core/models/bird.model';

@Component({
  selector: 'app-bird-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FilterPanelComponent],
  template: `
    <div class="container-fluid py-4">
      <div class="row mb-3">
        <div class="col">
          <h1 class="display-6 fw-bold text-success mb-0">
            <i class="bi bi-feather me-2"></i>Birds Atlas
          </h1>
          <p class="text-muted small mb-0">Live data via GBIF & iNaturalist &mdash; niks opgeslagen.</p>
        </div>
      </div>

      <div class="row g-4">
        <div class="col-lg-3">
          <app-filter-panel [meta]="filterMeta" (filterChange)="onFilter($event)"></app-filter-panel>
        </div>

        <div class="col-lg-9">
          <div class="d-flex align-items-center mb-3 gap-2">
            <span class="text-muted small" *ngIf="result">
              {{ result.total.toLocaleString('nl-BE') }} soorten gevonden
            </span>
            <span class="badge bg-warning text-dark" *ngIf="loading">
              <span class="spinner-border spinner-border-sm me-1"></span>Ophalen...
            </span>
          </div>

          <!-- Grid -->
          <div class="row row-cols-2 row-cols-md-3 row-cols-xl-4 g-3" *ngIf="result?.items?.length">
            <div class="col" *ngFor="let bird of result!.items">
              <div class="card h-100 border-0 shadow-sm bird-card" [routerLink]="['/birds', bird.gbifKey]">
                <img [src]="bird.thumbnailUrl || 'assets/bird-placeholder.svg'"
                  class="card-img-top" style="height:140px;object-fit:cover"
                  [alt]="bird.commonName" loading="lazy">
                <div class="card-body p-2">
                  <p class="fw-semibold mb-0 small lh-sm">{{ bird.commonName }}</p>
                  <p class="text-muted fst-italic mb-0" style="font-size:.72rem">{{ bird.scientificName }}</p>
                  <p class="text-muted mb-0" style="font-size:.7rem">{{ bird.family }}</p>
                </div>
                <div class="card-footer bg-transparent border-0 p-2 pt-0">
                  <span class="badge" [ngClass]="statusClass(bird.conservationStatus)">
                    {{ bird.conservationStatus ?? 'NE' }}
                  </span>
                </div>
              </div>
            </div>
          </div>

          <!-- Empty -->
          <div *ngIf="!loading && result?.items?.length === 0" class="text-center py-5 text-muted">
            <i class="bi bi-search fs-1"></i>
            <p class="mt-2">Geen vogels gevonden.</p>
          </div>

          <!-- Pagination -->
          <div class="d-flex justify-content-between align-items-center mt-4"
               *ngIf="result && result.total > result.limit">
            <button class="btn btn-outline-secondary btn-sm"
              [disabled]="result.offset === 0" (click)="prev()">‹ Vorige</button>
            <span class="text-muted small">
              {{ result.offset + 1 }}&ndash;{{ result.offset + result.items.length }}
              van {{ result.total.toLocaleString('nl-BE') }}
            </span>
            <button class="btn btn-outline-secondary btn-sm"
              [disabled]="result.offset + result.limit >= result.total" (click)="next()">Volgende ›</button>
          </div>
        </div>
      </div>
    </div>
    <style>
      .bird-card { cursor:pointer; transition:transform .15s, box-shadow .15s; }
      .bird-card:hover { transform:translateY(-3px); box-shadow:0 6px 20px rgba(0,0,0,.12)!important; }
    </style>
  `
})
export class BirdListComponent implements OnInit {
  private svc = inject(BirdService);
  result: PagedResult<BirdSummary> | null = null;
  filterMeta: FilterMeta | null = null;
  loading = false;
  activeReq: BirdSearchRequest = { limit: 24, offset: 0 };

  ngOnInit(): void {
    this.svc.getFilters().subscribe(m => this.filterMeta = m);
    this.load();
  }
  load(): void {
    this.loading = true;
    this.svc.search(this.activeReq).subscribe({
      next: r => { this.result = r; this.loading = false; },
      error: () => this.loading = false
    });
  }
  onFilter(req: BirdSearchRequest): void {
    this.activeReq = { ...req, limit: 24, offset: 0 };
    this.load();
  }
  next(): void { this.activeReq = { ...this.activeReq, offset: (this.activeReq.offset ?? 0) + 24 }; this.load(); }
  prev(): void { this.activeReq = { ...this.activeReq, offset: Math.max(0, (this.activeReq.offset ?? 0) - 24) }; this.load(); }

  statusClass(s: string | null): string {
    return ({ LC:'bg-success', NT:'bg-info text-dark', VU:'bg-warning text-dark', EN:'bg-danger', CR:'bg-dark' } as any)[s ?? ''] ?? 'bg-secondary';
  }
}
