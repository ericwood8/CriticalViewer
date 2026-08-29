-- ================================================================
--    Critical Viewer - Movie Review & Discovery Platform
--    Database creation, schema, indexes, and seed data (MySQL)
--
--    HOW TO RUN
--      mysql -h <server> -u <user> -p < CriticalViewerDB.sql
--      -- or paste into a MySQL client / Workbench and execute.
--
--    IDEMPOTENCY
--      Every CREATE DATABASE / CREATE TABLE is guarded with IF NOT EXISTS
--      (indexes are declared inline inside each CREATE TABLE, so they're
--      covered by the same guard - MySQL has no CREATE INDEX IF NOT
--      EXISTS). Every seed INSERT is guarded against re-inserting rows
--      that already exist by natural key (INSERT IGNORE against a UNIQUE
--      constraint for Reviewers/Reviews, an explicit NOT EXISTS check for
--      Movies, which has no single-column natural key). Running this
--      script five times in a row leaves the database in exactly the same
--      state as running it once - nothing is dropped, duplicated, or
--      overwritten.
-- ================================================================


-- ================================================================
--    1. DATABASE
-- ================================================================

CREATE DATABASE IF NOT EXISTS CriticalViewer
    CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

USE CriticalViewer;


-- ================================================================
--    2. TABLES (indexes declared inline - see IDEMPOTENCY note above)
-- ================================================================

-- Reviewers: registered reviewers. Feature: Account Creation / Password Change.
CREATE TABLE IF NOT EXISTS Reviewers
(
    ReviewerId   CHAR(36)      NOT NULL DEFAULT (UUID()),
    Email        VARCHAR(256)  NOT NULL,
    DisplayName  VARCHAR(100)  NOT NULL, -- shown next to reviews; see note below
    PasswordHash VARCHAR(256)  NOT NULL,
    CreatedAt    DATETIME      NOT NULL DEFAULT (UTC_TIMESTAMP()),
    PRIMARY KEY (ReviewerId),
    CONSTRAINT UQ_Reviewers_Email UNIQUE (Email)
);

-- Movies: the catalog. Feature: Movie List / Search, Movie Detail View.
CREATE TABLE IF NOT EXISTS Movies
(
    MovieId     CHAR(36)      NOT NULL DEFAULT (UUID()),
    Title       VARCHAR(300)  NOT NULL,
    Genre       VARCHAR(100)  NOT NULL,
    Director    VARCHAR(200)  NOT NULL,
    ReleaseYear INT           NOT NULL,
    PosterUrl   VARCHAR(500)  NULL,
    Tagline     VARCHAR(300)  NULL,
    Summary     VARCHAR(2000) NOT NULL,
    PRIMARY KEY (MovieId),
    -- Movie List / Search: filter by title, genre, director, or year,
    -- defaulted to the current year, paginated. Title/Genre/Director
    -- support prefix or exact-match filtering (e.g. "Genre = 'Drama'" or
    -- "Title LIKE 'The%'") efficiently. If the real search box needs
    -- mid-string matching (Title LIKE '%word%'), a plain B-tree index
    -- like this one won't be used by the optimizer for that pattern - a
    -- full-text index would be the right follow-up once that's confirmed.
    INDEX IX_Movies_Title (Title),
    INDEX IX_Movies_Genre (Genre),
    INDEX IX_Movies_Director (Director),
    INDEX IX_Movies_ReleaseYear (ReleaseYear)
);

