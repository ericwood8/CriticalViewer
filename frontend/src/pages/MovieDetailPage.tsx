import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { useParams } from 'react-router-dom';
import { apiClient, ApiError } from '../api/client';
import { StarRating } from '../components/StarRating';
import type { Movie, Review } from '../api/types';

export function MovieDetailPage() {
  const { id } = useParams<{ id: string }>();

  const [movie, setMovie] = useState<Movie | null>(null);
  const [movieError, setMovieError] = useState<string | null>(null);

  const [reviews, setReviews] = useState<Review[]>([]);
  const [reviewsPage, setReviewsPage] = useState(1);
  const [reviewsTotalPages, setReviewsTotalPages] = useState(1);
  const [reviewsLoading, setReviewsLoading] = useState(false);

  const [rating, setRating] = useState(5);
  const [body, setBody] = useState('');
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [alreadyReviewed, setAlreadyReviewed] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const token = localStorage.getItem('cv_token');

  useEffect(() => {
    if (!id) return;
    setMovie(null);
    setMovieError(null);
    apiClient
      .getMovie(id)
      .then(setMovie)
      .catch(() => setMovieError('Movie not found.'));
  }, [id]);

  const loadReviews = async (targetPage: number) => {
    if (!id) return;
    setReviewsLoading(true);
    try {
      const result = await apiClient.getMovieReviews(id, targetPage);
      setReviews((prev) => (targetPage === 1 ? result.items : [...prev, ...result.items]));
      setReviewsPage(result.page);
      setReviewsTotalPages(result.totalPages);
    } finally {
      setReviewsLoading(false);
    }
  };

  useEffect(() => {
    if (!id) return;
    void loadReviews(1);
    // Re-fetch the first page of reviews whenever the movie id changes.
  }, [id]);

  const handleReviewSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!id) return;

    setSubmitError(null);
    setAlreadyReviewed(false);
    setSubmitting(true);
    try {
      const created = await apiClient.createReview(id, { rating, body });
      setReviews((prev) => [created, ...prev]);
      setBody('');
      setRating(5);
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        setAlreadyReviewed(true);
      } else {
        setSubmitError('Could not submit your review. Please try again.');
      }
    } finally {
      setSubmitting(false);
    }
  };

  if (movieError) {
    return (
      <div className="rail">
        <p>{movieError}</p>
      </div>
    );
  }

  if (!movie) {
    return (
      <div className="rail">
        <p>Loading movie&hellip;</p>
      </div>
    );
  }

  return (
    <div className="rail">
      <div className="movie-detail">
        {movie.posterUrl && (
          <img className="movie-card-poster" src={movie.posterUrl} alt={`${movie.title} poster`} />
        )}
        <h1>{movie.title}</h1>
        {movie.tagline && <p><em>{movie.tagline}</em></p>}
        <p className="movie-card-meta">
          {movie.genre} &middot; {movie.director} &middot; {movie.releaseYear}
        </p>
        <p>{movie.summary}</p>
      </div>

      <div className="review-section">
        <h2>Reviews</h2>

        {token ? (
          <form onSubmit={handleReviewSubmit}>
            <label htmlFor="review-rating">Rating</label>
            <input
              id="review-rating"
              type="number"
              aria-label="Rating"
              min={1}
              max={5}
              value={rating}
              onChange={(event) => setRating(Number(event.target.value))}
            />

            <label htmlFor="review-body">Review</label>
            <textarea
              id="review-body"
              aria-label="Review text"
              value={body}
              onChange={(event) => setBody(event.target.value)}
              required
            />

            {alreadyReviewed && <p>You&rsquo;ve already reviewed this movie.</p>}
            {submitError && <p>{submitError}</p>}

            <button type="submit" disabled={submitting}>
              Submit Review
            </button>
          </form>
        ) : (
          <p>Log in to leave a review.</p>
        )}

        <div className="review-list">
          {reviews.map((review) => (
            <div className="review-item" key={review.id}>
              <div className="review-item-user">{review.username}</div>
              <StarRating rating={review.rating} />
              <p>{review.body}</p>
            </div>
          ))}
        </div>

        {reviews.length === 0 && !reviewsLoading && <p>No reviews yet.</p>}

        {reviewsPage < reviewsTotalPages && (
          <button type="button" onClick={() => void loadReviews(reviewsPage + 1)} disabled={reviewsLoading}>
            Load more reviews
          </button>
        )}
      </div>
    </div>
  );
}
