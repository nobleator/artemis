create table if not exists criteria (
    id integer primary key not null,
    lft integer not null,
    rgt integer not null,
    node_id integer not null
);
create table if not exists node_group (
    id integer primary key not null,
    operator int not null
);
create table if not exists node_term (
    id integer primary key not null,
    category_id int not null,
    dist_amt decimal not null
);
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


-- insert into location ([name], [address], lat, lon, notes)
-- values
--  ('White House', '1600 Pennsylvania Avenue NW, Washington, D.C. 20500', 38.89774479, -77.03670855, 'The US President lives here')
-- ,('British Embassy', '3100 Massachusetts Avenue NW, Washington, D.C. 20008', 38.92053149, -77.06308419, 'Jolly good old chap');

-- create table if not exists lookups (id int, name varchar, name_value varchar);
-- create table if not exists settings (ccy char(3), dist_unit { mi | km });

-- create table if not exists criteria (id int, lft int, rgt int, node_id int);
-- create table if not exists node_group (id int, operator int {and | or});
-- create table if not exists node_term (id int, category_id int, dist_amt decimal);

-- create table if not exists category (id int, name varchar);
-- create table if not exists source (id int, name varchar);
-- create table if not exists category_source (id int, category_id int, source_id int, params varchar);
-- create table if not exists poi (id int, source varchar, source_id varchar, lat decimal(8,6), lon decimal(9,6), updated_on timestamp);

-- create table if not exists location (id int, name varchar, address varchar, lat decimal(8,6), lon decimal(9,6), notes varchar, price_amt int, price_ccy char(3));

-- create table if not exists distance (id int, criteria_id int, location_id int, measurement_mode {great circle|geodesic|road network}, dist_km decimal, dist_unit {mi | km});
-- create or replace view vw_score (criteria_id int, location_id int, raw_value decimal, norm_value decimal);
-- TODO sproc or app code
