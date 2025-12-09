-- drop table if exists criterion;
-- drop table if exists batch;
-- drop table if exists poi;
-- drop table if exists category;

create table if not exists category (
  id integer primary key not null,
  [name] varchar not null
);

CREATE TEMP TABLE temp_category(id, [name]);

INSERT INTO temp_category(id, [name])
VALUES
    (1, 'Airport'),
    (2, 'Bus Station'),
    (3, 'Coffee Shop');

INSERT INTO category (id, [name])
SELECT t.id, t.[name]
FROM temp_category t
WHERE NOT EXISTS (SELECT 1 FROM category c WHERE c.id = t.id);

DROP TABLE temp_category;

create table if not exists criterion (
    id integer primary key not null,
    lft integer not null,
    rgt integer not null,
    operator int null,
    category_id int null,
    dist_amt decimal null,
    FOREIGN KEY (category_id) REFERENCES category(id)
);
-- TODO operator should not be null if category_id or dist_amt is null, and vice versa

INSERT INTO criterion (id, lft, rgt, operator, category_id, dist_amt)
SELECT 1, 1, 2, 0, NULL, NULL
WHERE NOT EXISTS (SELECT 1 FROM criterion);

create table if not exists [location] (
    id integer primary key not null,
    [name] varchar not null,
    [address] varchar null,
    lat decimal(8,6) null,
    lon decimal(9,6) null,
    notes varchar null,
    price_amt int null,
    price_ccy char(3) null
);

create table if not exists batch (
  id integer primary key not null,
  source varchar,
  run_at timestamp
);

create table if not exists poi (
    id integer primary key not null,
    batch_id int null,
    source_xref varchar null,
    category_id int null,
    lat decimal(8,6) null,
    lon decimal(9,6) null,
    FOREIGN KEY (batch_id) REFERENCES batch(id),
    FOREIGN KEY (category_id) REFERENCES category(id)
);

-- insert into location ([name], [address], lat, lon, notes)
-- values
--  ('White House', '1600 Pennsylvania Avenue NW, Washington, D.C. 20500', 38.89774479, -77.03670855, 'The US President lives here')
-- ,('British Embassy', '3100 Massachusetts Avenue NW, Washington, D.C. 20008', 38.92053149, -77.06308419, 'Jolly good old chap');

-- create table if not exists lookups (id int, name varchar, name_value varchar);
-- create table if not exists settings (ccy char(3), dist_unit { mi | km });

-- create table if not exists category (id int, name varchar);
-- create table if not exists source (id int, name varchar);
-- create table if not exists category_source (id int, category_id int, source_id int, params varchar);
-- create table if not exists batch (id int, source varchar, run_at timestamp);
-- create table if not exists poi (id int, batch_id int, source varchar, source_id varchar, lat decimal(8,6), lon decimal(9,6), updated_at timestamp);

-- create table if not exists location (id int, name varchar, address varchar, lat decimal(8,6), lon decimal(9,6), notes varchar, price_amt int, price_ccy char(3));

-- create table if not exists distance (id int, criteria_id int, location_id int, measurement_mode {great circle|geodesic|road network}, dist_km decimal, dist_unit {mi | km});
-- create or replace view vw_score (criteria_id int, location_id int, raw_value decimal, norm_value decimal);
-- TODO stored proc or app code
