import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'birds', pathMatch: 'full' },
  { path: 'birds', loadComponent: () => import('./features/bird-list/bird-list.component').then(m => m.BirdListComponent) },
  { path: 'birds/:id', loadComponent: () => import('./features/bird-detail/bird-detail.component').then(m => m.BirdDetailComponent) },
  { path: 'map', loadComponent: () => import('./features/map/map.component').then(m => m.MapComponent) },
  { path: 'profile', loadComponent: () => import('./features/profile/profile.component').then(m => m.ProfileComponent) },
  { path: '**', redirectTo: 'birds' }
];
