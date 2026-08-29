import { Link } from 'react-router-dom';

export function Header() {
  // Read fresh on every render rather than in state - there's no app-wide
  // auth context yet, and login/logout don't happen from this component,
  // so a plain read is enough to keep the link in sync with reality.
  const token = localStorage.getItem('cv_token');

  return (
    <header className="header">
      <div className="rail header-content">
        <Link to="/" className="brand">Critical Viewer</Link>
        <nav className="nav-menu menu">
          <Link to="/">Browse</Link>
          {token && <Link to="/movies/new">Add Movie</Link>}
        </nav>
      </div>
    </header>
  );
}
