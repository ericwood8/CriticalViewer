export interface Movie {
  id: string;
  title: string;
  genre: string;
  director: string;
  releaseYear: number;
  posterUrl?: string;
  tagline?: string;
  summary: string;
}

export interface Review {
  id: string;
  username: string;
  rating: number;
  body: string;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface PageCountResponse {
  totalMovies: number;
  pageSize: number;
  totalPages: number;
}

export interface CreateReviewRequest {
  rating: number;
  body: string;
}

export interface CreateMovieRequest {
  title: string;
  genre: string;
  director: string;
  releaseYear: number;
  posterUrl?: string;
  tagline?: string;
  summary: string;
}
