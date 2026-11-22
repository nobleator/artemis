CREATE OR REPLACE TABLE raw.census_msa AS
SELECT *
FROM ST_Read('/Users/bnoble/Projects/artemis/data/tl_2026_us_cbsa/tl_2026_us_cbsa.shp');

CREATE OR REPLACE TABLE raw.hud_coc_boundary AS
SELECT *
FROM ST_Read('/Users/bnoble/Projects/artemis/data/Continuum_of_Care_Grantee_Areas_-3232323369725890958.geojson');

INSERT INTO core.dim_region (cbsa_code, region_name, lat, lon, land_area_m2, water_area_m2, geom)
SELECT
    trim(CBSAFP),
    trim(NAME),
    round(CAST(INTPTLAT AS DOUBLE), 4),
    round(CAST(INTPTLON AS DOUBLE), 4),
    ALAND,
    AWATER,
    geom
FROM raw.census_msa
WHERE LSAD = 'M1'
  AND nullif(trim(CBSAFP), '') IS NOT NULL
  AND nullif(trim(NAME), '') IS NOT NULL
  AND CAST(INTPTLAT AS DOUBLE) BETWEEN -90 AND 90
  AND CAST(INTPTLON AS DOUBLE) BETWEEN -180 AND 180
ON CONFLICT (cbsa_code) DO UPDATE SET
    region_name = excluded.region_name,
    lat = excluded.lat,
    lon = excluded.lon,
    land_area_m2 = excluded.land_area_m2,
    water_area_m2 = excluded.water_area_m2,
    geom = excluded.geom;

INSERT INTO core.dim_coc (coc_number, coc_name, state_code, first_year, last_year, is_active, geom)
SELECT
    c.coc_number,
    c.coc_name,
    left(c.coc_number, 2),
    c.first_year,
    c.last_year,
    c.last_year = m.max_year,
    b.geom
FROM (
    SELECT
        coc_number,
        arg_max(coc_name, sheet_year) AS coc_name,
        min(sheet_year) AS first_year,
        max(sheet_year) AS last_year
    FROM raw.hud_coc
    GROUP BY coc_number
) AS c
CROSS JOIN (
    SELECT max(sheet_year) AS max_year
    FROM raw.hud_coc
) AS m
LEFT JOIN (
    SELECT
        trim(COCNUM) AS coc_number,
        ST_Union_Agg(geom) AS geom
    FROM raw.hud_coc_boundary
    GROUP BY 1
) AS b
    ON b.coc_number = c.coc_number
ON CONFLICT (coc_number) DO UPDATE SET
    coc_name = excluded.coc_name,
    state_code = excluded.state_code,
    first_year = excluded.first_year,
    last_year = excluded.last_year,
    is_active = excluded.is_active,
    geom = excluded.geom;

DELETE FROM core.map_region_coc;

INSERT INTO core.map_region_coc
SELECT
    region_id,
    coc_id,
    overlap_area,
    overlap_area / region_area,
    overlap_area / coc_area
FROM (
    SELECT
        r.region_id,
        c.coc_id,
        ST_Area(ST_Intersection(ST_MakeValid(r.geom), ST_MakeValid(c.geom))) AS overlap_area,
        ST_Area(r.geom) AS region_area,
        ST_Area(c.geom) AS coc_area
    FROM core.dim_region AS r
    JOIN core.dim_coc AS c
        ON ST_Intersects(r.geom, c.geom)
    WHERE r.geom IS NOT NULL
      AND c.geom IS NOT NULL
)
WHERE overlap_area / region_area >= 0.01
   OR overlap_area / coc_area >= 0.01;