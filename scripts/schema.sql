-- PostgreSQL schema for ticketing system

CREATE TYPE ticket_status AS ENUM (
  'available',
  'reserved',
  'paid',
  'released',
  'cancelled'
);

CREATE TYPE payment_status AS ENUM (
  'pending',
  'approved',
  'failed',
  'expired'
);

CREATE TABLE events (
  id BIGSERIAL PRIMARY KEY,
  name VARCHAR(200) NOT NULL,
  starts_at TIMESTAMPTZ NOT NULL
);

CREATE TABLE tickets (
  id BIGSERIAL PRIMARY KEY,
  event_id BIGINT NOT NULL REFERENCES events(id) ON DELETE CASCADE,
  status ticket_status NOT NULL DEFAULT 'available',
  reserved_at TIMESTAMPTZ,
  expires_at TIMESTAMPTZ,
  paid_at TIMESTAMPTZ,
  order_id VARCHAR(80),
  reserved_by VARCHAR(120),
  version INT NOT NULL DEFAULT 0,
  CONSTRAINT tickets_reserved_fields
    CHECK (
      (status <> 'reserved') OR (reserved_at IS NOT NULL AND expires_at IS NOT NULL)
    )
);

CREATE TABLE payments (
  id BIGSERIAL PRIMARY KEY,
  ticket_id BIGINT NOT NULL REFERENCES tickets(id) ON DELETE CASCADE,
  status payment_status NOT NULL DEFAULT 'pending',
  provider_ref VARCHAR(120),
  amount_cents INT NOT NULL CHECK (amount_cents >= 0),
  currency CHAR(3) NOT NULL DEFAULT 'USD',
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE ticket_history (
  id BIGSERIAL PRIMARY KEY,
  ticket_id BIGINT NOT NULL REFERENCES tickets(id) ON DELETE CASCADE,
  old_status ticket_status NOT NULL,
  new_status ticket_status NOT NULL,
  changed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  reason VARCHAR(200)
);

CREATE INDEX idx_tickets_status_expires_at ON tickets(status, expires_at);
CREATE INDEX idx_tickets_event_id ON tickets(event_id);
CREATE INDEX idx_payments_ticket_id ON payments(ticket_id);
CREATE INDEX idx_payments_status ON payments(status);

-- ---------------------------------------------------------------
-- Fair Queue: cola justa de acceso a tickets
-- Una entrada por usuario por ticket (uq_queue_user_ticket)
-- Posiciones únicas por ticket (uq_queue_position)
-- ---------------------------------------------------------------
CREATE TABLE fair_queue (
  id              BIGSERIAL PRIMARY KEY,
  event_id        BIGINT NOT NULL REFERENCES events(id) ON DELETE CASCADE,
  ticket_id       BIGINT NOT NULL REFERENCES tickets(id) ON DELETE CASCADE,
  user_id         VARCHAR(36)  NOT NULL,   -- claim 'sub' del JWT
  email           VARCHAR(255) NOT NULL,   -- claim 'email' del JWT
  position        INT NOT NULL,            -- 1-indexed, asignado al entrar
  status          VARCHAR(20)  NOT NULL DEFAULT 'waiting',
                  -- waiting | active | completed | cancelled | timed_out
  entered_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
  turn_started_at TIMESTAMPTZ,             -- cuando empieza su turno
  turn_expires_at TIMESTAMPTZ,             -- cuando vence su turno
  CONSTRAINT uq_queue_user_ticket UNIQUE (ticket_id, user_id),
  CONSTRAINT uq_queue_position    UNIQUE (ticket_id, position),
  CONSTRAINT chk_queue_status CHECK (
    status IN ('waiting','active','completed','cancelled','timed_out')
  )
);

CREATE INDEX idx_fq_ticket_status_pos ON fair_queue(ticket_id, status, position);
CREATE INDEX idx_fq_event             ON fair_queue(event_id, status);
CREATE INDEX idx_fq_turn_expires      ON fair_queue(turn_expires_at) WHERE status = 'active';
