import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { BirdService } from '../../core/services/bird.service';
import { BirdDetail } from '../../core/models/bird.model';

@Component({
  selector: 'app-bird-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="container py-4" *ngIf="bird">
      <a routerLink="/birds" class="btn btn-outline-secondary btn-sm mb-3">
        ‹ Terug naar overzicht
      </a>

      <div class="row g-4">
        <!-- Foto -->
        <div class="col-md-5">
          <img
            [src]="bird.primaryImageUrl || 'assets/bird-placeholder.svg'"
            [alt]="bird.commonNameEn"
            class="img-fluid rounded-3 shadow w-100"
            style="max-height: 400px; object-fit: cover">
        </div>

        <!-- Info -->
        <div class="col-md-7">
          <h1 class="fw-bold">{{ bird.commonNameEn }}</h1>
          <p class="text-muted fst-italic fs-5">{{ bird.scientificName }}</p>
          <p *ngIf="bird.commonNameNl" class="text-secondary">🇧🇪 {{ bird.commonNameNl }}</p>

          <div class="row g-2 mb-3">
            <div class="col-6">
              <div class="border rounded p-2 small">
                <span class="text-muted">Orde</span><br>
                <strong>{{ bird.order }}</strong>
              </div>
            </div>
            <div class="col-6">
              <div class="border rounded p-2 small">
                <span class="text-muted">Familie</span><br>
                <strong>{{ bird.family }}</strong>
              </div>
            </div>
            <div class="col-6">
              <div class="border rounded p-2 small">
                <span class="text-muted">Genus</span><br>
                <strong>{{ bird.genus }}</strong>
              </div>
            </div>
            <div class="col-6">
              <div class="border rounded p-2 small">
                <span class="text-muted">Habitat</span><br>
                <strong>{{ bird.habitatType ?? '—' }}</strong>
              </div>
            </div>
          </div>

          <!-- Kenmerken -->
          <h5 class="fw-semibold mt-3">Kenmerken</h5>
          <div *ngIf="bird.characteristics.length > 0" class="d-flex flex-wrap gap-2">
            <span *ngFor="let c of bird.characteristics" class="badge bg-light text-dark border">
              {{ formatKey(c.key) }}: <strong>{{ c.value }}</strong>
            </span>
          </div>
          <p *ngIf="bird.characteristics.length === 0" class="text-muted small">Geen kenmerken beschikbaar.</p>

          <!-- Continenten -->
          <h5 class="fw-semibold mt-3">Verspreiding</h5>
          <div class="d-flex flex-wrap gap-2">
            <span *ngFor="let c of bird.continents" class="badge bg-success">
              {{ continentLabel(c) }}
            </span>
          </div>

          <!-- Status -->
          <div class="mt-3">
            <span class="badge fs-6" [ngClass]="statusBadge(bird.conservationStatus)">
              {{ bird.conservationStatus ?? 'NE' }} — {{ statusLabel(bird.conservationStatus) }}
            </span>
          </div>

          <!-- Wikipedia link -->
          <a *ngIf="bird" [href]="'https://en.wikipedia.org/wiki/' + bird.scientificName.replace(' ', '_')"
             target="_blank" class="btn btn-outline-dark btn-sm mt-3">
            📖 Wikipedia
          </a>
        </div>
      </div>
    </div>

    <div *ngIf="loading" class="text-center py-5">
      <div class="spinner-border text-success"></div>
    </div>
  `
})
export class BirdDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private birdService = inject(BirdService);

  bird: BirdDetail | null = null;
  loading = true;

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.birdService.getBird(id).subscribe({
      next: b => { this.bird = b; this.loading = false; },
      error: () => this.loading = false
    });
  }

  formatKey(key: string): string {
    const map: Record<string, string> = {
      beak_color: 'Snavel', breast_color: 'Borst', back_color: 'Rug',
      wing_color: 'Vleugel', size_category: 'Grootte', leg_color: 'Poten'
    };
    return map[key] ?? key.replace(/_/g, ' ');
  }

  continentLabel(code: string): string {
    const map: Record<string, string> = {
      EUROPE: '🇪🇺 Europa', AFRICA: '🌍 Afrika', ASIA: '🌏 Azië',
      NORTH_AMERICA: '🌎 Noord-Amerika', SOUTH_AMERICA: '🌎 Zuid-Amerika', OCEANIA: '🌊 Oceanië'
    };
    return map[code] ?? code;
  }

  statusBadge(status: string | null): string {
    const map: Record<string, string> = {
      LC: 'bg-success', NT: 'bg-info text-dark',
      VU: 'bg-warning text-dark', EN: 'bg-danger', CR: 'bg-dark'
    };
    return map[status ?? ''] ?? 'bg-secondary';
  }

  statusLabel(status: string | null): string {
    const map: Record<string, string> = {
      LC: 'Niet bedreigd', NT: 'Bijna bedreigd',
      VU: 'Kwetsbaar', EN: 'Bedreigd', CR: 'Kritiek'
    };
    return map[status ?? ''] ?? 'Niet geëvalueerd';
  }
}
