import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { BirdService } from '../../core/services/bird.service';
import { FilterPanelComponent } from '../../shared/filter-panel/filter-panel.component';
import { BirdFilterOptions, BirdFilterRequest, BirdListItem, PagedResult } from '../../core/models/bird.model';

@Component({
  selector: 'app-bird-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FilterPanelComponent],
  template: `
    <div class="container-fluid py-4">
      <div class="row mb-4">
        <div class="col">
          <h1 class="display-5 fw-bold text-success">
            <i class="bi bi-feather me-2"></i>Birds Atlas
          </h1>
          <p class="text-muted">Ontdek vogels van over de hele wereld — gefilterd op regio, soort en kenmerken.</p>
        </div>
      </div>

      <div class="row">
        <!-- Filter sidebar -->
        <div class="col-lg-3 col-md-4">
          <app-filter-panel
            [options]="filterOptions"
            (filterChange)="onFilterChange($event)">
          </app-filter-panel>
        </div>

        <!-- Bird grid -->
        <div class="col-lg-9 col-md-8">

          <div class="d-flex justify-content-between align-items-center mb-3">
            <span class="text-muted" *ngIf="result">
              {{ result.total }} vogels gevonden
            </span>
          </div>

          <!-- Loading -->
          <div *ngIf="loading" class="text-center py-5">
            <div class="spinner-border text-success" role="status"></div>
            <p class="mt-2 text-muted">Vogels laden...</p>
          </div>

          <!-- Grid -->
          <div class="row row-cols-1 row-cols-sm-2 row-cols-xl-3 g-4" *ngIf="!loading && result">
            <div class="col" *ngFor="let bird of result.items">
              <div class="card h-100 border-0 shadow-sm" style="cursor:pointer" [routerLink]="['/birds', bird.id]">
                <img
                  [src]="bird.primaryImageUrl || 'assets/bird-placeholder.svg'"
                  class="card-img-top object-fit-cover"
                  style="height: 180px"
                  [alt]="bird.commonNameEn">
                <div class="card-body pb-1">
                  <h6 class="card-title mb-0 fw-bold">{{ bird.commonNameEn || bird.scientificName }}</h6>
                  <p class="text-muted small fst-italic mb-1">{{ bird.scientificName }}</p>
                  <p class="text-muted small mb-1">{{ bird.order }} · {{ bird.family }}</p>
                </div>
                <div class="card-footer bg-transparent border-0 pt-0">
                  <span class="badge me-1" [ngClass]="statusBadge(bird.conservationStatus)">
                    {{ bird.conservationStatus ?? 'NE' }}
                  </span>
                  <span class="badge bg-light text-dark me-1" *ngFor="let c of bird.continents.slice(0,2)">
                    {{ continentEmoji(c) }}
                  </span>
                </div>
              </div>
            </div>
          </div>

          <!-- Empty state -->
          <div *ngIf="!loading && result?.items?.length === 0" class="text-center py-5">
            <i class="bi bi-search fs-1 text-muted"></i>
            <p class="mt-2 text-muted">Geen vogels gevonden met deze filters.</p>
          </div>

          <!-- Pagination -->
          <nav *ngIf="result && result.totalPages > 1" class="mt-4">
            <ul class="pagination justify-content-center">
              <li class="page-item" [class.disabled]="!result.hasPreviousPage">
                <button class="page-link" (click)="changePage(currentPage - 1)">‹</button>
              </li>
              <li class="page-item" *ngFor="let p of pageRange">
                <button class="page-link" [class.active]="p === currentPage" (click)="changePage(p)">{{ p }}</button>
              </li>
              <li class="page-item" [class.disabled]="!result.hasNextPage">
                <button class="page-link" (click)="changePage(currentPage + 1)">›</button>
              </li>
            </ul>
          </nav>

        </div>
      </div>
    </div>
  `
})
export class BirdListComponent implements OnInit {
  private birdService = inject(BirdService);

  result: PagedResult<BirdListItem> | null = null;
  filterOptions: BirdFilterOptions | null = null;
  loading = false;
  currentPage = 1;
  activeFilter: BirdFilterRequest = { page: 1, pageSize: 24 };

  get pageRange(): number[] {
    if (!this.result) return [];
    const start = Math.max(1, this.currentPage - 2);
    const end = Math.min(this.result.totalPages, this.currentPage + 2);
    return Array.from({ length: end - start + 1 }, (_, i) => start + i);
  }

  ngOnInit(): void {
    this.birdService.getFilterOptions().subscribe(opts => this.filterOptions = opts);
    this.loadBirds();
  }

  loadBirds(): void {
    this.loading = true;
    this.birdService.getBirds({ ...this.activeFilter, page: this.currentPage }).subscribe({
      next: res => { this.result = res; this.loading = false; },
      error: () => this.loading = false
    });
  }

  onFilterChange(filter: BirdFilterRequest): void {
    this.activeFilter = filter;
    this.currentPage = 1;
    this.loadBirds();
  }

  changePage(page: number): void {
    if (!this.result || page < 1 || page > this.result.totalPages) return;
    this.currentPage = page;
    this.loadBirds();
  }

  statusBadge(status: string | null): string {
    const map: Record<string, string> = {
      LC: 'bg-success', NT: 'bg-info text-dark',
      VU: 'bg-warning text-dark', EN: 'bg-danger', CR: 'bg-dark'
    };
    return map[status ?? ''] ?? 'bg-secondary';
  }

  continentEmoji(code: string): string {
    const map: Record<string, string> = {
      EUROPE: '🇪🇺', AFRICA: '🌍', ASIA: '🌏',
      NORTH_AMERICA: '🌎', SOUTH_AMERICA: '🌎', OCEANIA: '🌊'
    };
    return map[code] ?? code;
  }
}
