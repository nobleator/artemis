drop sequence if exists main.score_id_seq;
drop table if exists main.score;

CREATE SEQUENCE IF NOT EXISTS main.category_id_seq START 1;
CREATE TABLE IF NOT EXISTS main.category (
  id INTEGER PRIMARY KEY DEFAULT NEXTVAL('main.category_id_seq'),
  "name" VARCHAR NOT NULL
);

CREATE TEMP TABLE temp_category (
  id INTEGER,
  "name" VARCHAR
);

INSERT INTO temp_category (id, "name") VALUES
  (  0, 'Job'),
  (  1, 'Airport'),
  (  2, 'Bus Station'),
  (  3, 'Coffee Shop'),
  (  4, 'Fire Station'),
  (  5, 'Grocery'),
  (  6, 'Library'),
  (  7, 'Park'),
  (  8, 'Police Station'),
  (  9, 'School'),
  (  10, 'Train Station'),
  (  11, 'Whole Foods'),
  (  12, 'Trader Joes'),
  (  13, 'Giant'),
  (  14, 'Safeway'),
  (  15, 'Harris Teeter'),
  (  16, 'Bike Trail'),
  (1001, 'Comfortable Days');

INSERT INTO main.category (id, "name")
SELECT t.id, t."name"
FROM temp_category t
WHERE NOT EXISTS (
  SELECT 1 FROM main.category c WHERE c.id = t.id
);

DROP TABLE temp_category;

CREATE SEQUENCE IF NOT EXISTS main.tree_id_seq START 1;
CREATE TABLE IF NOT EXISTS main.tree (
  id INTEGER PRIMARY KEY DEFAULT NEXTVAL('main.tree_id_seq'),
  "name" VARCHAR NOT NULL
);

CREATE SEQUENCE IF NOT EXISTS main.criterion_id_seq START 1;
CREATE TABLE IF NOT EXISTS main.criterion (
  id INTEGER PRIMARY KEY DEFAULT NEXTVAL('main.criterion_id_seq'),
  tree_id INTEGER NOT NULL,
  lft INTEGER NOT NULL,
  rgt INTEGER NOT NULL,
  "operator" INTEGER,
  category_id INTEGER,
  dist_amt DOUBLE PRECISION,
  FOREIGN KEY (category_id) REFERENCES main.category(id),
  FOREIGN KEY (tree_id) REFERENCES main.tree(id),
  UNIQUE (tree_id, lft),
  UNIQUE (tree_id, rgt)
);
CREATE INDEX IF NOT EXISTS criterion_tree_lft_idx ON main.criterion (tree_id, lft);

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
  UNIQUE (source, source_xref, category_id)
);

CREATE SEQUENCE IF NOT EXISTS main.region_id_seq START 1;
CREATE TABLE IF NOT EXISTS main.region (
  id INTEGER PRIMARY KEY DEFAULT NEXTVAL('main.region_id_seq'),
  "name" VARCHAR NOT NULL,
  min_lat DOUBLE NOT NULL,
  min_lon DOUBLE NOT NULL,
  max_lat DOUBLE NOT NULL,
  max_lon DOUBLE NOT NULL
);

CREATE TEMP TABLE temp_region (
  id INTEGER,
  "name" VARCHAR,
  min_lat DOUBLE,
  min_lon DOUBLE,
  max_lat DOUBLE,
  max_lon DOUBLE
);

