# Background
Searching for a home is hard, and realtors are expensive. Everyone has preferences and requirements, but consistently applying those preferences is hard. Often consumers will compromise on their needs without full confidence that all they truly exhausted all of their options.

Enter Artemis. The core concept is to explicitly break down all your criteria for a home into a structured “preference tree”. We can then evaluate individual homes against this tree, returning a normalized score to allow like-for-like comparisons between candidates. This enables users to locate homes that meet their precise requirements. The end result should be simple enough for your mom to use, but complex enough for the nerd who built it.

# Design
## Application
This application is split across 3 major areas:
- API to manage user application data, define evaluation mode scoring logic, trigger ELT loads, and fetch results
- ELT pipeline to ingest external data required for scoring
- UI for reporting/analysis on the results

All data is stored in a DuckDB instance.
The ELT logic is written in F# functions, all sequenced together in `Pipeline.run`.
The API is written in F# and ASP.NET Core minimal APIs.
The UI is written in F#, compiled to JavaScript with Fable.

## Database
### ELT pipeline
The ELT pipeline that pulls together external data sources is maintained across 3 schemas: `meta`, `raw`, `core`

`meta`:
Each data source is configured in F# code, with a corresponding entry in the `meta` schema: `meta.data_source`
Metadata table to track execution status of each data source load, with FKs to `meta.data_source`. Includes run timestamps, row counts, and status: `meta.batch`

