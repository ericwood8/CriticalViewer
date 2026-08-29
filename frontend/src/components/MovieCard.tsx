import { Link } from 'react-router-dom';
import type { Movie } from '../api/types';

export function MovieCard({ movie }: { movie: Movie }) {
  return (
    <Link to={`/movies/${movie.id}`} className="movie-card">
      <img
        className="movie-card-poster"
        src={movie.posterUrl ?? '/placeholder-poster.png'}
        alt={`${movie.title} poster`}
      />
      <div className="movie-card-body">
        <div className="movie-card-title">{movie.title}</div>
        <div className="movie-card-meta">{movie.director} &middot; {movie.releaseYear}</div>
      </div>
    </Link>
  );
}
