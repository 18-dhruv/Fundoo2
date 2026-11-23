-- Schema for the Notes service database (Fundoo.Notes.Infrastructure uses raw Npgsql, no EF migrations).
-- Column names match NotesRepository / LabelsRepository. Safe to run repeatedly.

CREATE TABLE IF NOT EXISTS notes (
    id              uuid PRIMARY KEY,
    user_id         uuid        NOT NULL,          -- user lives in the User DB, so no FK
    title           text        NOT NULL,
    content         text,
    color           varchar(20),
    is_pinned       boolean     NOT NULL DEFAULT false,
    is_archived     boolean     NOT NULL DEFAULT false,
    is_trashed      boolean     NOT NULL DEFAULT false,
    created_at_utc  timestamptz NOT NULL DEFAULT now(),
    updated_at_utc  timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_notes_user_state ON notes (user_id, is_archived, is_trashed);

CREATE TABLE IF NOT EXISTS labels (
    id       uuid PRIMARY KEY,
    user_id  uuid NOT NULL,
    name     text NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_labels_user ON labels (user_id);

CREATE TABLE IF NOT EXISTS note_labels (
    note_id   uuid NOT NULL REFERENCES notes(id)  ON DELETE CASCADE,
    label_id  uuid NOT NULL REFERENCES labels(id) ON DELETE CASCADE,
    PRIMARY KEY (note_id, label_id)
);
