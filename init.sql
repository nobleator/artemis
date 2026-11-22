INSTALL spatial;
LOAD spatial;

SET threads = 1;

CREATE SCHEMA IF NOT EXISTS raw;
CREATE SCHEMA IF NOT EXISTS core;

CREATE SEQUENCE IF NOT EXISTS core.dim_region_id_seq;
CREATE SEQUENCE IF NOT EXISTS core.dim_coc_id_seq;

CREATE TABLE IF NOT EXISTS core.dim_region (
    region_id INTEGER PRIMARY KEY DEFAULT nextval('core.dim_region_id_seq'),
    cbsa_code VARCHAR NOT NULL UNIQUE,
    region_name VARCHAR NOT NULL,
    lat DOUBLE NOT NULL,
    lon DOUBLE NOT NULL,
    land_area_m2 BIGINT,
    water_area_m2 BIGINT,
    geom GEOMETRY,
    is_active BOOLEAN NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS core.dim_coc (
    coc_id INTEGER PRIMARY KEY DEFAULT nextval('core.dim_coc_id_seq'),
    coc_number VARCHAR NOT NULL UNIQUE,
    coc_name VARCHAR NOT NULL,
    state_code VARCHAR NOT NULL,
    first_year SMALLINT NOT NULL,
    last_year SMALLINT NOT NULL,
    is_active BOOLEAN NOT NULL,
    geom GEOMETRY
);

CREATE TABLE IF NOT EXISTS core.map_region_coc (
    region_id INTEGER NOT NULL,
    coc_id INTEGER NOT NULL,
    overlap_area_deg2 DOUBLE NOT NULL,
    pct_of_region DOUBLE NOT NULL,
    pct_of_coc DOUBLE NOT NULL,
    PRIMARY KEY (region_id, coc_id)
);

CREATE TABLE IF NOT EXISTS core.dim_date (
    date_id INTEGER PRIMARY KEY,
    full_date DATE NOT NULL UNIQUE,
    year SMALLINT NOT NULL,
    quarter SMALLINT NOT NULL,
    month SMALLINT NOT NULL,
    month_name VARCHAR NOT NULL,
    day_of_month SMALLINT NOT NULL,
    iso_day_of_week SMALLINT NOT NULL,
    day_name VARCHAR NOT NULL,
    is_weekend BOOLEAN NOT NULL
);

CREATE TABLE IF NOT EXISTS core.fct_weather_daily (
    region_id INTEGER NOT NULL,
    date_id INTEGER NOT NULL,
    temp_min_f DOUBLE,
    temp_mean_f DOUBLE,
    temp_max_f DOUBLE,
    humidity_min_pct DOUBLE,
    humidity_mean_pct DOUBLE,
    humidity_max_pct DOUBLE,
    dew_point_min_f DOUBLE,
    dew_point_mean_f DOUBLE,
    dew_point_max_f DOUBLE,
    PRIMARY KEY (region_id, date_id)
);

CREATE TABLE IF NOT EXISTS core.fct_pollen (
    region_id INTEGER NOT NULL,
    year SMALLINT NOT NULL,
    week SMALLINT NOT NULL,
    pollen_type VARCHAR NOT NULL,
    pollen_count DOUBLE NOT NULL,
    PRIMARY KEY (region_id, year, week, pollen_type)
);

CREATE OR REPLACE TABLE raw.hud_coc (
    sheet_year INTEGER NOT NULL,
    coc_number VARCHAR NOT NULL,
    coc_name VARCHAR NOT NULL
);

CREATE TABLE IF NOT EXISTS raw.hud_coc_boundary (
    COCNUM VARCHAR,
    geom GEOMETRY
);

CREATE TABLE IF NOT EXISTS raw.open_meteo_historical_weather (
    cbsa_code VARCHAR NOT NULL,
    latitude DOUBLE NOT NULL,
    longitude DOUBLE NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    response JSON NOT NULL,
    fetched_at TIMESTAMP NOT NULL DEFAULT current_timestamp,
    PRIMARY KEY (cbsa_code, start_date, end_date)
);

CREATE SEQUENCE IF NOT EXISTS main.tree_id_seq START 1;
CREATE TABLE IF NOT EXISTS main.tree (
  id INTEGER PRIMARY KEY DEFAULT NEXTVAL('main.tree_id_seq'),
  "name" VARCHAR NOT NULL
);

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

drop sequence if exists main.score_id_seq;
drop table if exists main.score;
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