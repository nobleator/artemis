# Background
TODO

# Local Setup
TODO

# Unit Testing
TODO

# Deploy
TODO

## Dependencies
Stadia Maps

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
let results = Array.from(document.querySelectorAll("article"))
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
copy(results.join("\n"));
console.log(`Copied ${results.length} results to clipboard`);
```