import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { HomePage } from '../HomePage';
import { apiClient } from '../../api/client';
import type { Movie, PagedResult } from '../../api/types';

function renderHomePage() {
  return render(
    <MemoryRouter>
      <HomePage />
    </MemoryRouter>,
  );
}

function pagedResult(items: Movie[], overrides: Partial<PagedResult<Movie>> = {}): PagedResult<Movie> {
  return {
    items,
    page: 1,
    pageSize: 100,
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
  summary: 'A story of escape.',
};

describe('HomePage', () => {
  beforeEach(() => {
    vi.spyOn(apiClient, 'searchMovies').mockResolvedValue(pagedResult([movie]));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('renders search results from a mocked API response', async () => {
    renderHomePage();

    expect(await screen.findByText('The Great Escape')).toBeInTheDocument();
    expect(apiClient.searchMovies).toHaveBeenCalledTimes(1);
  });

  it('shows an empty-state message when there are no results', async () => {
    vi.spyOn(apiClient, 'searchMovies').mockResolvedValue(pagedResult([]));
    renderHomePage();

    expect(await screen.findByText('No movies match your search.')).toBeInTheDocument();
  });

  it('submits the search form with the entered filter values', async () => {
    renderHomePage();
    await waitFor(() => expect(apiClient.searchMovies).toHaveBeenCalledTimes(1));

    fireEvent.change(screen.getByLabelText('Search by title'), { target: { value: 'Escape' } });
    fireEvent.change(screen.getByLabelText('Genre'), { target: { value: 'Drama' } });
    fireEvent.change(screen.getByLabelText('Director'), { target: { value: 'Jane Doe' } });

    fireEvent.click(screen.getByRole('button', { name: 'Search' }));

    await waitFor(() => expect(apiClient.searchMovies).toHaveBeenCalledTimes(2));
    expect(apiClient.searchMovies).toHaveBeenLastCalledWith(
      expect.objectContaining({
        title: 'Escape',
        genre: 'Drama',
        director: 'Jane Doe',
        page: 1,
      }),
    );
  });

  it('calls the next and previous page on pagination button clicks', async () => {
    vi.spyOn(apiClient, 'searchMovies').mockResolvedValue(
      pagedResult([movie], { page: 2, totalPages: 3 }),
    );
    renderHomePage();

    await screen.findByText('Page 2 of 3');
    expect(apiClient.searchMovies).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole('button', { name: 'Next' }));
    await waitFor(() => expect(apiClient.searchMovies).toHaveBeenCalledTimes(2));
    expect(apiClient.searchMovies).toHaveBeenLastCalledWith(expect.objectContaining({ page: 3 }));

    fireEvent.click(screen.getByRole('button', { name: 'Previous' }));
    await waitFor(() => expect(apiClient.searchMovies).toHaveBeenCalledTimes(3));
    expect(apiClient.searchMovies).toHaveBeenLastCalledWith(expect.objectContaining({ page: 1 }));
  });

  it('disables the previous and next buttons when there is only one page', async () => {
    renderHomePage();
    await screen.findByText('Page 1 of 1');

    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled();
  });
});
