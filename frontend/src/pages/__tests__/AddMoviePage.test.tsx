import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useParams } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AddMoviePage } from '../AddMoviePage';
import { apiClient, ApiError } from '../../api/client';
import type { Movie } from '../../api/types';

function DummyMoviePage() {
  const { id } = useParams<{ id: string }>();
  return <div>Movie page for id {id}</div>;
}

function renderAddMoviePage() {
  return render(
    <MemoryRouter initialEntries={['/movies/new']}>
      <Routes>
        <Route path="/movies/new" element={<AddMoviePage />} />
        <Route path="/movies/:id" element={<DummyMoviePage />} />
      </Routes>
    </MemoryRouter>,
  );
}

const created: Movie = {
  id: 'new-movie-id',
  title: 'Brand New Movie',
  genre: 'Drama',
  director: 'Some Director',
  releaseYear: 2027,
  summary: 'A summary.',
};

describe('AddMoviePage', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('shows a login prompt instead of the form when no token is present', () => {
    renderAddMoviePage();

    expect(screen.getByText('Log in to add a movie.')).toBeInTheDocument();
    expect(screen.queryByLabelText('Title')).not.toBeInTheDocument();
  });

  it('shows the form when a token is present', () => {
    localStorage.setItem('cv_token', 'test-token');
    renderAddMoviePage();

    expect(screen.getByLabelText('Title')).toBeInTheDocument();
    expect(screen.queryByText('Log in to add a movie.')).not.toBeInTheDocument();
  });

  it('submits the form to createMovie and navigates to the new movie on success', async () => {
    localStorage.setItem('cv_token', 'test-token');
    vi.spyOn(apiClient, 'createMovie').mockResolvedValue(created);

    renderAddMoviePage();

    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Brand New Movie' } });
    fireEvent.change(screen.getByLabelText('Genre'), { target: { value: 'Drama' } });
    fireEvent.change(screen.getByLabelText('Director'), { target: { value: 'Some Director' } });
    fireEvent.change(screen.getByLabelText('Release year'), { target: { value: '2027' } });
    fireEvent.change(screen.getByLabelText('Summary'), { target: { value: 'A summary.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add Movie' }));

    await waitFor(() =>
      expect(apiClient.createMovie).toHaveBeenCalledWith({
        title: 'Brand New Movie',
        genre: 'Drama',
        director: 'Some Director',
        releaseYear: 2027,
        posterUrl: undefined,
        tagline: undefined,
        summary: 'A summary.',
      }),
    );
    expect(await screen.findByText('Movie page for id new-movie-id')).toBeInTheDocument();
  });

  it('shows an inline error message when creation fails', async () => {
    localStorage.setItem('cv_token', 'test-token');
    vi.spyOn(apiClient, 'createMovie').mockRejectedValue(new ApiError(400, 'title is required.'));

    renderAddMoviePage();

    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'x' } });
    fireEvent.change(screen.getByLabelText('Genre'), { target: { value: 'x' } });
    fireEvent.change(screen.getByLabelText('Director'), { target: { value: 'x' } });
    fireEvent.change(screen.getByLabelText('Summary'), { target: { value: 'x' } });
    fireEvent.click(screen.getByRole('button', { name: 'Add Movie' }));

    expect(await screen.findByText('title is required.')).toBeInTheDocument();
  });
});