-- Reviews: one review per user per movie. Feature: Movie Detail View.
CREATE TABLE IF NOT EXISTS Reviews
(
    ReviewId   CHAR(36)      NOT NULL DEFAULT (UUID()),
    MovieId    CHAR(36)      NOT NULL,
    ReviewerId CHAR(36)      NOT NULL,
    Rating     TINYINT       NOT NULL,
    Body       VARCHAR(2000) NOT NULL,
    CreatedAt  DATETIME      NOT NULL DEFAULT (UTC_TIMESTAMP()),
    PRIMARY KEY (ReviewId),
    CONSTRAINT CK_Reviews_Rating CHECK (Rating BETWEEN 1 AND 5),
    CONSTRAINT FK_Reviews_Movies FOREIGN KEY (MovieId) REFERENCES Movies (MovieId),
    -- No FK on ReviewerId -> Reviewers: the real app's Reviews rows store
    -- AspNetUsers.Id (ASP.NET Core Identity) in this column, not
    -- Reviewers.ReviewerId - those are two separate ID spaces (see
    -- CriticalViewer.Infrastructure/Data/AppDbContext.cs). Reviewers is
    -- seed/demo data only, kept for query/index testing.
    -- One review per user per movie. Remove this if the product should
    -- allow multiple reviews from the same person on the same title.
    CONSTRAINT UQ_Reviews_Movie_User UNIQUE (MovieId, ReviewerId),
    -- Movie Detail View: infinite-scroll review list for one movie,
    -- newest first, 10 at a time. MySQL/InnoDB has no equivalent to SQL
    -- Server's covering-index INCLUDE syntax; a plain composite index is
    -- the closest match (InnoDB secondary indexes implicitly carry the
    -- primary key anyway). DESC is honored as true descending order on
    -- InnoDB since MySQL 8.0.
    INDEX IX_Reviews_MovieId_CreatedAt (MovieId, CreatedAt DESC),
    -- Supports a future "my reviews" account page.
    INDEX IX_Reviews_ReviewerId (ReviewerId)
);


-- ================================================================
--    3. SEED DATA
--    A dozen rows per table. Safe to re-run - see IDEMPOTENCY note above.
-- ================================================================

-- Reviewers
INSERT IGNORE INTO Reviewers (Email, DisplayName, PasswordHash)
VALUES
    ('ava.moreno@example.com', 'AvaM', 'SEED-PLACEHOLDER-HASH-001'),
    ('jrpatel@example.com', 'jrpatel', 'SEED-PLACEHOLDER-HASH-002'),
    ('l.chen88@example.com', 'lchen88', 'SEED-PLACEHOLDER-HASH-003'),
    ('desmond.k@example.com', 'DKay', 'SEED-PLACEHOLDER-HASH-004'),
    ('sofia.reyes@example.com', 'sofiar', 'SEED-PLACEHOLDER-HASH-005'),
    ('tunde.okafor@example.com', 'TundeO', 'SEED-PLACEHOLDER-HASH-006'),
    ('maribel.santos@example.com', 'MariS', 'SEED-PLACEHOLDER-HASH-007'),
    ('noah.whitfield@example.com', 'noahw', 'SEED-PLACEHOLDER-HASH-008'),
    ('yuki.tanaka@example.com', 'yukit', 'SEED-PLACEHOLDER-HASH-009'),
    ('grace.oduya@example.com', 'GraceO', 'SEED-PLACEHOLDER-HASH-010'),
    ('felix.hart@example.com', 'felixh', 'SEED-PLACEHOLDER-HASH-011'),
    ('priya.menon@example.com', 'priyam', 'SEED-PLACEHOLDER-HASH-012');

