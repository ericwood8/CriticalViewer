interface StarRatingProps {
  rating: number; // 1-5
}

export function StarRating({ rating }: StarRatingProps) {
  const clamped = Math.max(0, Math.min(5, Math.round(rating)));
  return (
    <span className="star-rating" aria-label={`${clamped} out of 5 stars`}>
      {'★'.repeat(clamped)}{'☆'.repeat(5 - clamped)}
    </span>
  );
}
