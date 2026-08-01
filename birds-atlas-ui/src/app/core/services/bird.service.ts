import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { BirdDetail, BirdSearchRequest, BirdSummary, FilterMeta, PagedResult } from '../models/bird.model';

@Injectable({ providedIn: 'root' })
export class BirdService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/birds`;

  search(req: BirdSearchRequest): Observable<PagedResult<BirdSummary>> {
    let params = new HttpParams();
    Object.entries(req).forEach(([k, v]) => {
      if (v !== undefined && v !== null && v !== '') params = params.set(k, String(v));
    });
    return this.http.get<PagedResult<BirdSummary>>(this.base, { params });
  }

  getDetail(gbifKey: number): Observable<BirdDetail> {
    return this.http.get<BirdDetail>(`${this.base}/${gbifKey}`);
  }

  getFilters(): Observable<FilterMeta> {
    return this.http.get<FilterMeta>(`${this.base}/filters`);
  }
}