-- Movies (no single-column natural key, so an explicit per-row NOT EXISTS
-- check is used instead of INSERT IGNORE against a unique constraint)
INSERT INTO Movies (Title, Genre, Director, ReleaseYear, PosterUrl, Tagline, Summary)
SELECT v.Title, v.Genre, v.Director, v.ReleaseYear, v.PosterUrl, v.Tagline, v.Summary
FROM (
    SELECT 'The Last Ember' AS Title, 'Drama' AS Genre, 'Mara Voss' AS Director, 2026 AS ReleaseYear, '/posters/the-last-ember.svg' AS PosterUrl, 'Some fires are worth keeping.' AS Tagline, 'A retired smokejumper returns to the mountains she once protected, confronting the wildfire that ended her career and the family she left behind.' AS Summary
    UNION ALL SELECT 'Static Bloom', 'Sci-Fi', 'Idris Kane', 2025, '/posters/static-bloom.jpg', 'Grown, not born.', 'In a coastal city where synthetic flora regulates the climate, a botanist uncovers a flaw in the system that could unravel the last livable region on Earth.'
    UNION ALL SELECT 'Midnight Ledger', 'Thriller', 'Priya Nandakumar', 2024, '/posters/midnight-ledger.jpg', 'Every debt comes due.', 'An overnight bank auditor discovers a decades-old embezzlement scheme just as the people behind it realize she''s found it.'
    UNION ALL SELECT 'Comet & Cane', 'Comedy', 'Owen Baptiste', 2026, '/posters/comet-cane.svg', 'Two strangers, one very bad map.', 'A washed-up magician and a runaway bride pair up on a cross-country road trip after both miss the same wedding for very different reasons.'
    UNION ALL SELECT 'The Quiet Orchard', 'Romance', 'Helene Marchetti', 2023, '/posters/the-quiet-orchard.jpg', 'Some things take a season to grow.', 'A city landscaper inherits her estranged grandmother''s failing orchard and slowly falls for the neighboring farmer who''s been trying to buy the land for years.'
    UNION ALL SELECT 'Hollow Frequency', 'Horror', 'Desmond Ruiz', 2025, '/posters/hollow-frequency.jpg', 'Turn it off.', 'A late-night radio host starts receiving calls from a station that went off the air thirty years ago, each one predicting a death that hasn''t happened yet.'
    UNION ALL SELECT 'Iron Meridian', 'Action', 'Nadia Okafor', 2026, '/posters/iron-meridian.svg', 'The line holds, or nothing does.', 'A disgraced border engineer is called back to defend the last functioning supply corridor during a continent-wide infrastructure collapse.'
    UNION ALL SELECT 'The Cartographer''s Daughter', 'Adventure', 'Felix Renner', 2022, '/posters/the-cartographer-s-daughter.jpg', 'The map was never the point.', 'Following her father''s death, a young cartographer sets out to complete his final, unfinished map of a mountain range that may not appear on any other chart.'
    UNION ALL SELECT 'Paper Moons', 'Animation', 'Suki Tanaka', 2026, '/posters/paper-moons.svg', 'Fold your own fate.', 'In a village where origami creatures come alive at dusk, a young folder must save her paper companions from a curious rival craftsman''s flame.'
    UNION ALL SELECT 'The Sixth Draft', 'Mystery', 'Colm Whitfield', 2021, '/posters/the-sixth-draft.jpg', 'Someone kept editing the truth.', 'A ghostwriter finds six drastically different manuscripts of the same true-crime memoir and starts to suspect her client didn''t write any of them.'
    UNION ALL SELECT 'Salt and Static', 'Documentary', 'Renata Alves', 2024, '/posters/salt-and-static.jpg', 'The coastline is talking. Are we listening?', 'A year embedded with a fishing community documents how shifting tides and dying radio infrastructure are reshaping a way of life.'
    UNION ALL SELECT 'The Sunken Atelier', 'Fantasy', 'Tobias Lindqvist', 2026, '/posters/the-sunken-atelier.svg', 'Not every door leads home.', 'A glassblower''s apprentice discovers her master''s finest work is a doorway into a realm that has been quietly vanishing for centuries.'
) AS v
WHERE NOT EXISTS (
    SELECT 1 FROM Movies m WHERE m.Title = v.Title AND m.ReleaseYear = v.ReleaseYear
);

-- Pagination test data: 250 synthetic movies under one dedicated year
-- (2099, chosen specifically so it never collides with real catalog data
-- and never shows up in a default/current-year browse) so the 100-item
-- page size can actually be exercised end to end (2 full pages + 1
-- partial). Guarded on the last row's natural key, same idempotency
-- pattern as the rest of this script - safe to re-run.
INSERT INTO Movies (Title, Genre, Director, ReleaseYear, PosterUrl, Tagline, Summary)
SELECT
    CONCAT('Pagination Test Movie ', LPAD(n, 4, '0')),
    'Test',
    CONCAT('Pagination Director ', ((n - 1) MOD 5) + 1),
    2099,
    NULL,
    'Synthetic data for pagination testing.',
    'One of a batch of synthetic movies seeded to exercise offset pagination (100 items/page) end to end - safe to ignore or delete.'
