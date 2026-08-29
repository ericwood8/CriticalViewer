// Thin fetch wrapper. Swapped for something heavier (axios, react-query)
// only if a real need shows up - the API surface here is small so far.
import type {
  CreateMovieRequest,
  CreateReviewRequest,
  Movie,
  PageCountResponse,
  PagedResult,
  Review,
} from './types';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = localStorage.getItem('cv_token');

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init?.headers,
    },
  });

  if (!response.ok) {
    const body = await response.text();
    throw new ApiError(response.status, body || response.statusText);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

function toQueryString<T extends object>(params: T): string {
  const qs = new URLSearchParams();
  for (const [key, value] of Object.entries(params) as [string, string | number | undefined][]) {
    if (value !== undefined) {
      qs.set(key, String(value));
    }
  }
  const serialized = qs.toString();
  return serialized ? `?${serialized}` : '';
}

export interface SearchMoviesParams {
  title?: string;
  genre?: string;
  director?: string;
  year?: string | number;
  page?: number;
}

export const apiClient = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body: unknown) =>
    request<T>(path, { method: 'POST', body: JSON.stringify(body) }),

  searchMovies: (params: SearchMoviesParams = {}) =>
    request<PagedResult<Movie>>(`/api/movies${toQueryString(params)}`),

  getMoviePageCount: () => request<PageCountResponse>('/api/movies/page-count'),

  getMovie: (id: string) => request<Movie>(`/api/movies/${id}`),

  getMovieReviews: (movieId: string, page = 1) =>
    request<PagedResult<Review>>(`/api/movies/${movieId}/reviews${toQueryString({ page })}`),

  createReview: (movieId: string, body: CreateReviewRequest) =>
    request<Review>(`/api/movies/${movieId}/reviews`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),

  createMovie: (body: CreateMovieRequest) =>
    request<Movie>('/api/movies', {
      method: 'POST',
      body: JSON.stringify(body),
    }),
};
