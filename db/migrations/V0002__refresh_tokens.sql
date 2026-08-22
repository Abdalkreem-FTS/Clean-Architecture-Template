CREATE TABLE refresh_tokens (
    id             uuid         NOT NULL,
    user_id        uuid         NOT NULL,
    token_hash     varchar(128) NOT NULL,
    created_at_utc timestamptz  NOT NULL,
    expires_at_utc timestamptz  NOT NULL,
    revoked_at_utc timestamptz  NULL,

    CONSTRAINT pk_refresh_tokens PRIMARY KEY (id),
    CONSTRAINT fk_refresh_tokens_users FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX ix_refresh_tokens_token_hash ON refresh_tokens (token_hash);

CREATE INDEX ix_refresh_tokens_user_id ON refresh_tokens (user_id);