FROM (
    WITH RECURSIVE Numbers AS (
        SELECT 1 AS n
        UNION ALL
        SELECT n + 1 FROM Numbers WHERE n < 250
    )
    SELECT n FROM Numbers
) AS Numbers
WHERE NOT EXISTS (
    SELECT 1 FROM Movies WHERE Title = 'Pagination Test Movie 0250' AND ReleaseYear = 2099
);

-- Reviews (resolves MovieId/ReviewerId by the natural keys above, then
-- relies on INSERT IGNORE + UQ_Reviews_Movie_User to skip pairs that
-- already have a review)
INSERT IGNORE INTO Reviews (MovieId, ReviewerId, Rating, Body)
SELECT m.MovieId, u.ReviewerId, v.Rating, v.Body
FROM (
    SELECT 'The Last Ember' AS MovieTitle, 2026 AS MovieYear, 'ava.moreno@example.com' AS ReviewerEmail, 5 AS Rating, 'Quietly devastating. The wildfire scenes never feel like spectacle, just consequence.' AS Body
    UNION ALL SELECT 'Static Bloom', 2025, 'jrpatel@example.com', 4, 'Smart premise, slightly rushed third act, but the visuals of the bloom fields alone make it worth watching.'
    UNION ALL SELECT 'Midnight Ledger', 2024, 'l.chen88@example.com', 4, 'Tense in exactly the way a good financial thriller should be. Wish the ending gave the auditor more to do.'
    UNION ALL SELECT 'Comet & Cane', 2026, 'desmond.k@example.com', 3, 'Funny in stretches, but the two leads have better chemistry than the script gives them credit for.'
    UNION ALL SELECT 'The Quiet Orchard', 2023, 'sofia.reyes@example.com', 5, 'Slow in the best way. By the final harvest scene I didn''t want it to end.'
    UNION ALL SELECT 'Hollow Frequency', 2025, 'tunde.okafor@example.com', 4, 'Genuinely unsettling without relying on jump scares. The sound design is doing a lot of the work here.'
    UNION ALL SELECT 'Iron Meridian', 2026, 'maribel.santos@example.com', 3, 'Solid action, thin plot. Watch it for the corridor siege sequence and nothing else.'
    UNION ALL SELECT 'The Cartographer''s Daughter', 2022, 'noah.whitfield@example.com', 5, 'One of the better father-daughter stories I''ve seen in this genre. The final map reveal got me.'
    UNION ALL SELECT 'Paper Moons', 2026, 'yuki.tanaka@example.com', 5, 'Beautifully animated, and the folding sequences are inventive every single time.'
    UNION ALL SELECT 'The Sixth Draft', 2021, 'grace.oduya@example.com', 4, 'The kind of mystery where you''ll want to immediately rewatch the first act once you know the twist.'
    UNION ALL SELECT 'Salt and Static', 2024, 'felix.hart@example.com', 4, 'A patient, respectful documentary that never talks down to its subjects.'
    UNION ALL SELECT 'The Sunken Atelier', 2026, 'priya.menon@example.com', 5, 'Gorgeous world-building. The doorway concept is used more cleverly than I expected.'
) AS v
JOIN Movies m ON m.Title = v.MovieTitle AND m.ReleaseYear = v.MovieYear
JOIN Reviewers u ON u.Email = v.ReviewerEmail;


-- ================================================================
--    4. POST-RUN SANITY CHECK
-- ================================================================

SELECT COUNT(*) FROM Reviewers;
SELECT COUNT(*) FROM Movies;
SELECT COUNT(*) FROM Reviews;
