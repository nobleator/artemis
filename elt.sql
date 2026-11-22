CREATE OR REPLACE TABLE raw.cdc_weekly_pollen_concentration AS
SELECT *
FROM read_csv('/Users/bnoble/Projects/artemis/data/cdc_pollen_bgxp-nxmn_20260927.csv');

INSERT OR IGNORE INTO core.dim_date
SELECT
    CAST(strftime(d, '%Y%m%d') AS INTEGER),
    d,
    year(d),
    quarter(d),
    month(d),
    monthname(d),
    day(d),
    isodow(d),
    dayname(d),
    isodow(d) >= 6
FROM (
    SELECT CAST(ts AS DATE) AS d
    FROM range(getvariable('d0'), getvariable('d1') + 1, INTERVAL 1 DAY) AS t(ts)
);

INSERT INTO core.fct_weather_daily
SELECT
    r.region_id,
    CAST(strftime(u.observed_date, '%Y%m%d') AS INTEGER),
    u.temp_min_f,
    u.temp_mean_f,
    u.temp_max_f,
    u.humidity_min_pct,
    u.humidity_mean_pct,
    u.humidity_max_pct,
    u.dew_point_min_f,
    u.dew_point_mean_f,
    u.dew_point_max_f
FROM (
    SELECT
        w.cbsa_code,
        CAST(unnest(w.payload.daily."time") AS DATE) AS observed_date,
        unnest(w.payload.daily.temperature_2m_min) AS temp_min_f,
        unnest(w.payload.daily.temperature_2m_mean) AS temp_mean_f,
        unnest(w.payload.daily.temperature_2m_max) AS temp_max_f,
        unnest(w.payload.daily.relative_humidity_2m_min) AS humidity_min_pct,
        unnest(w.payload.daily.relative_humidity_2m_mean) AS humidity_mean_pct,
        unnest(w.payload.daily.relative_humidity_2m_max) AS humidity_max_pct,
        unnest(w.payload.daily.dew_point_2m_min) AS dew_point_min_f,
        unnest(w.payload.daily.dew_point_2m_mean) AS dew_point_mean_f,
        unnest(w.payload.daily.dew_point_2m_max) AS dew_point_max_f
    FROM (
        SELECT
            cbsa_code,
            from_json(response, '{"daily":{"time":["VARCHAR"],"temperature_2m_min":["DOUBLE"],"temperature_2m_mean":["DOUBLE"],"temperature_2m_max":["DOUBLE"],"relative_humidity_2m_min":["DOUBLE"],"relative_humidity_2m_mean":["DOUBLE"],"relative_humidity_2m_max":["DOUBLE"],"dew_point_2m_min":["DOUBLE"],"dew_point_2m_mean":["DOUBLE"],"dew_point_2m_max":["DOUBLE"]}}') AS payload
        FROM raw.open_meteo_historical_weather
    ) AS w
) AS u
JOIN core.dim_region AS r
    ON r.cbsa_code = u.cbsa_code
WHERE u.temp_mean_f IS NOT NULL
ON CONFLICT (region_id, date_id) DO NOTHING;

INSERT INTO core.fct_pollen
SELECT
    r.region_id,
    p.year,
    p.week,
    p.pollen_type,
    p.pollen_count
FROM (
    SELECT
        trim(pollen_site) AS site,
        trim(state) AS state,
        CAST(replace(CAST(year AS VARCHAR), ',', '') AS SMALLINT) AS year,
        CAST(replace(CAST(week AS VARCHAR), ',', '') AS SMALLINT) AS week,
        trim(pollen_type) AS pollen_type,
        CAST(replace(CAST(pollen_count AS VARCHAR), ',', '') AS DOUBLE) AS pollen_count
    FROM raw.cdc_weekly_pollen_concentration
    WHERE pollen_count IS NOT NULL
) AS p
JOIN core.dim_region AS r
    ON list_contains(string_split(split_part(r.region_name, ', ', 1), '-'), p.site)
   AND list_contains(string_split(split_part(r.region_name, ', ', 2), '-'), p.state)
ON CONFLICT (region_id, year, week, pollen_type) DO NOTHING;