-- drop sequence if exists main.score_id_seq;
-- drop table if exists main.score;
-- drop sequence if exists main.criterion_id_seq;
-- drop table if exists main.criterion;
-- drop sequence if exists main.poi_id_seq;
-- drop table if exists main.poi;
-- drop sequence if exists main.batch_id_seq;
-- drop table if exists main.batch;
-- drop sequence if exists main.category_id_seq;
-- drop table if exists main.category;
-- drop sequence if exists main.location_id_seq;
-- drop table if exists main."location";

CREATE SEQUENCE IF NOT EXISTS main.category_id_seq START 1;
CREATE TABLE IF NOT EXISTS main.category (
  id INTEGER PRIMARY KEY DEFAULT NEXTVAL('main.category_id_seq'),
  "name" VARCHAR NOT NULL
);

CREATE TEMP TABLE main.temp_category (
  id INTEGER,
  "name" VARCHAR
);

INSERT INTO main.temp_category (id, "name") VALUES
  (1, 'Airport'),
  (2, 'Bus Station'),
  (3, 'Coffee Shop'),
  (4, 'Fire Station'),
  (5, 'Grocery'),
  (6, 'Library'),
  (7, 'Park'),
  (8, 'Police Station'),
  (9, 'School'),
  (10, 'Train Station'),
  (11, 'Whole Foods'),
  (12, 'Trader Joes'),
  (13, 'Giant'),
  (14, 'Safeway'),
  (15, 'Harris Teeter'),
  (16, 'Job'),
  (17, 'Bike Trail');

INSERT INTO main.category (id, "name")
SELECT t.id, t."name"
FROM main.temp_category t
WHERE NOT EXISTS (
  SELECT 1 FROM main.category c WHERE c.id = t.id
);

DROP TABLE main.temp_category;

CREATE SEQUENCE IF NOT EXISTS main.criterion_id_seq START 1;
CREATE TABLE IF NOT EXISTS main.criterion (
  id INTEGER PRIMARY KEY DEFAULT NEXTVAL('main.criterion_id_seq'),
  lft INTEGER NOT NULL,
  rgt INTEGER NOT NULL,
  "operator" INTEGER,
  category_id INTEGER,
  dist_amt DECIMAL(9,3),
  FOREIGN KEY (category_id) REFERENCES main.category(id)
);

-- CREATE TEMP TABLE temp_criterion (
--   id INTEGER,
--   lft INTEGER,
--   rgt INTEGER,
--   "operator" INTEGER,
--   category_id INTEGER,
--   dist_amt DECIMAL(9,3)
-- );

-- INSERT INTO temp_criterion VALUES
-- (1,  1, 38, 0, NULL, NULL),
-- (2,  2,  3, NULL, 9,  0.1),
-- (3,  4,  5, NULL, 7,  0.2),
-- (4,  6,  7, NULL, 6,  0.5),
-- (5,  8,  9, NULL, 1, 20.0),
-- (6, 10, 15, 1, NULL, NULL),
-- (7, 11, 12, NULL, 11, 5.0),
-- (8, 13, 14, NULL, 12, 5.0),
-- (9, 16, 23, 1, NULL, NULL),
-- (10, 17, 18, NULL, 13, 1.0),
-- (11, 19, 20, NULL, 14, 1.0),
-- (12, 21, 22, NULL, 15, 1.0),
-- (13, 24, 37, 1, NULL, NULL),
-- (14, 25, 26, NULL, 16, 0.5),
-- (15, 27, 32, 0, NULL, NULL),
-- (16, 28, 29, NULL, 16, 5.0),
-- (17, 30, 31, NULL, 17, 1.0),
-- (18, 33, 38, 0, NULL, NULL),
-- (19, 34, 35, NULL, 16, 10.0),
-- (20, 36, 37, NULL, 10, 0.5);

-- INSERT INTO criterion (id, lft, rgt, "operator", category_id, dist_amt)
-- SELECT
--   t.id, t.lft, t.rgt, t."operator", t.category_id, t.dist_amt
-- FROM temp_criterion t
-- WHERE NOT EXISTS (
--   SELECT 1 FROM criterion c WHERE c.id = 1
-- );

-- DROP TABLE temp_criterion;

CREATE SEQUENCE IF NOT EXISTS main.location_id_seq START 1;
CREATE TABLE IF NOT EXISTS main."location" (
  id INTEGER PRIMARY KEY DEFAULT NEXTVAL('main.location_id_seq'),
  "name" VARCHAR NOT NULL,
  "address" VARCHAR,
  lat DOUBLE,
  lon DOUBLE,
  notes VARCHAR,
  price_amt INTEGER,
  price_ccy CHAR(3),
  UNIQUE ("address")
);

CREATE SEQUENCE IF NOT EXISTS main.batch_id_seq START 1;
CREATE TABLE IF NOT EXISTS main.batch (
  id INTEGER PRIMARY KEY DEFAULT NEXTVAL('main.batch_id_seq'),
  source VARCHAR NOT NULL,
  "status" VARCHAR NOT NULL,
  start_utc TIMESTAMP NOT NULL,
  end_utc TIMESTAMP
);

CREATE SEQUENCE IF NOT EXISTS main.poi_id_seq START 1;
CREATE TABLE IF NOT EXISTS main.poi (
  id INTEGER PRIMARY KEY DEFAULT NEXTVAL('main.poi_id_seq'),
  batch_id INTEGER NOT NULL,
  source VARCHAR NOT NULL,
  source_xref VARCHAR,
  category_id INTEGER,
  lat DOUBLE,
  lon DOUBLE,
  FOREIGN KEY (batch_id) REFERENCES main.batch(id),
  FOREIGN KEY (category_id) REFERENCES main.category(id),
  UNIQUE (source, source_xref)
);

CREATE SEQUENCE IF NOT EXISTS main.score_id_seq START 1;
CREATE TABLE IF NOT EXISTS main.score (
  id INTEGER PRIMARY KEY DEFAULT NEXTVAL('main.score_id_seq'),
  location_id INTEGER NOT NULL,
  criterion_id INTEGER NOT NULL,
  raw_value DECIMAL(9,6) NOT NULL,
  norm_value DECIMAL(9,6) NOT NULL,
  FOREIGN KEY (location_id) REFERENCES main."location"(id),
  FOREIGN KEY (criterion_id) REFERENCES main.criterion(id)
);
