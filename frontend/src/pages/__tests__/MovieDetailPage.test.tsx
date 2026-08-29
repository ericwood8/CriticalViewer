import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { MovieDetailPage } from '../MovieDetailPage';
import { apiClient, ApiError } from '../../api/client';
import type { Movie, PagedResult, Review } from '../../api/types';

function renderMovieDetailPage(movieId = 'm1') {
  return render(
    <MemoryRouter initialEntries={[`/movies/${movieId}`]}>
      <Routes>
        <Route path="/movies/:id" element={<MovieDetailPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

function pagedResult<T>(items: T[], overrides: Partial<PagedResult<T>> = {}): PagedResult<T> {
  return {
    items,
    page: 1,
    pageSize: 10,
    totalCount: items.length,
    totalPages: 1,
    ...overrides,
  };
}

const movie: Movie = {
  id: 'm1',
  title: 'The Great Escape',
  genre: 'Drama',
  director: 'Jane Doe',
  releaseYear: 2020,
  tagline: 'Freedom awaits.',
  summary: 'A story of escape.',
};

const review: Review = {
  id: 'r1',
  username: 'movieFan',
  rating: 4,
  body: 'Really enjoyed this one.',
  createdAt: '2026-01-01T00:00:00Z',
};

describe('MovieDetailPage', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(apiClient, 'getMovie').mockResolvedValue(movie);
    vi.spyOn(apiClient, 'getMovieReviews').mockResolvedValue(pagedResult([review]));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('renders movie details and reviews from mocked responses', async () => {
    renderMovieDetailPage();

    expect(await screen.findByText('The Great Escape')).toBeInTheDocument();
    expect(screen.getByText(/Drama/)).toBeInTheDocument();
    expect(screen.getByText(/Jane Doe/)).toBeInTheDocument();
    expect(screen.getByText(/2020/)).toBeInTheDocument();
    expect(screen.getByText('A story of escape.')).toBeInTheDocument();

    expect(await screen.findByText('movieFan')).toBeInTheDocument();
    expect(screen.getByText('Really enjoyed this one.')).toBeInTheDocument();
    expect(screen.getByLabelText('4 out of 5 stars')).toBeInTheDocument();

    expect(apiClient.getMovie).toHaveBeenCalledWith('m1');
    expect(apiClient.getMovieReviews).toHaveBeenCalledWith('m1', 1);
  });

  it('shows a "Load more reviews" button and fetches the next page', async () => {
    vi.spyOn(apiClient, 'getMovieReviews').mockResolvedValue(
      pagedResult([review], { page: 1, totalPages: 2 }),
    );
    renderMovieDetailPage();

    const loadMore = await screen.findByRole('button', { name: 'Load more reviews' });

    vi.spyOn(apiClient, 'getMovieReviews').mockResolvedValue(
      pagedResult(
        [{ ...review, id: 'r2', username: 'anotherFan' }],
        { page: 2, totalPages: 2 },
      ),
    );
    fireEvent.click(loadMore);

    expect(await screen.findByText('anotherFan')).toBeInTheDocument();
    // Original review from page 1 should still be present.
    expect(screen.getByText('movieFan')).toBeInTheDocument();
    expect(apiClient.getMovieReviews).toHaveBeenLastCalledWith('m1', 2);
  });

  it('hides the review form and shows a login prompt when no token is present', async () => {
    renderMovieDetailPage();

    await screen.findByText('The Great Escape');
    expect(screen.getByText('Log in to leave a review.')).toBeInTheDocument();
    expect(screen.queryByLabelText('Review text')).not.toBeInTheDocument();
  });

  it('shows the review form when a token is present', async () => {
    localStorage.setItem('cv_token', 'test-token');
    renderMovieDetailPage();

    await screen.findByText('The Great Escape');
    expect(screen.getByLabelText('Review text')).toBeInTheDocument();
    expect(screen.queryByText('Log in to leave a review.')).not.toBeInTheDocument();
  });

  it('submits a new review to the correct endpoint and prepends it to the list', async () => {
    localStorage.setItem('cv_token', 'test-token');
    const created: Review = { id: 'r3', username: 'me', rating: 5, body: 'Loved it!', createdAt: '2026-08-24T00:00:00Z' };
    vi.spyOn(apiClient, 'createReview').mockResolvedValue(created);

    renderMovieDetailPage();
    await screen.findByText('The Great Escape');
    await screen.findByText('movieFan');

    fireEvent.change(screen.getByLabelText('Rating'), { target: { value: '5' } });
    fireEvent.change(screen.getByLabelText('Review text'), { target: { value: 'Loved it!' } });
    fireEvent.click(screen.getByRole('button', { name: 'Submit Review' }));

    await waitFor(() =>
      expect(apiClient.createReview).toHaveBeenCalledWith('m1', { rating: 5, body: 'Loved it!' }),
    );
    expect(await screen.findByText('Loved it!')).toBeInTheDocument();
  });

  it('shows an inline message on a 409 duplicate-review response', async () => {
    localStorage.setItem('cv_token', 'test-token');
    vi.spyOn(apiClient, 'createReview').mockRejectedValue(new ApiError(409, 'already reviewed'));

    renderMovieDetailPage();
    await screen.findByText('The Great Escape');

    fireEvent.change(screen.getByLabelText('Review text'), { target: { value: 'Another try' } });
    fireEvent.click(screen.getByRole('button', { name: 'Submit Review' }));

    expect(await screen.findByText(/already reviewed this movie/)).toBeInTheDocument();
  });
});
