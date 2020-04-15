DROP SCHEMA berbot CASCADE;
CREATE SCHEMA berbot;
SET search_path TO berbot;

CREATE TABLE identity (
    id            SERIAL NOT NULL PRIMARY KEY,
	
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE poll (
    poll_id       UUID NOT NULL PRIMARY KEY,
	
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE vote (
    vote_id       SERIAL NOT NULL PRIMARY KEY,
    user_id       INTEGER NOT NULL,
    decision_id   INTEGER NOT NULL,
	
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE redditor (
    username      TEXT NOT NULL PRIMARY KEY,
    app_user_id   INTEGER NOT NULL UNIQUE,
	
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE kvstore (
	type          TEXT NOT NULL,
	key           TEXT NOT NULL,
	value         TEXT NOT NULL,
	
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
	
    PRIMARY KEY (type, key)
);

INSERT INTO kvstore (type, key, value) VALUES ('asdf', 'JKL', 'SEMI');

CREATE TABLE audit_log (
    id            SERIAL NOT NULL PRIMARY KEY,
	type          TEXT NOT NULL,
	subject       TEXT NOT NULL,
	data          TEXT NOT NULL,
	
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE vote ADD CONSTRAINT fk_vote_user_id_to_identity_id FOREIGN KEY (user_id) REFERENCES identity (id);
ALTER TABLE redditor ADD CONSTRAINT fk_redditor_app_user_id_to_identity_id FOREIGN KEY (app_user_id) REFERENCES identity (id);

CREATE OR REPLACE FUNCTION trigger_set_updated_at_timestamp()
RETURNS TRIGGER AS $$
BEGIN
  NEW.updated_at = NOW();
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER tr_identity_update BEFORE UPDATE ON identity FOR EACH ROW EXECUTE PROCEDURE trigger_set_updated_at_timestamp();
CREATE TRIGGER tr_poll_update BEFORE UPDATE ON poll FOR EACH ROW EXECUTE PROCEDURE trigger_set_updated_at_timestamp();
CREATE TRIGGER tr_vote_update BEFORE UPDATE ON vote FOR EACH ROW EXECUTE PROCEDURE trigger_set_updated_at_timestamp();
CREATE TRIGGER tr_redditor_update BEFORE UPDATE ON redditor FOR EACH ROW EXECUTE PROCEDURE trigger_set_updated_at_timestamp();
CREATE TRIGGER tr_kvstore_update BEFORE UPDATE ON kvstore FOR EACH ROW EXECUTE PROCEDURE trigger_set_updated_at_timestamp();
CREATE TRIGGER tr_audit_log_update BEFORE UPDATE ON audit_log FOR EACH ROW EXECUTE PROCEDURE trigger_set_updated_at_timestamp();