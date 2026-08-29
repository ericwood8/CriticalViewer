import { useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiClient, ApiError } from '../api/client';

const currentYear = new Date().getFullYear();

// Any logged-in user can add a movie (same trust level as leaving a
// review - there's no admin role in this app). Route-level gating on the
// token happens in the render below rather than hiding the nav link only,
// since a direct /movies/new visit should behave the same way.
export function AddMoviePage() {
  const navigate = useNavigate();
  const token = localStorage.getItem('cv_token');

  const [title, setTitle] = useState('');
  const [genre, setGenre] = useState('');
  const [director, setDirector] = useState('');
  const [releaseYear, setReleaseYear] = useState(String(currentYear));
  const [posterUrl, setPosterUrl] = useState('');
  const [tagline, setTagline] = useState('');
  const [summary, setSummary] = useState('');

  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  if (!token) {
    return (
      <div className="rail">
        <p>Log in to add a movie.</p>
      </div>
    );
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const created = await apiClient.createMovie({
        title,
        genre,
        director,
        releaseYear: Number(releaseYear),
        posterUrl: posterUrl || undefined,
        tagline: tagline || undefined,
        summary,
      });
      navigate(`/movies/${created.id}`);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not add this movie. Please try again.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="rail">
      <h1>Add a Movie</h1>
      <form onSubmit={handleSubmit}>
        <label htmlFor="movie-title">Title</label>
        <input
          id="movie-title"
          type="text"
          aria-label="Title"
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          required
        />

        <label htmlFor="movie-genre">Genre</label>
        <input
          id="movie-genre"
          type="text"
          aria-label="Genre"
          value={genre}
          onChange={(event) => setGenre(event.target.value)}
          required
        />

        <label htmlFor="movie-director">Director</label>
        <input
          id="movie-director"
          type="text"
          aria-label="Director"
          value={director}
          onChange={(event) => setDirector(event.target.value)}
          required
        />

        <label htmlFor="movie-release-year">Release Year</label>
        <input
          id="movie-release-year"
          type="number"
          aria-label="Release year"
          value={releaseYear}
          onChange={(event) => setReleaseYear(event.target.value)}
          required
        />

        <label htmlFor="movie-poster-url">Poster URL (optional)</label>
        <input
          id="movie-poster-url"
          type="text"
          aria-label="Poster URL"
          value={posterUrl}
          onChange={(event) => setPosterUrl(event.target.value)}
        />

        <label htmlFor="movie-tagline">Tagline (optional)</label>
        <input
          id="movie-tagline"
          type="text"
          aria-label="Tagline"
          value={tagline}
          onChange={(event) => setTagline(event.target.value)}
        />

        <label htmlFor="movie-summary">Summary</label>
        <textarea
          id="movie-summary"
          aria-label="Summary"
          value={summary}
          onChange={(event) => setSummary(event.target.value)}
          required
        />

        {error && <p>{error}</p>}

        <button type="submit" disabled={submitting}>
          Add Movie
        </button>
      </form>
    </div>
  );
}
