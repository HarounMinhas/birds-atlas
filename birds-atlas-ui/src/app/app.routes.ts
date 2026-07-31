import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'birds', pathMatch: 'full' },
  {
    path: 'birds',
    loadComponent: () => import('./features/bird-list/bird-list.component').then(m => m.BirdListComponent)
  },
  {
    path: 'birds/:id',
    loadComponent: () => import('./features/bird-detail/bird-detail.component').then(m => m.BirdDetailComponent)
  }
];
