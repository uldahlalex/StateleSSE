DROP SCHEMA IF EXISTS kahoot CASCADE;
CREATE SCHEMA IF NOT EXISTS kahoot;

CREATE TABLE kahoot.users
(
    id   text not null primary key,
    name text not null
);
CREATE TABLE kahoot.userscredentials
(
    id           text not null references kahoot.users (id) primary key,
    salt         text not null,
    passwordHash text not null
);


CREATE TABLE kahoot.quizzes
(
    id        text not null primary key,
    name      text not null,
    createdBy text not null references kahoot.users (id)
);

CREATE TABLE kahoot.questions
(
    id          text not null primary key,
    description text not null,
    quizId      text not null references kahoot.quizzes (id),
    seconds     int  not null
);

CREATE TABLE kahoot.options
(
    id          text not null primary key,
    description text not null,
    isCorrect   bool not null,
    questionId  text not null references kahoot.questions (id)
);



CREATE TABLE kahoot.game
(
    id     text not null primary key,
    hostId text not null references kahoot.users (id),
    quizId text not null references kahoot.quizzes (id)
);
create table kahoot.gameround
(
    id         text                     not null primary key,
    questionId text                     not null references kahoot.questions (id),
    gameId     text                     not null references kahoot.game (id),
    startedAt  timestamp with time zone not null,
    endedAt    timestamp with time zone
);

CREATE TABLE kahoot.answers
(
    userId     text not null references kahoot.users (id),
    gameRound  text not null references kahoot.gameround (id),
    option     text not null references kahoot.options (id),
    primary key (gameRound, userId),
    answeredAt timestamp with time zone
);

CREATE TABLE kahoot.gamemember
(
    userid   text references kahoot.users (id) on delete cascade,
    gameid   text references kahoot.game (id) on delete cascade,
    joinedAt timestamp with time zone not null,
    primary key (userid, gameid)
);

CREATE TABLE kahoot.activeconnections
(
    connectionid text                     not null primary key,
    userid       text                     not null references kahoot.users (id) on delete cascade,
    gameid       text                     not null references kahoot.game (id) on delete cascade,
    connectedat  timestamp with time zone not null,
    lastseenaat  timestamp with time zone not null
);

CREATE INDEX activeconnections_gameid_idx ON kahoot.activeconnections (gameid);
CREATE INDEX activeconnections_lastseenaat_idx ON kahoot.activeconnections (lastseenaat);

-- =============================================================================
-- WEATHER MONITORING TABLES
-- =============================================================================

CREATE TABLE kahoot.weatherstations
(
    id   text not null primary key,
    name text not null
);

CREATE TABLE kahoot.weatherreadings
(
    id          text                     not null primary key,
    stationid   text                     not null references kahoot.weatherstations (id),
    temperature numeric(5, 2)            not null,
    humidity    numeric(5, 2)            not null,
    pressure    numeric(6, 2)            not null,
    timestamp   timestamp with time zone not null
);

CREATE INDEX weatherreadings_stationid_idx ON kahoot.weatherreadings (stationid);
CREATE INDEX weatherreadings_timestamp_idx ON kahoot.weatherreadings (timestamp);

-- =============================================================================
-- REALTIME NOTIFICATIONS via LISTEN/NOTIFY
-- =============================================================================

-- Function that sends notification when gamemember changes
CREATE OR REPLACE FUNCTION kahoot.notify_gamemember_changed()
RETURNS trigger AS $$
DECLARE
    payload json;
    change_type int;
BEGIN
    -- Map TG_OP to ChangeType enum (Insert=0, Update=1, Delete=2)
    IF TG_OP = 'DELETE' THEN
        change_type := 2;
        payload := json_build_object(
            'Type', change_type,
            'Data', row_to_json(OLD),
            'Timestamp', now()
        );
    ELSE
        change_type := CASE TG_OP WHEN 'INSERT' THEN 0 ELSE 1 END;
        payload := json_build_object(
            'Type', change_type,
            'Data', row_to_json(NEW),
            'Timestamp', now()
        );
    END IF;

    PERFORM pg_notify('gamemember_changed', payload::text);
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

-- Attach trigger to gamemember table
CREATE TRIGGER gamemember_changed_trigger
AFTER INSERT OR UPDATE OR DELETE ON kahoot.gamemember
FOR EACH ROW EXECUTE FUNCTION kahoot.notify_gamemember_changed();

-- Function that sends notification when game changes
CREATE OR REPLACE FUNCTION kahoot.notify_game_changed()
RETURNS trigger AS $$
DECLARE
    payload json;
    change_type int;
BEGIN
    IF TG_OP = 'DELETE' THEN
        change_type := 2;
        payload := json_build_object(
            'Type', change_type,
            'Data', row_to_json(OLD),
            'Timestamp', now()
        );
    ELSE
        change_type := CASE TG_OP WHEN 'INSERT' THEN 0 ELSE 1 END;
        payload := json_build_object(
            'Type', change_type,
            'Data', row_to_json(NEW),
            'Timestamp', now()
        );
    END IF;

    PERFORM pg_notify('game_changed', payload::text);
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

-- Attach trigger to game table
CREATE TRIGGER game_changed_trigger
AFTER INSERT OR UPDATE OR DELETE ON kahoot.game
FOR EACH ROW EXECUTE FUNCTION kahoot.notify_game_changed();