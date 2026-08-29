import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { MovieCard } from '../components/MovieCard';
import { apiClient } from '../api/client';
import type { Movie } from '../api/types';

const currentYear = new Date().getFullYear();

export function HomePage() {
  const [title, setTitle] = useState('');
  const [genre, setGenre] = useState('');
  const [director, setDirector] = useState('');
  const [year, setYear] = useState(String(currentYear));

  const [movies, setMovies] = useState<Movie[]>([]);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const runSearch = async (targetPage: number) => {
    setLoading(true);
    setError(null);
    try {
      const result = await apiClient.searchMovies({
        title,
        genre,
        director,
        year,
        page: targetPage,
      });
      setMovies(result.items);
      setPage(result.page);
      setTotalPages(result.totalPages);
    } catch {
      setError('Failed to load movies.');
      setMovies([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void runSearch(1);
    // Intentionally run once on mount with the default filter values;
    // subsequent searches are triggered by form submit / pagination.
  }, []);

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    void runSearch(1);
  };

  return (
    <div className="rail">
      <form className="search-bar" onSubmit={handleSubmit}>
        <input
          type="text"
          placeholder="Search by title"
          aria-label="Search by title"
          value={title}
          onChange={(event) => setTitle(event.target.value)}
        />
        <input
          type="text"
          placeholder="Genre"
          aria-label="Genre"
          value={genre}
          onChange={(event) => setGenre(event.target.value)}
        />
        <input
          type="text"
          placeholder="Director"
          aria-label="Director"
          value={director}
          onChange={(event) => setDirector(event.target.value)}
        />
        <input
          type="number"
          placeholder="Year"
          aria-label="Release year"
          value={year}
          onChange={(event) => setYear(event.target.value)}
        />
        <button type="submit">Search</button>
      </form>

      {error && <p>{error}</p>}

      {!loading && movies.length === 0 ? (
        <p>No movies match your search.</p>
      ) : (
        <div className="movie-grid">
          {movies.map((movie) => (
            <MovieCard key={movie.id} movie={movie} />
          ))}
        </div>
      )}

      <div className="pagination">
        <button
          type="button"
          onClick={() => void runSearch(page - 1)}
          disabled={page <= 1 || loading}
        >
          Previous
        </button>
        <span>
          Page {page} of {totalPages}
        </span>
        <button
          type="button"
          onClick={() => void runSearch(page + 1)}
          disabled={page >= totalPages || loading}
        >
          Next
        </button>
      </div>
    </div>
  );
}
