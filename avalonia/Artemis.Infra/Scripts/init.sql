-- drop table if exists score;
-- drop table if exists criterion;
-- drop table if exists poi;
-- drop table if exists batch;
-- drop table if exists category;
-- drop table if exists [location];

create table if not exists category (
  id integer primary key not null,
  [name] varchar not null
);

CREATE TEMP TABLE temp_category(id, [name]);

INSERT INTO temp_category(id, [name])
VALUES
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

CREATE TEMP TABLE temp_criterion (
  id INTEGER,
  lft INTEGER,
  rgt INTEGER,
  operator INTEGER,
  category_id INTEGER,
  dist_amt DECIMAL
);

INSERT INTO temp_criterion VALUES
-- ROOT AND
(1,  1, 38, 0, NULL, NULL),
-- Simple AND leaves
(2, 2,  3, NULL, 9,  0.1),  -- Elementary School
(3, 4,  5, NULL, 7,  0.2),  -- Park
(4, 6,  7, NULL, 6,  0.5),  -- Library
(5, 8,  9, NULL, 1, 20.0),  -- Airport
-- (Whole Foods OR Trader Joes)
(6, 10, 15, 1, NULL, NULL),
(7, 11, 12, NULL, 11, 5.0),
(8, 13, 14, NULL, 12, 5.0),
-- (Giant OR Safeway OR Harris Teeter)
(9, 16, 23, 1, NULL, NULL),
(10, 17, 18, NULL, 13, 1.0),
(11, 19, 20, NULL, 14, 1.0),
(12, 21, 22, NULL, 15, 1.0),
-- Job logic OR
(13, 24, 37, 1, NULL, NULL),
-- Job < 0.5
(14, 25, 26, NULL, 16, 0.5),
-- (Job < 5 AND Bike Trail < 1)
(15, 27, 32, 0, NULL, NULL),
(16, 28, 29, NULL, 16, 5.0),
(17, 30, 31, NULL, 17, 1.0),
-- (Job < 10 AND Train Station < 0.5)
(18, 33, 38, 0, NULL, NULL),
(19, 34, 35, NULL, 16,10.0),
(20, 36, 37, NULL, 10, 0.5);

INSERT INTO criterion (id, lft, rgt, operator, category_id, dist_amt)
SELECT
  t.id, t.lft, t.rgt, t.operator, t.category_id, t.dist_amt
FROM temp_criterion t
WHERE NOT EXISTS (
  SELECT 1 FROM criterion c WHERE c.id = 1 -- Only insert if root is missing
);

DROP TABLE temp_criterion;

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
  source varchar not null,
  [status] varchar not null,
  start_utc timestamp not null,
  end_utc timestamp null
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

create table if not exists score (
  id integer primary key not null,
  location_id int not null,
  criterion_id int not null,
  raw_value decimal(9,6) not null,
  norm_value decimal(9,6) not null,
  FOREIGN KEY (location_id) REFERENCES [location](id),
  FOREIGN KEY (criterion_id) REFERENCES criterion(id)
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
