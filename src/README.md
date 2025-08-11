# Background
F# + Fable + Elmish + Feliz + Vite + Preact

Fable converts the F# code to JavaScript. Vite serves that JavaScript.

This is the `/src/` subfolder of a parent web client. The parent folder is expected to contain a `/public/` folder with an `index.html` file with key ID & script references to the generate `Program.js` file. The parent also contains `package.json` and `vite.config.js`.

# Setup
Install dotnet fable tool:
`cd src`
`dotnet new tool-manifest`
`dotnet tool install fable`
Install vite globally with `pnpm i -g vite`
Install npm packages with `pnpm i`

# Dev
From `/src/`, run `dotnet fable watch --outDir ../public`
From parent folder, run `pnpm run dev`

# Deploy
Compile Fable from `/WebClient/src/`, run `dotnet fable --outDir ../public`.
From root (`/WebClient/`) run: `vite build`.  This will generate `/WebClient/public/dist/`.
Copy over leaflet images from `/WebClient/public/leaflet/images/` to `/WebClient/public/dist/`.
Copy over data files from `/WebClient/public/data/` to `/WebClient/public/dist/data/`.
Push to Netlify (or drag & drop entire `/WebClient/public/dist/` folder).