`Pipeline.run` (F#): loads data from APIs or CSVs into `raw` tables, then transforms into dimension (`dim_*`) and fact (`fct_*`) tables within `core`:
- Core definitions of metropolitan statistical areas, including GeoJSON boundary definitions. Sourced from [TIGER/Line Shapefiles](https://www.census.gov/geographies/mapping-files/time-series/geo/tiger-line-file.html): `raw.census_msa` -> `core.dim_region`
- Department of Housing and Urban Development (HUD) Continuums of Care (CoC) boundary definitions. Required to link homelessness data to standardized regions. Sourced from [HUD CoC Grantee Areas](https://hub.arcgis.com/datasets/HUD::continuum-of-care-coc-grantee-areas/about) `raw.hud_coc` -> `core.dim_coc`, `core.map_region_coc`
- Historical weather data. Sourced from [Open-Meteo historical weather API](https://open-meteo.com/en/docs/historical-weather-api): `raw.open_meteo_historical_weather` -> `core.fct_weather`
- Weekly pollen concentrations, with both tree and weed types. Sourced from [CDC weekly grouped pollen concentrations](https://data.cdc.gov/National-Center-for-Environmental-Health/Weekly-Grouped-Pollen-Concentrations-Trees-and-Wee/bgxp-nxmn/about_data): `raw.cdc_weekly_pollen_concentration` -> `core.fct_pollen`
- Categories of employment occupations from the Bureau of Labor Statistics (BLS) Occupational Employment and Wage Statistics (OEWS) occupation profiles. Sourced from [BLS OEWS occupation profiles](https://www.bls.gov/oes/2023/may/oes_stru.htm): `raw.bls_occupation_profile` -> `core.dim_occupation`
- BLS OEWS details. Sourced from [OEWS query system](https://data.bls.gov/oes/#/area/0047900/2025): `raw.bls_oew_statistics` -> `core.fct_jobs_by_occupation`
- BLS Job Openings and Labor Turnover Survey (JOLTS), providing employment data by industry. Sourced from [BLS JOLTS](https://www.bls.gov/jlt/data.htm): `raw.bls_jolts` -> `core.fct_jobs_by_industry`
- HUD homelessness estimates. Sourced from [AHAR PIT estimates](https://www.huduser.gov/portal/datasets/ahar/2025-ahar-part-1-pit-estimates-of-homelessness-in-the-us.html): `raw.hud_ahar_pit_homelessness` -> `core.fct_homeless`
- Open Street Map (OSM) points of interest (POI), with categories loaded in a separate dimension. Sourced from [Overpass API](https://overpass-turbo.eu/): `raw.osm_poi` -> `core.dim_poi_category`, `core.fct_poi`
- Foursquare open source POI data, with categories loaded in a separate dimension. Sourced from [Foursquare OS Places](https://opensource.foursquare.com/os-places/): `raw.fsq_os_poi` -> `core.dim_poi_category`, `core.fct_poi`
- TODO BLS or Census industry data, NAICS codes: `core.dim_industry`
- TODO FBI Crime Data Explorer (CDE): `core.dim_crime`, `core.fct_crime`
- TODO housing prices/cost of living: `core.fct_col`
- TODO natural disaster risk: `core.fct_disaster_risk`
- TODO demographics (age, sex ratios): `core.fct_demographics`
- TODO education: `core.school_rating`

### Application data
User preferences are maintained in a separate `app` schema:
- User profile details, expected to have 1 record when run locally. Also stores fields used for regional evaluations, such as preferred min/max temperatures: `app.profile`
- Preference tree root definitions, allowing for multiple separate trees per user: `app.tree`
- Nested sets tree node definitions, all linked to a tree ID: `app.criterion`
- Scores calculated for each criterion node, based on a specific evaluation mode. Retained in `app` schema as it will be rebuilt any time the underlying trees are updated: `app.score`

### Database scripts
All object definitions are maintained with idempotent SQL definitions within `init.sql`. Database initialization is validated on an in-memory DuckDB instance to ensure object dependencies are properly sequenced.


## Competitors
- [There to Where](https://theretowhere.com/)
- [Best Places](https://www.bestplaces.net/move/)

# Local Setup
TODO

# Operation

Configure bounding box(es)
Configure upstream data subscriptions
Upload listings (browser extension or helper script)
Run eval with specified methodologies
Analysis

See `./.vscode/launch.json` for examples on how to run this project.

# Unit Testing
TODO

# Deploy
TODO

## Dependencies
Stadia Maps
[Open Free Maps](https://openfreemap.org)

# Data Sources
Open Street Map
Overpass: `https://www.overpass-api.de/api/interpreter`
Nominatim: `https://nominatim.openstreetmap.org`
US Census Geocoding: `https://geocoding.geo.census.gov/geocoder/`
(TBD) Four Square Open Source Data: `https://opensource.foursquare.com/os-places/`
(TBD) Walk Score
(TBD) mypollenpal: `https://www.mypollenpal.com/api/pollen?location=Falls%20Church%2C%20VA&days=1`
(TBD) Weatherspark
(TBD) First Street
(TBD) Bureau of Labor Statistics job data
(TBD) US Census longitudinal household
(TBD) Open Infra Map
(TBD) Crime stats

Data categories:
- Geocoding
- Point of interest in a variety of categories
- Characteristics
    - Walkability
    - Pollen
    - Jobs?

# Manual listing load
URL with search params dynamically populated at run time. For each page in these results, fetch all `<article>` tags. Within each `<article>` there should be 3 nested fields:
1) A `<span>` tag with the data-test="property-card-price" attribute set. We will pull the text contents from these.
2) An `<a>` tag with the data-test="property-card-link" attribute set. We will pull the href from these.
3) An `<address>` tag. We will pull the text contents from these.

This will result in a list of "Address, URL, Price" tuples. Paste these into `listings.csv` and they will be loaded and geocoded automatically.

```
let results = [];

function extract() {
  const newRows = Array.from(document.querySelectorAll("article"))
    .map(article => {
      const priceEl   = article.querySelector('span[data-test="property-card-price"]');
      const linkEl    = article.querySelector('a[data-test="property-card-link"]');
      const addressEl = article.querySelector("address");

      const address = addressEl ? addressEl.textContent.trim().replace(/"/g, '""') : "";
      const url     = linkEl ? linkEl.href.replace(/"/g, '""') : "";
      const price   = priceEl ? priceEl.textContent.trim().replace(/"/g, '""') : "";

      return { address, url, price };
    })
    .filter(r => r.address && r.url && r.price)
    .map(r => `"${r.address}","${r.url}","${r.price}"`);
  const existing = new Set(results);
  newRows.forEach(r => {
    if (!existing.has(r)) results.push(r);
  });

  console.log(`Total rows: ${results.length}`);
}

function scrollToNext() {
  const next = document.querySelector('a[rel="next"][aria-disabled="false"]');
  if (!next) return;

  next.scrollIntoView({
    behavior: "smooth",
    block: "center"
  });
}

function hookNext() {
  const next = document.querySelector('a[rel="next"][aria-disabled="false"]');
  if (!next || next.__hooked) return;
  next.__hooked = true;
  next.addEventListener("click", () => {
    setTimeout(() => {
      scrollToNext();
      setTimeout(() => {
        extract();
      }, 2000);
    }, 500);
  });
}

extract();
hookNext();
setInterval(hookNext, 1000);
```

Navigate to the starting link printed by the console app, then paste the above into the DevTools. Click on the next page, and you will be scrolled to the bottom of the page automatically. Keep clicking through until you reach the last page. Then, paste this into the DevTools:
```
copy(results.join("\n"));
```

Your clipboard will now contain a CSV of all the data you need. Paste this into `listings.csv` and they will be automatically parsed on the next run of artemis.