INSERT INTO temp_region (id, "name", min_lat, min_lon, max_lat, max_lon) VALUES
  ( 1, 'New York',         40.696951,  -74.022437, 40.758613,  -73.952075),
  ( 2, 'Washington, DC',   38.64287,   -77.555545, 39.140061,  -76.728104),
  ( 3, 'San Diego',        32.614624, -117.321743, 32.830673, -116.972894),
  ( 4, 'Los Angeles',      34.032911, -118.283255, 34.069655, -118.234676),
  ( 5, 'Chicago',          41.868164,  -87.652981, 41.907202,  -87.612951),
  ( 6, 'Houston',          29.735866,  -95.384607, 29.774127,  -95.348109),
  ( 7, 'Phoenix',          33.430866, -112.096876, 33.470053, -112.048969),
  ( 8, 'Philadelphia',     39.939152,  -75.176011, 39.965578,  -75.14382),
  ( 9, 'San Antonio',      29.410548,  -98.502244, 29.435556,  -98.469192),
  (10, 'Dallas',           32.766481,  -96.815506, 32.796213,  -96.780762),
  (11, 'Austin',           30.253746,  -97.755432, 30.281765,  -97.729263),
  (12, 'Jacksonville',     30.316138,  -81.681806, 30.342879,  -81.647116),
  (13, 'Fort Worth',       32.744784,  -97.339758, 32.766786,  -97.312088),
  (14, 'San Jose',         37.324715, -121.904297, 37.352759, -121.870651),
  (15, 'Columbus',         39.945157,  -83.021484, 39.976837,  -82.977674),
  (16, 'Charlotte',        35.216774,  -80.860214, 35.238742,  -80.828171),
  (17, 'Indianapolis',     39.756072,  -86.176376, 39.780155,  -86.14212),
  (18, 'San Francisco',    37.7595,   -122.4494,   37.8018,   -122.3936),
  (19, 'Seattle',          47.593742, -122.352622, 47.626504, -122.319908),
  (20, 'Denver',           39.734211, -105.005844, 39.762921, -104.977226),
  (21, 'Boston',           42.34718,   -71.076146, 42.372307,  -71.038284),
  (22, 'El Paso',          31.752233, -106.502113, 31.774822, -106.478271),
  (23, 'Nashville',        36.147074,  -86.795387, 36.176452,  -86.766663),
  (24, 'Detroit',          42.324146,  -83.06549,  42.348189,  -83.032913),
  (25, 'Oklahoma City',    35.457948,  -97.532883, 35.481077,  -97.502136),
  (26, 'Portland',         45.512223, -122.694244, 45.531525, -122.665062),
  (27, 'Las Vegas',        36.153229, -115.166893, 36.180212, -115.129547),
  (28, 'Memphis',          35.135331,  -90.061188, 35.156097,  -90.032349),
  (29, 'Louisville',       38.240272,  -85.769806, 38.264954,  -85.734558),
  (30, 'Baltimore',        39.279842,  -76.626511, 39.30798,   -76.590233),
  (31, 'Milwaukee',        43.028005,  -87.923355, 43.05248,   -87.895355),
  (32, 'Albuquerque',      35.07709,  -106.66301,  35.096577, -106.634674),
  (33, 'Tucson',           32.209934, -110.988464, 32.235504, -110.958843),
  (34, 'Fresno',           36.727974, -119.79554,  36.752308, -119.766769),
  (35, 'Sacramento',       38.566666, -121.503296, 38.588528, -121.478882),
  (36, 'Mesa',             33.402946, -111.850281, 33.428528, -111.821899),
  (37, 'Kansas City',      39.089478,  -94.591064, 39.111328,  -94.567566),
  (38, 'Atlanta',          33.740498,  -84.404411, 33.774845,  -84.365387),
  (39, 'Omaha',            41.246788,  -95.955238, 41.267685,  -95.930344),
  (40, 'Colorado Springs', 38.826904, -104.834213, 38.848907, -104.807053),
  (41, 'Raleigh',          35.767925,  -78.653275, 35.789364,  -78.628723),
  (42, 'Miami',            25.756672,  -80.211258, 25.792282,  -80.183258),
  (43, 'Long Beach',       33.762436, -118.207932, 33.786285, -118.181229),
  (44, 'Virginia Beach',   36.836052,  -76.062012, 36.858032,  -76.033325),
  (45, 'Oakland',          37.788666, -122.279152, 37.815315, -122.254333),
  (46, 'Minneapolis',      44.965313,  -93.28009,  44.989979,  -93.25325),
  (47, 'Tulsa',            36.140587,  -95.998283, 36.161221,  -95.976318),
  (48, 'Tampa',            27.936527,  -82.470856, 27.958233,  -82.44574),
  (49, 'Arlington',        32.732208,  -97.111282, 32.756577,  -97.086655),
  (50, 'New Orleans',      29.941002,  -90.083618, 29.964153,  -90.056);

INSERT INTO main.region (id, "name", min_lat, min_lon, max_lat, max_lon)
SELECT t.id, t."name", t.min_lat, t.min_lon, t.max_lat, t.max_lon
FROM temp_region t
WHERE NOT EXISTS (
  SELECT 1 FROM main.region r WHERE r.id = t.id
);

DROP TABLE temp_region;

CREATE SEQUENCE IF NOT EXISTS main.rc_open_meteo_id_seq START 1;
CREATE TABLE IF NOT EXISTS main.rc_open_meteo (
  id INTEGER PRIMARY KEY DEFAULT NEXTVAL('main.rc_open_meteo_id_seq'),
  batch_id INTEGER NOT NULL,
  region_id INTEGER NOT NULL,
  lat DOUBLE,
  lon DOUBLE,
  "time" DATE,
  temperature_2m_min DOUBLE,
  temperature_2m_mean DOUBLE,
  temperature_2m_max DOUBLE,
  dew_point_2m_min DOUBLE,
  dew_point_2m_mean DOUBLE,
  dew_point_2m_max DOUBLE,
  FOREIGN KEY (batch_id) REFERENCES main.batch(id),
  FOREIGN KEY (region_id) REFERENCES main.region(id),
  UNIQUE (lat, lon, "time")
);

-- Seeding dummy job POI
INSERT INTO main.batch VALUES
(-1, 'Seed SQL', 'Success', current_timestamp, current_timestamp)
ON CONFLICT (id) DO NOTHING;

INSERT INTO main.poi VALUES
(-1, -1, 'Seed SQL', 'job-1', 0, 38.89542918499952, -77.02506182718213)
ON CONFLICT (id) DO NOTHING;

CREATE SEQUENCE IF NOT EXISTS main.score_id_seq START 1;
CREATE TABLE IF NOT EXISTS main.score (
  id INTEGER PRIMARY KEY DEFAULT NEXTVAL('main.score_id_seq'),
  eval_mode VARCHAR(10) NOT NULL,
  location_id INTEGER NOT NULL,
  criterion_id INTEGER NOT NULL,
  key_poi_id INTEGER NOT NULL,
  raw_value DECIMAL(9,6) NOT NULL,
  norm_value DECIMAL(9,6) NOT NULL,
  FOREIGN KEY (location_id) REFERENCES main."location"(id),
  -- FOREIGN KEY (criterion_id) REFERENCES main.criterion(id),
  FOREIGN KEY (key_poi_id) REFERENCES main.poi(id)
);
