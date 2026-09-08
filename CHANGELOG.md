# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Persist scores to database
- Ingest user input from ./data/ file
- Add units of measure
- Four Square POI data
- Location characteristics
- Regional characteristics
- Building characteristics

### Changed

- N/A

### Removed

- N/A

## [0.0.2] - 2026-09-05

### Added

- Store region definitions in database rather that hardcoded list
- New term category for regional characteristics
- Calculations for "comfortable days" using the Open-Meteo historical weather API

### Fixed

- N/A

### Changed

- N/A

### Removed

- N/A

## [0.0.1] - 2026-09-01

### Added

- Initial implementation of Artemis as a console app with group and term node tree structure
- 16 categories of Point of Interest (POI) data sourced from Open Street Map via Overpass Turbo API
- HTTP utility to handle exponential backoff with retries and a custom user agent
- Preload 3 sample criteria trees
- Linear and exponential evaluation modes
- Calculate and tag Pareto efficient locations

### Fixed

- N/A

### Changed

- N/A

### Removed

- N/A
