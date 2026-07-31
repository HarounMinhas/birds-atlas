export interface BirdListItem {
  id: number;
  commonNameEn: string;
  scientificName: string;
  order: string;
  family: string;
  conservationStatus: string | null;
  primaryImageUrl: string | null;
  continents: string[];
}

export interface BirdDetail extends BirdListItem {
  commonNameNl: string;
  genus: string;
  habitatType: string | null;
  characteristics: Characteristic[];
}

export interface Characteristic {
  key: string;
  value: string;
}

export interface BirdFilterOptions {
  continents: string[];
  orders: string[];
  families: string[];
  beakColors: string[];
  breastColors: string[];
  sizeCategories: string[];
  habitatTypes: string[];
  conservationStatuses: string[];
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface BirdFilterRequest {
  continent?: string;
  order?: string;
  family?: string;
  genus?: string;
  beakColor?: string;
  breastColor?: string;
  backColor?: string;
  sizeCategory?: string;
  habitatType?: string;
  conservationStatus?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}
