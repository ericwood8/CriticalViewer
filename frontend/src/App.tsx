import { BrowserRouter, Routes, Route, useLocation } from 'react-router-dom';
import { Header } from './components/Header';
import { Footer } from './components/Footer';
import { HomePage } from './pages/HomePage';
import { MovieDetailPage } from './pages/MovieDetailPage';
import { AddMoviePage } from './pages/AddMoviePage';

function AppRoutes() {
  // location.key changes on every navigation, even one that lands back on
  // the same path (e.g. clicking "Browse" while already on "/") - keying
  // HomePage on it forces a remount so its search resets instead of
  // silently no-opping, which is what a plain <Route> does by default
  // since React Router doesn't remount an element for a same-path nav.
  const location = useLocation();

  return (
    <Routes>
      <Route path="/" element={<HomePage key={location.key} />} />
      <Route path="/movies/new" element={<AddMoviePage />} />
      <Route path="/movies/:id" element={<MovieDetailPage />} />
    </Routes>
  );
}

export function App() {
  return (
    <BrowserRouter>
      <div className="app-shell">
        <Header />
        <main className="app-main">
          <AppRoutes />
        </main>
        <Footer />
      </div>
    </BrowserRouter>
  );
}
