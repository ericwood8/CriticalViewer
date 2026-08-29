import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { apiClient, ApiError } from '../client';

function jsonResponse(body: unknown, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    statusText: 'status',
    text: () => Promise.resolve(JSON.stringify(body)),
    json: () => Promise.resolve(body),
  } as Response;
}

describe('apiClient', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('fetches the movie page count from the contract endpoint', async () => {
    const body = { totalMovies: 250, pageSize: 100, totalPages: 3 };
    vi.mocked(fetch).mockResolvedValue(jsonResponse(body));

    const result = await apiClient.getMoviePageCount();

    expect(result).toEqual(body);
    const [url, init] = vi.mocked(fetch).mock.calls[0];
    expect(String(url)).toContain('/api/movies/page-count');
    expect(init?.method ?? 'GET').toBe('GET');
  });

  it('builds a movies search query string from all filter params', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse({ items: [], page: 1, pageSize: 100, totalCount: 0, totalPages: 0 }));

    await apiClient.searchMovies({ title: 'Escape', genre: 'Drama', director: 'Jane', year: 2020, page: 2 });

    const [url] = vi.mocked(fetch).mock.calls[0];
    const parsed = new URL(String(url));
    expect(parsed.pathname).toBe('/api/movies');
    expect(parsed.searchParams.get('title')).toBe('Escape');
    expect(parsed.searchParams.get('genre')).toBe('Drama');
    expect(parsed.searchParams.get('director')).toBe('Jane');
    expect(parsed.searchParams.get('year')).toBe('2020');
    expect(parsed.searchParams.get('page')).toBe('2');
  });

  it('fetches a single movie by id', async () => {
    const movie = { id: 'm1', title: 'X', genre: 'Y', director: 'Z', releaseYear: 2020, summary: 'S' };
    vi.mocked(fetch).mockResolvedValue(jsonResponse(movie));

    const result = await apiClient.getMovie('m1');

    expect(result).toEqual(movie);
    const [url] = vi.mocked(fetch).mock.calls[0];
    expect(String(url)).toContain('/api/movies/m1');
  });

  it('throws an ApiError with the response status on a non-ok response', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse({ message: 'not found' }, 404));

    await expect(apiClient.getMovie('missing')).rejects.toBeInstanceOf(ApiError);
  });

  it('posts a new review with the bearer token attached when present', async () => {
    localStorage.setItem('cv_token', 'secret-token');
    const created = { id: 'r1', username: 'me', rating: 5, body: 'Great!', createdAt: '2026-01-01T00:00:00Z' };
    vi.mocked(fetch).mockResolvedValue(jsonResponse(created, 201));

    const result = await apiClient.createReview('m1', { rating: 5, body: 'Great!' });

    expect(result).toEqual(created);
    const [url, init] = vi.mocked(fetch).mock.calls[0];
    expect(String(url)).toContain('/api/movies/m1/reviews');
    expect(init?.method).toBe('POST');
    expect(init?.body).toBe(JSON.stringify({ rating: 5, body: 'Great!' }));
    expect((init?.headers as Record<string, string>).Authorization).toBe('Bearer secret-token');
  });

  it('fetches a paginated review list for a movie', async () => {
    const body = { items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 };
    vi.mocked(fetch).mockResolvedValue(jsonResponse(body));

    await apiClient.getMovieReviews('m1', 2);

    const [url] = vi.mocked(fetch).mock.calls[0];
    const parsed = new URL(String(url));
    expect(parsed.pathname).toBe('/api/movies/m1/reviews');
    expect(parsed.searchParams.get('page')).toBe('2');
  });
});
