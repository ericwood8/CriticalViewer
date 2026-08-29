import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { StarRating } from '../StarRating';

describe('StarRating', () => {
  it('renders the correct number of filled stars', () => {
    render(<StarRating rating={3} />);
    expect(screen.getByLabelText('3 out of 5 stars')).toBeInTheDocument();
  });

  it('clamps ratings above 5', () => {
    render(<StarRating rating={9} />);
    expect(screen.getByLabelText('5 out of 5 stars')).toBeInTheDocument();
  });
});
