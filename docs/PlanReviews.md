# Plan: AI-Assisted Review Generation

Planning document only — nothing described here has been built yet. Captures the feature shape and the reasoning behind each decision, reached through discussion before any code changes.

## Goal

Extend Critical Viewer so movie reviews can be authored by AI (via AWS Bedrock) and then edited by a human, instead of only being hand-written. Scope is **review generation only** — the AI never invents new movies. A year with a sparse or empty catalog just yields fewer or no tiles; expanding the movie catalog itself is a separate, explicitly out-of-scope decision (it would reopen the same "no real poster art" problem already hit once with the 2026 movies).

## UI flow — two separate screens

Originally considered one combined screen (a grid mixing "not yet reviewed" and "already reviewed" movies in the same view), but rejected that in favor of splitting into two purpose-built screens:

- A single mixed screen means every tile needs three visual states (not-yet-reviewed / generating / ready-to-edit) baked into one grid — more UI state to build and more ways for it to look wrong mid-batch.
- Splitting sidesteps that entirely, and "Edit Reviews" turns out to reuse the **existing Movie List/Search screen** almost as-is (same year filter, same poster grid) rather than needing new UI from scratch.

### Create Reviews

- Entry point: a "Create Reviews" button/menu item on the landing screen.
- A variant of the core browse screen with filters stripped down to just **Year** — a static hardcoded dropdown covering the current decade (e.g. 2020–2029). Hardcoded rather than computed is an accepted simplification for now; it'll need a one-line update again in 2030.
- "Create" button in place of "Search."
- On press: finds movies in that year that don't yet have a review from the "AI Reviewer" account (see Attribution below) and kicks off generation for each.
- Nothing on this screen needs to be clickable/editable — it's purely a one-way trigger + progress view (e.g. "12 of 15 generated…"), not a poster grid people interact with.
- Runs as an async **kick-off, then poll** job rather than one long synchronous request. A full year's worth of missing reviews could mean many sequential Bedrock calls, and this project already hit a real Lightsail proxy timeout (`[deployment:N] Took too long` → 502) from a slow-starting container earlier — the same failure mode would hit a big synchronous batch of LLM calls behind the nginx proxy.
- Batch concurrency capped at **3 simultaneous Bedrock calls** — guards against a rate-limit or cost surprise from one click.
- Job state is tracked **in-memory** in the ASP.NET process for this first pass (acceptable since Lightsail runs a single `nano` instance, no horizontal scaling to worry about). Not persisted to the database. A poll against a job that no longer exists (e.g. after a redeploy mid-batch) needs a defined "not found" response rather than hanging — implementation detail to handle when built, not a blocker to the plan.

### Edit Reviews

- Shows the year-filtered poster grid — same mechanics as Movie List/Search — but clicking a poster opens the **fully editable** review (rating + body) instead of navigating to the public Movie Detail View.
- Grid shows every movie for the selected year that already has an AI-authored review, so a fully-covered year stays reachable for editing rather than disappearing behind an empty state.
- **Save** and **Cancel** buttons; Save updates the existing review row in place — no duplicate created. This lines up with the schema's existing `UNIQUE (MovieId, ReviewerId)` constraint (one review per reviewer per movie).
- Adds a **Reviewer** field to the review-detail screen (currently missing from it), as an editable dropdown populated from registered users — lets someone reassign a review's byline away from "AI Reviewer" to a real registered user after editing the content. No security/auth gating restricts who can do this in this first pass; that dropdown can hand a review to *any* registered user's identity, which is an accepted simplification for now and worth revisiting before this goes further than a prototype.

## AI generation

- **Service: Amazon Bedrock**, model **Amazon Nova Micro** (`us.amazon.nova-micro-v1:0`, the inference profile id: in us-east-2 the plain `amazon.nova-micro-v1:0` supports only inference profiles). Chosen over Amazon SageMaker (real hourly hosting cost even when idle — a mismatch for a `nano`-tier app) and Amazon Comprehend (that's NLP analysis — sentiment, entities — not text generation, so it can't write a review at all).
- Region: **`us-east-2`**, same as the existing Lightsail deployment. Confirmed via `aws bedrock list-foundation-models` that Nova Micro is offered there — no need to call cross-region.
- Bedrock generates **both** the star rating and the review body text — "fully AI authored," not just body text with a placeholder rating.
- Prompt inputs are fields the `Movie` entity already has: title, genre, director, tagline, summary. No new data needed to drive generation.

## Attribution / identity

- **ASP.NET Core Identity** (`AspNetUsers`) is the real, live user store — not the demo `Reviewers` table in `CriticalViewerDB.sql`, which that file's own comments confirm is disconnected reference/seed data, never wired to the app's actual FK relationships.
- `Review.UserId` (mapped to the `ReviewerId` column) stores a real `AspNetUsers.Id`. Seed one new row directly into `AspNetUsers` for a system account named **"AI Reviewer"**; every Bedrock-generated review is attributed to that account's Id.
- No external call is needed to resolve an identity or display name anywhere in this feature — "AWS Identity service" doesn't exist and isn't involved; ASP.NET Core Identity is a local library managing a table in this same MySQL database. Displaying a reviewer's name is a plain local join (`Review.User.UserName`), same as every other review already does today.
- Because there's no login-gating on this feature yet, "AI Reviewer" is effectively the only reviewer identity this feature ever writes — which simplifies "does this movie already have an AI review for this year" to a straightforward existence check on `(MovieId, ReviewerId = AI Reviewer's fixed Id)`, rather than needing to reason about arbitrary human reviewers.

## AWS prerequisites — user-owned, outside of code

Neither of these exists yet; both are account-level setup, not something to build in the app itself.

1. **Request Bedrock model access** for Amazon Nova Micro in `us-east-2`, via AWS Console → Bedrock → Model access → enable Nova Micro. Confirmed live via a test `invoke-model` call that this is **not yet granted** on this account: `Error 002: Access to Bedrock models is not allowed for this account`. Amazon's own Nova models typically grant instantly once requested (no review/waiting period, unlike some third-party models).
2. **Runtime AWS credentials for the backend container.** Lightsail Container Service has no IAM-role-for-containers mechanism (no EC2 instance profile or ECS task-role equivalent) — a running container can't just "be" an authorized AWS identity the way it could elsewhere. The only path is an explicit IAM user with a narrowly-scoped policy (`bedrock:InvokeModel` only, least privilege), whose access key gets added to `containers.json`'s `backend-api.environment` block — same gitignored-secrets pattern already used for the DB password and JWT signing key.

Both are being handled by the user directly rather than run via CLI mid-planning, since creating IAM users/policies is account-security-relevant and worth a deliberate action rather than something to do automatically. Once both are done, the same `invoke-model` test call can re-confirm access before anything is built against it.

## Implementation notes for when this gets built (not yet done)

- Wrap the Bedrock call behind an interface, mirroring the existing `IMovieCountProvider` testability pattern, so xUnit tests fake the AI call rather than hitting live Bedrock.
- New API surface needed that doesn't exist today: an update-review endpoint (currently only create + list exist), plus the "Create Reviews" batch kick-off/poll endpoints.
- New seed row for "AI Reviewer" in `AspNetUsers`.

## Explicitly out of scope for this feature

- **No AI-invented movies.** Review generation only, against the existing catalog — avoids reopening the placeholder-poster problem for any newly-invented titles.
- **No security/auth gating** on the Create Reviews / Edit Reviews screens for this first pass — an accepted simplification, flagged to revisit before this goes further than a prototype.
- **No persisted job-tracking table** for batch generation status — in-memory only for this first pass, acceptable given Lightsail runs a single `nano` instance.
